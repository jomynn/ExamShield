/**
 * Load scenario: Concurrent manual review queue processing
 *
 * Simulates 50 reviewers polling the review queue and submitting answers,
 * 10 supervisors approving/rejecting decisions.
 *
 * Validates:
 *   - Review queue fetch p95 < 300 ms
 *   - Review submit p95 < 400 ms
 *   - 0 % error rate
 *
 * Run:
 *   k6 run --env BASE_URL=http://localhost:5000 scenarios/concurrent-review.js
 */

import http from 'k6/http'
import { check, sleep } from 'k6'
import { Trend, Rate } from 'k6/metrics'
import { BASE_URL, ADMIN_EMAIL, ADMIN_PASS } from '../config.js'

export const options = {
  scenarios: {
    reviewers: {
      executor: 'constant-vus',
      vus: 50,
      duration: '3m',
      tags: { role: 'reviewer' },
    },
    supervisors: {
      executor: 'constant-vus',
      vus: 10,
      duration: '3m',
      startTime: '30s',
      tags: { role: 'supervisor' },
    },
  },
  thresholds: {
    'http_req_duration{endpoint:queue}':  ['p(95)<300'],
    'http_req_duration{endpoint:submit}': ['p(95)<400'],
    'http_req_failed':                    ['rate<0.01'],
  },
}

const queueLatency  = new Trend('review_queue_latency',  true)
const submitLatency = new Trend('review_submit_latency', true)
const errorRate     = new Rate('review_errors')

export function setup() {
  const res = http.post(`${BASE_URL}/auth/login`,
    JSON.stringify({ email: ADMIN_EMAIL, password: ADMIN_PASS }),
    { headers: { 'Content-Type': 'application/json' } })
  return { token: res.json('token') }
}

export default function (data) {
  const headers = { Authorization: `Bearer ${data.token}`, 'Content-Type': 'application/json' }
  const role    = __ENV.K6_SCENARIO_NAME === 'supervisors' ? 'supervisor' : 'reviewer'

  if (role === 'reviewer') {
    // Fetch pending reviews
    const queueRes = http.get(`${BASE_URL}/reviews?status=Pending&pageSize=10`,
      { headers, tags: { endpoint: 'queue' } })
    queueLatency.add(queueRes.timings.duration)
    check(queueRes, { 'queue 200': r => r.status === 200 })

    const reviews = queueRes.json('reviews') ?? []
    for (const review of reviews.slice(0, 3)) {
      const submitRes = http.post(
        `${BASE_URL}/reviews/${review.id}/submit`,
        JSON.stringify({ interpretedAnswer: ['A', 'B', 'C', 'D'][Math.floor(Math.random() * 4)] }),
        { headers, tags: { endpoint: 'submit' } })
      submitLatency.add(submitRes.timings.duration)
      errorRate.add(!check(submitRes, { 'submit 200': r => r.status === 200 || r.status === 204 }))
    }
  } else {
    // Supervisor approves pending reviews
    const pendingRes = http.get(`${BASE_URL}/reviews?status=PendingApproval&pageSize=5`,
      { headers, tags: { endpoint: 'queue' } })
    const reviews = pendingRes.json('reviews') ?? []
    for (const review of reviews) {
      http.post(`${BASE_URL}/reviews/${review.id}/approve`, null, { headers })
    }
  }

  sleep(Math.random() * 3 + 1)
}
