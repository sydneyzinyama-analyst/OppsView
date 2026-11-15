// Mirrors the API's DTOs (api/src/OppsView.Api/Features/*/…Dtos.cs).
// Enums travel as strings thanks to JsonStringEnumConverter on the server.

export const STATUSES = ['Applied', 'Viewed', 'Interview', 'Offer', 'Rejected'] as const
export type ApplicationStatus = (typeof STATUSES)[number]

export const PLATFORMS = ['Pnet', 'Indeed', 'LinkedIn', 'Workable', 'CompanyPortal', 'Other'] as const
export type JobPlatform = (typeof PLATFORMS)[number]

export const PLATFORM_LABELS: Record<JobPlatform, string> = {
  Pnet: 'Pnet',
  Indeed: 'Indeed',
  LinkedIn: 'LinkedIn',
  Workable: 'Workable',
  CompanyPortal: 'Company portal',
  Other: 'Other',
}

/** Who made a change: the user, an email, or OppsView after a reply deadline passed. */
export type ApplicationSource = 'Manual' | 'Email' | 'NoReply'

export interface Application {
  id: string
  company: string
  role: string
  platform: JobPlatform
  status: ApplicationStatus
  /** yyyy-MM-dd (a DateOnly on the server: no time, no time zone). */
  appliedOn: string
  jobUrl: string | null
  notes: string | null
  source: ApplicationSource
  /** "No reply by this date means unsuccessful" (yyyy-MM-dd), from the employer's email. */
  noReplyDeadline: string | null
  /** ISO 8601 UTC timestamps. */
  createdAt: string
  updatedAt: string
  /** Statuses the server will accept next. The UI never re-implements the rules. */
  allowedNextStatuses: ApplicationStatus[]
}

/** Body for POST /applications and PUT /applications/{id}. */
export interface SaveApplicationRequest {
  company: string
  role: string
  platform: JobPlatform
  appliedOn: string
  jobUrl: string | null
  notes: string | null
}

export interface StatusChange {
  fromStatus: ApplicationStatus
  toStatus: ApplicationStatus
  changedAt: string
  source: ApplicationSource
}

export interface AuthResponse {
  token: string
  expiresAt: string
  email: string
}

// ---- Email import (Gmail) ------------------------------------------------

export type EmailKind =
  | 'ApplicationReceived'
  | 'ApplicationViewed'
  | 'InterviewInvite'
  | 'Offer'
  | 'Rejection'
  | 'NotRelevant'
  | 'Unknown'

export interface EmailStatus {
  /** False when the server has no Google settings (see README). */
  available: boolean
  connected: boolean
  emailAddress: string | null
  lastSyncedAt: string | null
  /** Google stopped accepting the connection; the user must connect again. */
  needsReconnect: boolean
  needsReviewCount: number
}

export interface ImportSummary {
  created: number
  updated: number
  noChange: number
  ignored: number
  needsReview: number
  alreadyProcessed: number
  /** Emails Gmail wouldn't hand over this time; the next sync tries them again. */
  failed: number
  /** Applications marked Rejected because the employer's reply deadline passed. */
  noReplyRejected: number
}

/** An email OppsView wasn't sure about. */
export interface ReviewItem {
  id: string
  receivedAt: string
  sender: string
  subject: string
  kind: EmailKind
  platform: JobPlatform | null
  company: string | null
  role: string | null
  reason: string | null
  applicationId: string | null
}
