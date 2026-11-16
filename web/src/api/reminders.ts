import { request } from './client'
import type { Reminder, ReminderSettings } from './types'

export const listReminders = () => request<Reminder[]>('GET', '/reminders')

export const countReminders = () => request<{ open: number }>('GET', '/reminders/count').then((r) => r.open)

export const markReminderDone = (id: string) => request<void>('POST', `/reminders/${id}/done`)

export const dismissReminder = (id: string) => request<void>('POST', `/reminders/${id}/dismiss`)

export const snoozeReminder = (id: string, days: number) => request<void>('POST', `/reminders/${id}/snooze`, { days })

export const getReminderSettings = () => request<ReminderSettings>('GET', '/reminders/settings')

export const saveReminderSettings = (settings: ReminderSettings) =>
  request<ReminderSettings>('PUT', '/reminders/settings', settings)
