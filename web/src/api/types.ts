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
}

export interface AuthResponse {
  token: string
  expiresAt: string
  email: string
}
