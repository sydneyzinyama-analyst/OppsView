import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { makeApplication } from '../test/fixtures'
import { StatusSelect } from './StatusSelect'

describe('StatusSelect', () => {
  it('offers only the moves the server allows', () => {
    render(
      <StatusSelect
        application={makeApplication({ status: 'Interview', allowedNextStatuses: ['Offer', 'Rejected'] })}
        onChange={vi.fn()}
      />,
    )

    const options = screen.getAllByRole('option').map((o) => o.textContent)
    expect(options).toEqual(['Interview', '→ Offer', '→ Rejected'])
  })

  it('reports the chosen status', async () => {
    const onChange = vi.fn()
    render(<StatusSelect application={makeApplication()} onChange={onChange} />)

    await userEvent.selectOptions(screen.getByLabelText('Status for Acme'), 'Interview')

    expect(onChange).toHaveBeenCalledWith('Interview')
  })

  it('shows a final status as a plain badge', () => {
    render(
      <StatusSelect application={makeApplication({ status: 'Rejected', allowedNextStatuses: [] })} onChange={vi.fn()} />,
    )
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
    expect(screen.getByText('Rejected')).toBeInTheDocument()
  })
})
