"""
Unit + integration tests for the ExamShield OCR service.

Strategy:
  - Pure helper functions (_group_rows, _fill_ratio, _confidence_from_gap) are
    tested with synthetic numpy inputs — fast and fully deterministic.
  - _threshold is tested with a known grayscale array.
  - The FastAPI endpoints are tested with TestClient using minimal synthetic JPEG
    images (plain white/black, not real bubble sheets) to verify wiring and error
    paths without depending on ML accuracy.
  - extract_answers is tested for error handling; actual bubble detection accuracy
    is validated by the CI smoke-test against a running service with real images.
"""
import io
import sys
import os

import cv2
import numpy as np
import pytest
from starlette.testclient import TestClient

# Make the parent directory importable so we can import main.py
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from main import (
    _confidence_from_gap,
    _fill_ratio,
    _group_rows,
    _threshold,
    app,
    extract_answers,
)

client = TestClient(app)


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _make_jpeg(width: int = 64, height: int = 64, fill: int = 200) -> bytes:
    """Create a plain single-colour JPEG for endpoint tests."""
    img = np.full((height, width, 3), fill, dtype=np.uint8)
    ok, buf = cv2.imencode(".jpg", img)
    assert ok
    return buf.tobytes()


def _make_gray(width: int = 64, height: int = 64, fill: int = 200) -> np.ndarray:
    return np.full((height, width), fill, dtype=np.uint8)


# ---------------------------------------------------------------------------
# _threshold
# ---------------------------------------------------------------------------

class TestThreshold:
    def test_returns_binary_image(self):
        gray = _make_gray()
        result = _threshold(gray)
        assert result.shape == gray.shape
        unique = set(result.flatten().tolist())
        assert unique.issubset({0, 255})

    def test_dark_pixels_become_white_after_invert(self):
        """
        adaptiveThreshold with THRESH_BINARY_INV: dark pixels (low value) in the
        source become 255 in the output.  A uniformly bright image → mostly 0.
        """
        gray = _make_gray(fill=240)
        result = _threshold(gray)
        # Most pixels should be 0 (bright → not inverted)
        zeros = np.count_nonzero(result == 0)
        total = result.size
        assert zeros / total > 0.5


# ---------------------------------------------------------------------------
# _group_rows
# ---------------------------------------------------------------------------

class TestGroupRows:
    def test_empty_input_returns_empty(self):
        assert _group_rows([]) == {}

    def test_single_bubble_forms_one_row(self):
        bubble = (10, 20, 8, 8, None)
        rows = _group_rows([bubble])
        assert len(rows) == 1

    def test_nearby_y_coords_grouped_together(self):
        # Two bubbles 3 px apart should land in the same row
        b1 = (10, 50, 8, 8, None)
        b2 = (50, 53, 8, 8, None)
        rows = _group_rows([b1, b2])
        assert len(rows) == 1

    def test_distant_y_coords_form_separate_rows(self):
        b1 = (10, 10, 8, 8, None)
        b2 = (10, 200, 8, 8, None)
        rows = _group_rows([b1, b2])
        assert len(rows) == 2

    def test_returns_correct_number_of_rows_for_grid(self):
        # Simulate a 3-row × 5-column grid (row spacing = 40 px)
        bubbles = [(col * 20, row * 40, 8, 8, None)
                   for row in range(3) for col in range(5)]
        rows = _group_rows(bubbles)
        assert len(rows) == 3


# ---------------------------------------------------------------------------
# _confidence_from_gap
# ---------------------------------------------------------------------------

class TestConfidenceFromGap:
    def test_single_score_returns_medium_confidence(self):
        assert _confidence_from_gap([0.9], 0) == 0.5

    def test_large_gap_returns_high_confidence(self):
        # gap = 0.8 - 0.2 = 0.6 → ≥ 0.25 threshold → 1.0
        result = _confidence_from_gap([0.8, 0.2], 0)
        assert result == 1.0

    def test_zero_gap_returns_minimum_confidence(self):
        # best == second → gap = 0 → 0.1
        result = _confidence_from_gap([0.5, 0.5], 0)
        assert result == 0.1

    def test_medium_gap_interpolates(self):
        # gap = 0.5 - 0.375 = 0.125; normalised = 0.125/0.25 = 0.5
        result = _confidence_from_gap([0.5, 0.375], 0)
        assert 0.4 <= result <= 0.6

    def test_result_clamped_between_0_1_and_1_0(self):
        for scores, idx in [([1.0, 0.0], 0), ([0.0, 0.0], 0)]:
            r = _confidence_from_gap(scores, idx)
            assert 0.1 <= r <= 1.0


