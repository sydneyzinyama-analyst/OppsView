import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../api/client'
import * as matchingApi from '../api/matching'
import type { Cv } from '../api/types'
import { CvPanel } from './CvPanel'

const cv: Cv = { fileName: 'my-cv.pdf', uploadedAt: '2026-11-15T10:00:00Z', characters: 1200, preview: 'Sydney, developer…' }
const file = () => new File(['C# and React'], 'cv.pdf', { type: 'application/pdf' })

describe('CvPanel', () => {
  it('uploads a CV and reports it', async () => {
    const upload = vi.spyOn(matchingApi, 'uploadCv').mockResolvedValue(cv)
    const onChanged = vi.fn()
    render(<CvPanel cv={null} onChanged={onChanged} />)

    await userEvent.upload(screen.getByLabelText('Upload CV'), file())

    expect(upload).toHaveBeenCalledWith(expect.any(File))
    expect(onChanged).toHaveBeenCalledWith(cv)
  })

  it("shows the matcher's reason when a file can't be read", async () => {
    vi.spyOn(matchingApi, 'uploadCv').mockRejectedValue(
      new ApiError(400, 'Validation failed', { file: 'No text could be found in that file.' }),
    )
    render(<CvPanel cv={null} onChanged={vi.fn()} />)

    await userEvent.upload(screen.getByLabelText('Upload CV'), file())

    expect(await screen.findByRole('alert')).toHaveTextContent('No text could be found')
  })

  it('shows the stored CV and offers to replace or remove it', async () => {
    const remove = vi.spyOn(matchingApi, 'deleteCv').mockResolvedValue(undefined)
    vi.spyOn(window, 'confirm').mockReturnValue(true)
    const onChanged = vi.fn()
    render(<CvPanel cv={cv} onChanged={onChanged} />)

    expect(screen.getByText('my-cv.pdf')).toBeInTheDocument()
    expect(screen.getByLabelText('Replace CV')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }))

    expect(remove).toHaveBeenCalled()
    expect(onChanged).toHaveBeenCalledWith(null)
  })
})
