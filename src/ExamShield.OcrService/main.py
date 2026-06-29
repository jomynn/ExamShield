"""
ExamShield OCR Service — OMR bubble-sheet detection via OpenCV.

POST /ocr/extract   Accept: application/octet-stream
Returns: {"answers": [{"questionNumber": 1, "selectedOption": "A", "confidence": 0.95}]}
"""

import logging
import os
from dataclasses import asdict, dataclass
from typing import List

import cv2
import numpy as np
from fastapi import FastAPI, HTTPException, Request

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = FastAPI(title="ExamShield OCR Service", version="1.0.0")

OPTIONS = ["A", "B", "C", "D", "E"]
NUM_QUESTIONS = int(os.getenv("OCR_NUM_QUESTIONS", "50"))
NUM_OPTIONS = int(os.getenv("OCR_NUM_OPTIONS", "5"))


@dataclass
class OcrAnswerDto:
    questionNumber: int
    selectedOption: str
    confidence: float


# ---------------------------------------------------------------------------
# Image processing helpers
# ---------------------------------------------------------------------------

def _threshold(gray: np.ndarray) -> np.ndarray:
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    return cv2.adaptiveThreshold(
        blurred, 255,
        cv2.ADAPTIVE_THRESH_GAUSSIAN_C,
        cv2.THRESH_BINARY_INV, 11, 2,
    )


