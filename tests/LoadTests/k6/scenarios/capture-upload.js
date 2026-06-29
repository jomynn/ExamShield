/**
 * Load scenario: Invigilator capture + upload pipeline
 *
 * Simulates 200 concurrent invigilators each performing:
 *   1. POST /auth/login
 *   2. POST /capture  (register hash + signature)
 *   3. POST /upload   (upload image bytes)
 *   4. GET  /verify/{id}
 *
 * Target: p95 < 500 ms for steps 2–4, 0 % error rate.
 *
 * Run:
 *   k6 run --env BASE_URL=http://localhost:5000 scenarios/capture-upload.js
 */

import http from 'k6/http'
import { check, sleep } from 'k6'
import { Trend, Rate, Counter } from 'k6/metrics'
import { BASE_URL, ADMIN_EMAIL, ADMIN_PASS, TINY_JPEG } from '../config.js'
import crypto from 'k6/crypto'

export const options = {
  stages: [
    { duration: '30s', target: 50   },  // ramp up
    { duration: '2m',  target: 200  },  // sustained load
    { duration: '30s', target: 0    },  // ramp down
  ],
  thresholds: {
    'http_req_duration{scenario:capture}': ['p(95)<500'],
    'http_req_duration{scenario:upload}':  ['p(95)<800'],
    'http_req_failed':                     ['rate<0.01'],
  },
}

const captureLatency = new Trend('capture_latency', true)
const uploadLatency  = new Trend('upload_latency',  true)
const verifyLatency  = new Trend('verify_latency',  true)
const errorRate      = new Rate('pipeline_errors')
const totalCaptures  = new Counter('total_captures')

let token

export function setup() {
  const res = http.post(`${BASE_URL}/auth/login`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASS }),
    { headers: { 'Content-Type': 'application/json' } })
  check(res, { 'login ok': r => r.status === 200 })
  return { token: res.json('token') }
}

export default function (data) {
  const headers = {
    'Authorization': `Bearer ${data.token}`,
    'Content-Type': 'application/json',
  }

  // 1. Register capture
  const imageHash  = crypto.sha256(TINY_JPEG, 'hex')
  const captureId  = crypto.md5(imageHash + __VU + __ITER, 'hex')
    .replace(/(.{8})(.{4})(.{4})(.{4})(.{12})/, '$1-$2-$3-$4-$5')
  const stubSig    = 'MEUCIQCsignaturestubAAECAw=='   // placeholder — real devices sign on-device

  const captureBody = JSON.stringify({
    imageHash,
    signature: stubSig,
    deviceId:  '00000000-0000-0000-0000-000000000001',
    examId:    '00000000-0000-0000-0000-000000000001',
    studentId: '00000000-0000-0000-0000-000000000002',
  })

  const captureRes = http.post(`${BASE_URL}/capture`, captureBody, { headers, tags: { scenario: 'capture' } })
  captureLatency.add(captureRes.timings.duration)

  const captureOk = check(captureRes, { 'capture 201': r => r.status === 201 })
  errorRate.add(!captureOk)

  if (!captureOk) { sleep(1); return }

  const cid = captureRes.json('captureId') ?? captureRes.json('id')
  totalCaptures.add(1)

  // 2. Upload image bytes
  const uploadRes = http.post(
    `${BASE_URL}/upload`,
    TINY_JPEG.buffer,
    {
      headers: { ...headers, 'Content-Type': 'application/octet-stream', 'X-Capture-Id': cid },
      tags: { scenario: 'upload' },
    })
  uploadLatency.add(uploadRes.timings.duration)
  const uploadOk = check(uploadRes, { 'upload 200': r => r.status === 200 || r.status === 204 })
  errorRate.add(!uploadOk)

  if (!uploadOk) { sleep(1); return }

  // 3. Verify
  const verifyRes = http.get(`${BASE_URL}/verify/${cid}`, { headers, tags: { scenario: 'verify' } })
  verifyLatency.add(verifyRes.timings.duration)
  check(verifyRes, { 'verify 200': r => r.status === 200 })

  sleep(Math.random() * 2 + 0.5)
}
