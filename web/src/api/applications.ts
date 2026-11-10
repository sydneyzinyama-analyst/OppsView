import { request } from './client'
import type { Application, ApplicationStatus, SaveApplicationRequest, StatusChange } from './types'

export const listApplications = () => request<Application[]>('GET', '/applications')

export const createApplication = (body: SaveApplicationRequest) =>
  request<Application>('POST', '/applications', body)

export const updateApplication = (id: string, body: SaveApplicationRequest) =>
  request<Application>('PUT', `/applications/${id}`, body)

export const deleteApplication = (id: string) => request<void>('DELETE', `/applications/${id}`)

export const changeStatus = (id: string, status: ApplicationStatus) =>
  request<Application>('PATCH', `/applications/${id}/status`, { status })

export const getHistory = (id: string) => request<StatusChange[]>('GET', `/applications/${id}/history`)
