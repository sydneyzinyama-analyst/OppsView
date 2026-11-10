import { afterEach, describe, expect, it, vi } from 'vitest'
import { jsonResponse } from '../test/fixtures'
import { ApiError, request, setAuthToken, setUnauthorizedHandler } from './client'

const fetchMock = vi.fn<typeof fetch>()
vi.stubGlobal('fetch', fetchMock)

afterEach(() => {
  setAuthToken(null)
  setUnauthorizedHandler(() => {})
})

describe('request', () => {
  it('sends JSON and the bearer token', async () => {
    setAuthToken('abc')
    fetchMock.mockResolvedValueOnce(jsonResponse(200, { ok: true }))

    await expect(request('POST', '/things', { a: 1 })).resolves.toEqual({ ok: true })

    const [url, init] = fetchMock.mock.calls[0]
    expect(url).toBe('http://localhost:5226/things')
    expect(init).toMatchObject({ body: '{"a":1}', headers: { Authorization: 'Bearer abc' } })
  })

  it('returns undefined for 204 No Content', async () => {
    fetchMock.mockResolvedValueOnce(new Response(null, { status: 204 }))
    await expect(request('DELETE', '/things/1')).resolves.toBeUndefined()
  })

  it('maps ValidationProblemDetails to field errors', async () => {
    fetchMock.mockResolvedValueOnce(
      jsonResponse(400, {
        title: 'One or more validation errors occurred.',
        errors: { company: ['Company is required.'], AppliedOn: ["Can't be in the future.", 'Other'] },
      }),
    )

    const error = (await request('POST', '/applications', {}).catch((e: unknown) => e)) as ApiError

    expect(error).toBeInstanceOf(ApiError)
    expect(error.status).toBe(400)
    expect(error.fieldErrors).toEqual({ company: 'Company is required.', appliedOn: "Can't be in the future." })
  })

  it('calls the unauthorized handler on 401 when a token was sent', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    setAuthToken('expired')
    fetchMock.mockResolvedValueOnce(jsonResponse(401))

    await expect(request('GET', '/applications')).rejects.toMatchObject({ status: 401 })
    expect(handler).toHaveBeenCalledOnce()
  })

  it('does not call the unauthorized handler for a failed login (no token)', async () => {
    const handler = vi.fn()
    setUnauthorizedHandler(handler)
    fetchMock.mockResolvedValueOnce(jsonResponse(401, { title: 'Incorrect email or password.' }))

    await expect(request('POST', '/auth/login', {})).rejects.toThrow('Incorrect email or password.')
    expect(handler).not.toHaveBeenCalled()
  })

  it('reports an unreachable API clearly', async () => {
    fetchMock.mockRejectedValueOnce(new TypeError('Failed to fetch'))
    await expect(request('GET', '/applications')).rejects.toThrow(/Can't reach/)
  })
})