def _find_bubble_contours(gray: np.ndarray, thresh: np.ndarray) -> list:
    h, w = gray.shape
    min_area = w * h * 0.0003
    max_area = w * h * 0.015

    contours, _ = cv2.findContours(thresh, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    bubbles = []
    for cnt in contours:
        area = cv2.contourArea(cnt)
        if not (min_area <= area <= max_area):
            continue
        x, y, bw, bh = cv2.boundingRect(cnt)
        aspect = bw / max(bh, 1)
        if not (0.6 <= aspect <= 1.6):
            continue
        perimeter = cv2.arcLength(cnt, True)
        circularity = 4 * np.pi * area / max(perimeter ** 2, 1e-6)
        if circularity < 0.45:
            continue
        cx, cy = x + bw // 2, y + bh // 2
        bubbles.append((cx, cy, bw, bh, cnt))

    return bubbles


def _group_rows(bubbles: list) -> dict:
    """Cluster bubbles into horizontal rows by y-coordinate."""
    if not bubbles:
        return {}

    avg_h = float(np.mean([b[3] for b in bubbles]))
    tolerance = avg_h * 0.45

    rows: dict[float, list] = {}
    for bubble in sorted(bubbles, key=lambda b: b[1]):
        cy = bubble[1]
        matched = next((k for k in rows if abs(cy - k) <= tolerance), None)
        if matched is not None:
            rows[matched].append(bubble)
        else:
            rows[float(cy)] = [bubble]

    return rows


def _fill_ratio(cnt: np.ndarray, thresh: np.ndarray) -> float:
    mask = np.zeros(thresh.shape, dtype=np.uint8)
    cv2.drawContours(mask, [cnt], -1, 255, -1)
    filled = cv2.countNonZero(cv2.bitwise_and(thresh, thresh, mask=mask))
    total = cv2.countNonZero(mask)
    return filled / max(total, 1)


def _confidence_from_gap(scores: list, best_idx: int) -> float:
    """Confidence = normalised gap between best and second-best fill ratio."""
    if len(scores) < 2:
        return 0.5
    best = scores[best_idx]
    second = max(s for i, s in enumerate(scores) if i != best_idx)
    gap = best - second
    # gap of 0.25+ → confidence 1.0; gap of 0 → confidence 0.1
    return round(min(1.0, max(0.1, gap / 0.25)), 3)


# ---------------------------------------------------------------------------
# Main extraction logic
# ---------------------------------------------------------------------------

def extract_answers(
    image_bytes: bytes,
    num_questions: int = NUM_QUESTIONS,
    num_options: int = NUM_OPTIONS,
) -> List[OcrAnswerDto]:
    nparr = np.frombuffer(image_bytes, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
    if img is None:
        raise ValueError("Cannot decode image — unsupported format or corrupted bytes")

    # Perspective correction: find the largest quadrilateral (answer sheet border)
    img = _correct_perspective(img)

    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    thresh = _threshold(gray)
    bubbles = _find_bubble_contours(gray, thresh)

    if len(bubbles) < num_options:
        logger.warning("Only %d bubbles detected; returning empty result", len(bubbles))
        return []

    rows = _group_rows(bubbles)
    answers: List[OcrAnswerDto] = []
    q_num = 1

    for row_y in sorted(rows.keys()):
        if q_num > num_questions:
            break
        row = sorted(rows[row_y], key=lambda b: b[0])[:num_options]
        if len(row) < 2:
            continue

        scores = [_fill_ratio(b[4], thresh) for b in row]
        best_idx = int(np.argmax(scores))
        confidence = _confidence_from_gap(scores, best_idx)
        option = OPTIONS[best_idx] if best_idx < len(OPTIONS) else str(best_idx + 1)
        answers.append(OcrAnswerDto(q_num, option, confidence))
        q_num += 1

    return answers


def _correct_perspective(img: np.ndarray) -> np.ndarray:
    """
    Attempt to detect the answer-sheet boundary and warp it to a straight rectangle.
    Falls back to the original image if no suitable quadrilateral is found.
    """
    gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    edged = cv2.Canny(blurred, 75, 200)

    contours, _ = cv2.findContours(edged, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    contours = sorted(contours, key=cv2.contourArea, reverse=True)[:5]

    for cnt in contours:
        peri = cv2.arcLength(cnt, True)
        approx = cv2.approxPolyDP(cnt, 0.02 * peri, True)
        if len(approx) == 4:
            return _four_point_transform(img, approx.reshape(4, 2))

    return img


def _order_points(pts: np.ndarray) -> np.ndarray:
    rect = np.zeros((4, 2), dtype="float32")
    s = pts.sum(axis=1)
    rect[0] = pts[np.argmin(s)]
    rect[2] = pts[np.argmax(s)]
    diff = np.diff(pts, axis=1)
    rect[1] = pts[np.argmin(diff)]
    rect[3] = pts[np.argmax(diff)]
    return rect


def _four_point_transform(image: np.ndarray, pts: np.ndarray) -> np.ndarray:
    rect = _order_points(pts)
    (tl, tr, br, bl) = rect

    width_a = np.linalg.norm(br - bl)
    width_b = np.linalg.norm(tr - tl)
    max_width = max(int(width_a), int(width_b))

    height_a = np.linalg.norm(tr - br)
    height_b = np.linalg.norm(tl - bl)
    max_height = max(int(height_a), int(height_b))

    if max_width < 50 or max_height < 50:
        return image

    dst = np.array(
        [[0, 0], [max_width - 1, 0], [max_width - 1, max_height - 1], [0, max_height - 1]],
        dtype="float32",
    )
    M = cv2.getPerspectiveTransform(rect, dst)
    return cv2.warpPerspective(image, M, (max_width, max_height))


# ---------------------------------------------------------------------------
# API endpoints
# ---------------------------------------------------------------------------

@app.get("/health")
def health():
    return {"status": "healthy", "service": "ExamShield OCR"}


@app.post("/ocr/extract")
async def extract(request: Request):
    body = await request.body()
    if not body:
        raise HTTPException(status_code=400, detail="Request body is empty")

    try:
        answers = extract_answers(body)
        logger.info("Extracted %d answers", len(answers))
        return {"answers": [asdict(a) for a in answers]}
    except Exception as exc:
        logger.exception("OCR extraction failed")
        raise HTTPException(status_code=422, detail=str(exc)) from exc
