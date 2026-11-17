import { request } from './client'
import type { ApplicationStatus, JobPlatform } from './types'

// Mirrors api/src/OppsView.Api/Features/Analytics/AnalyticsDtos.cs.

export interface Analytics {
  totals: {
    applications: number
    active: number
    interviews: number
    offers: number
    rejected: number
    /** 0-1: share that got a reply (interview, offer or rejection). */
    responseRate: number
    averageDaysToResponse: number | null
  }
  /** Oldest first; weekStart is the Monday (yyyy-MM-dd). */
  perWeek: { weekStart: string; applications: number }[]
  byPlatform: { platform: JobPlatform; applications: number; responses: number; responseRate: number }[]
  funnel: { stage: ApplicationStatus; reached: number }[]
  rejectedAt: { from: ApplicationStatus; count: number }[]
  waiting: { stage: ApplicationStatus; count: number; averageDays: number; waitingOver14Days: number }[]
  topMissingSkills: { skill: string; count: number }[]
  sources: { manual: number; email: number }
}

export const getAnalytics = (weeks = 12) => request<Analytics>('GET', `/analytics?weeks=${weeks}`)
