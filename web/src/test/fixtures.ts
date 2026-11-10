import type { Application } from '../api/types'

export function makeApplication(overrides: Partial<Application> = {}): Application {
  return {
    id: 'a1',
    company: 'Acme',
    role: 'Junior Developer',
    platform: 'Pnet',
    status: 'Applied',
    appliedOn: '2026-11-09',
    jobUrl: null,
    notes: null,
    createdAt: '2026-11-09T08:00:00Z',
    updatedAt: '2026-11-09T08:00:00Z',
    allowedNextStatuses: ['Viewed', 'Interview', 'Offer', 'Rejected'],
    ...overrides,
  }
}

/** A fetch Response with a JSON body. */
export function jsonResponse(status: number, body?: unknown): Response {
  return new Response(body === undefined ? null : JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}
