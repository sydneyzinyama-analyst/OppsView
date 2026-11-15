import { request } from './client'
import type { Application, EmailStatus, ImportSummary, ReviewItem, SaveApplicationRequest } from './types'

export const getEmailStatus = () => request<EmailStatus>('GET', '/email/status')

/** Returns Google's consent page URL; the caller sends the browser there. */
export const startGmailConnect = () =>
  request<{ authorizationUrl: string }>('POST', '/email/google/connect').then((r) => r.authorizationUrl)

export const disconnectGmail = () => request<void>('DELETE', '/email/google')

export const syncEmail = () => request<ImportSummary>('POST', '/email/sync')

export const listReviewItems = () => request<ReviewItem[]>('GET', '/email/review')

export const dismissReviewItem = (id: string) => request<void>('POST', `/email/review/${id}/dismiss`)

export const createFromReviewItem = (id: string, body: SaveApplicationRequest) =>
  request<Application>('POST', `/email/review/${id}/application`, body)
