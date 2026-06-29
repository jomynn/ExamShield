/**
 * Load scenario: OCR pipeline throughput
 *
 * Measures the throughput and latency of the OCR trigger pipeline under
 * sustained load. Simulates bulk OCR job submission (POST /ocr/{captureId})
 * and queue depth monitoring (GET /ocr/queue).
 *
 * Target: sustain 500 OCR triggers/min, queue depth stays < 1000.
 *
 * Run:
 *   k6 run --env BASE_URL=http://localhost:5000 scenarios/ocr-pipeline.js
 */

import http from 'k6/http'
import { check, sleep } from 'k6'
import { Trend, Gauge, Counter } from 'k6/metrics'
import { BASE_URL, ADMIN_EMAIL, ADMIN_PASS } from '../config.js'

export const options = {
  scenarios: {
    ocrTriggers: {
      executor: 'ramping-arrival-rate',
      startRate: 10,
      timeUnit: '1s',
      preAllocatedVUs: 50,
      maxVUs: 200,
      stages: [
        { target: 10, duration: '30s' },   // warm-up
        { target: 50, duration: '2m'  },   // ramp to ~3000/min
        { target: 50, duration: '3m'  },   // sustain
        { target: 0,  duration: '30s' },   // cool down
      ],
    },
  },
  thresholds: {
    'http_req_duration{endpoint:trigger}': ['p(95)<600'],
    'http_req_duration{endpoint:queue}':   ['p(95)<200'],
    'http_req_failed':                     ['rate<0.02'],
    'ocr_queue_depth':                     ['value<1000'],
  },
}

const triggerLatency = new Trend('ocr_trigger_latency', true)
const queueDepth     = new Gauge('ocr_queue_depth')
const ocrTriggers    = new Counter('ocr_triggers_total')

// A set of pre-existing capture IDs to trigger OCR on.
// In practice populate via the API or use a data file; here we use a stub UUID.
const STUB_CAPTURE_ID = '00000000-0000-0000-0000-000000000001'

export function setup() {
  const res = http.post(`${BASE_URL}/auth/login`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASS }),
    { headers: { 'Content-Type': 'application/json' } })
  return { token: res.json('token') }
}

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}`, 'Content-Type': 'application/json' }

  // Trigger OCR
  const trigRes = http.post(`${BASE_URL}/ocr/${STUB_CAPTURE_ID}`, null,
    { headers, tags: { endpoint: 'trigger' } })
  triggerLatency.add(trigRes.timings.duration)
  check(trigRes, { 'ocr trigger accepted': r => r.status === 202 || r.status === 200 })
  ocrTriggers.add(1)

  // Periodically check queue depth (1-in-10 VUs)
  if (__ITER % 10 === 0) {
    const queueRes = http.get(`${BASE_URL}/ocr/queue`, { headers, tags: { endpoint: 'queue' } })
    if (queueRes.status === 200) {
      const depth = queueRes.json('pendingCount') ?? 0
      queueDepth.add(depth)
    }
  }

  sleep(0.1)
}
