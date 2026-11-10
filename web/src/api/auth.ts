import { request } from './client'
import type { AuthResponse } from './types'

export interface Credentials {
  email: string
  password: string
}

export const register = (credentials: Credentials) =>
  request<AuthResponse>('POST', '/auth/register', credentials)

export const login = (credentials: Credentials) => request<AuthResponse>('POST', '/auth/login', credentials)
