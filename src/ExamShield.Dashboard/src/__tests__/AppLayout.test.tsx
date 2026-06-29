import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, it, expect } from 'vitest'
import AppLayout from '../components/layout/AppLayout'

function renderLayout(children = <div>content</div>) {
  return render(
    <MemoryRouter>
      <AppLayout userName="Admin">{children}</AppLayout>
    </MemoryRouter>
  )
}

describe('AppLayout', () => {
  it('renders the sidebar', () => {
    renderLayout()
    expect(screen.getByRole('navigation', { name: /sidebar/i })).toBeInTheDocument()
  })

  it('renders the top navigation bar', () => {
    renderLayout()
    expect(screen.getByRole('banner')).toBeInTheDocument()
  })

  it('displays the user name in the top nav', () => {
    renderLayout()
    const matches = screen.getAllByText('Admin')
    expect(matches.length).toBeGreaterThanOrEqual(1)
  })

  it('renders sidebar navigation links', () => {
    renderLayout()
    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /audit/i })).toBeInTheDocument()
  })

  it('collapses the sidebar on toggle', async () => {
    renderLayout()
    const toggle = screen.getByRole('button', { name: /toggle sidebar/i })
    await userEvent.click(toggle)
    expect(screen.getByRole('navigation', { name: /sidebar/i })).toHaveClass('collapsed')
  })
})
