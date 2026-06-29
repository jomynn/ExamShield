import { render, screen } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import StatusChip from '../components/ui/StatusChip'

describe('StatusChip', () => {
  it('renders label text', () => {
    render(<StatusChip label="Active" variant="success" />)
    expect(screen.getByText('Active')).toBeInTheDocument()
  })

  it('applies green class for success variant', () => {
    const { container } = render(<StatusChip label="Active" variant="success" />)
    expect(container.firstChild).toHaveClass('text-green-400')
  })

  it('applies red class for danger variant', () => {
    const { container } = render(<StatusChip label="Disabled" variant="danger" />)
    expect(container.firstChild).toHaveClass('text-red-400')
  })

  it('applies yellow class for warning variant', () => {
    const { container } = render(<StatusChip label="Pending" variant="warning" />)
    expect(container.firstChild).toHaveClass('text-yellow-400')
  })

  it('applies blue class for info variant', () => {
    const { container } = render(<StatusChip label="Syncing" variant="info" />)
    expect(container.firstChild).toHaveClass('text-blue-400')
  })
})
