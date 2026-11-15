import { ApiError, request } from './client'
import type { Cv, MatchResult, SavedMatch } from './types'

/** The stored CV, or null if none has been uploaded. */
export async function getCv(): Promise<Cv | null> {
  try {
    return await request<Cv>('GET', '/cv')
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) return null
    throw e
  }
}

export function uploadCv(file: File): Promise<Cv> {
  const form = new FormData()
  form.append('file', file)
  return request<Cv>('PUT', '/cv', form)
}

export const deleteCv = () => request<void>('DELETE', '/cv')

export const matchCv = (jobAd: string, applicationId?: string | null) =>
  request<MatchResult>('POST', '/match', { jobAd, applicationId: applicationId || null })

/** The match saved on an application, or null if it was never matched. */
export async function getSavedMatch(applicationId: string): Promise<SavedMatch | null> {
  try {
    return await request<SavedMatch>('GET', `/applications/${applicationId}/match`)
  } catch (e) {
    if (e instanceof ApiError && e.status === 404) return null
    throw e
  }
}