# ---------------------------------------------------------------------------
# _fill_ratio
# ---------------------------------------------------------------------------

class TestFillRatio:
    def _make_contour_and_thresh(self, size: int = 20, fill_fraction: float = 1.0) -> tuple:
        """Create a square contour and a threshold image."""
        thresh = np.zeros((100, 100), dtype=np.uint8)
        # Draw a filled rectangle in the threshold image
        filled_h = int(size * fill_fraction)
        thresh[10:10 + filled_h, 10:10 + size] = 255
        # Contour is the bounding square
        contour = np.array(
            [[[10, 10]], [[10 + size, 10]], [[10 + size, 10 + size]], [[10, 10 + size]]],
            dtype=np.int32)
        return contour, thresh

    def test_fully_filled_contour_returns_close_to_one(self):
        cnt, thresh = self._make_contour_and_thresh(fill_fraction=1.0)
        ratio = _fill_ratio(cnt, thresh)
        assert ratio > 0.9

    def test_empty_contour_returns_close_to_zero(self):
        cnt, thresh = self._make_contour_and_thresh(fill_fraction=0.0)
        ratio = _fill_ratio(cnt, thresh)
        assert ratio < 0.1


# ---------------------------------------------------------------------------
# HTTP API — /health
# ---------------------------------------------------------------------------

class TestHealthEndpoint:
    def test_health_returns_200(self):
        resp = client.get("/health")
        assert resp.status_code == 200

    def test_health_returns_healthy_status(self):
        resp = client.get("/health")
        assert resp.json()["status"] == "healthy"

    def test_health_returns_service_name(self):
        resp = client.get("/health")
        assert "service" in resp.json()


# ---------------------------------------------------------------------------
# HTTP API — /ocr/extract
# ---------------------------------------------------------------------------

class TestExtractEndpoint:
    def test_empty_body_returns_400(self):
        resp = client.post("/ocr/extract", content=b"")
        assert resp.status_code == 400

    def test_corrupt_bytes_returns_422(self):
        resp = client.post(
            "/ocr/extract",
            content=b"\x00\x01\x02\x03corrupt",
            headers={"Content-Type": "application/octet-stream"},
        )
        assert resp.status_code == 422

    def test_valid_jpeg_returns_200(self):
        jpeg = _make_jpeg()
        resp = client.post(
            "/ocr/extract",
            content=jpeg,
            headers={"Content-Type": "application/octet-stream"},
        )
        assert resp.status_code == 200

    def test_valid_jpeg_returns_answers_key(self):
        jpeg = _make_jpeg()
        resp = client.post(
            "/ocr/extract",
            content=jpeg,
            headers={"Content-Type": "application/octet-stream"},
        )
        assert "answers" in resp.json()

    def test_answers_is_list(self):
        jpeg = _make_jpeg()
        resp = client.post(
            "/ocr/extract",
            content=jpeg,
            headers={"Content-Type": "application/octet-stream"},
        )
        assert isinstance(resp.json()["answers"], list)


# ---------------------------------------------------------------------------
# extract_answers — unit-level
# ---------------------------------------------------------------------------

class TestExtractAnswers:
    def test_invalid_bytes_raise_value_error(self):
        with pytest.raises(ValueError, match="Cannot decode"):
            extract_answers(b"\x00\x00corrupt")

    def test_plain_jpeg_no_bubbles_returns_empty_list(self):
        # A plain white image has no detectable bubbles
        jpeg = _make_jpeg(width=400, height=600, fill=255)
        result = extract_answers(jpeg)
        assert isinstance(result, list)
        # Empty is fine — no bubbles on a blank page
        assert len(result) == 0
