import { render, screen, fireEvent, act } from '@testing-library/react'
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { MemoryRouter } from 'react-router-dom'
import PipelineDemoPage from '../pages/PipelineDemoPage'

function renderPage() {
  return render(<MemoryRouter><PipelineDemoPage /></MemoryRouter>)
}

beforeEach(() => {
  vi.useFakeTimers()
})

afterEach(() => {
  vi.runOnlyPendingTimers()
  vi.useRealTimers()
})

describe('PipelineDemoPage — layout', () => {
  it('shows Pipeline Showcase heading', () => {
    renderPage()
    expect(screen.getByRole('heading', { name: /pipeline showcase/i })).toBeInTheDocument()
  })

  it('shows Run Simulation button initially', () => {
    renderPage()
    expect(screen.getByRole('button', { name: /run simulation/i })).toBeInTheDocument()
  })

  it('shows Reset button', () => {
    renderPage()
    expect(screen.getByRole('button', { name: /reset/i })).toBeInTheDocument()
  })

  it('shows Live Audit Log heading', () => {
    renderPage()
    expect(screen.getByText(/live audit log/i)).toBeInTheDocument()
  })
})

describe('PipelineDemoPage — stage list', () => {
  it('shows Capture stage', () => {
    renderPage()
    // "Capture" appears in both sidebar list and detail panel — verify at least one instance
    expect(screen.getAllByText('Capture').length).toBeGreaterThanOrEqual(1)
  })

  it('shows Hash stage', () => {
    renderPage()
    expect(screen.getByText('Hash')).toBeInTheDocument()
  })

  it('shows Encrypt stage', () => {
    renderPage()
    expect(screen.getByText('Encrypt')).toBeInTheDocument()
  })

  it('shows OCR stage', () => {
    renderPage()
    expect(screen.getByText('OCR')).toBeInTheDocument()
  })

  it('shows Publish stage', () => {
    renderPage()
    expect(screen.getByText('Publish')).toBeInTheDocument()
  })
})

describe('PipelineDemoPage — controls', () => {
  it('changes to Pause button after clicking Run Simulation', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /run simulation/i }))
    expect(screen.getByRole('button', { name: /pause/i })).toBeInTheDocument()
  })

  it('changes back to Resume after pausing', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /run simulation/i }))
    fireEvent.click(screen.getByRole('button', { name: /pause/i }))
    expect(screen.getByRole('button', { name: /resume/i })).toBeInTheDocument()
  })

  it('Reset returns to initial state', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /run simulation/i }))
    fireEvent.click(screen.getByRole('button', { name: /reset/i }))
    expect(screen.getByRole('button', { name: /run simulation/i })).toBeInTheDocument()
  })

  it('clicking a stage button makes it active', () => {
    renderPage()
    const stageButton = screen.getAllByRole('button').find(
      b => b.textContent?.includes('Hash')
    )
    expect(stageButton).toBeDefined()
    fireEvent.click(stageButton!)
    // After clicking, Hash appears in the detail heading (h2)
    const h2s = document.querySelectorAll('h2')
    const hashHeading = Array.from(h2s).find(el => el.textContent === 'Hash')
    expect(hashHeading).toBeTruthy()
  })

  it('clicking Simulate Tamper toggles tamper mode on', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /simulate tamper/i }))
    expect(screen.getByRole('button', { name: /tamper mode on/i })).toBeInTheDocument()
  })

  it('shows security alert when tamper mode is active and Hash stage fires', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /simulate tamper/i }))
    fireEvent.click(screen.getByRole('button', { name: /run simulation/i }))
    act(() => {
      // Advance past stage 0 (Capture, 1200ms) + stage 1 (Hash, 600ms) → tamper triggers
      vi.advanceTimersByTime(2000)
    })
    expect(screen.getByText(/hash mismatch detected/i)).toBeInTheDocument()
  })

  it('simulation returns to stopped state after all stages complete', () => {
    renderPage()
    fireEvent.click(screen.getByRole('button', { name: /run simulation/i }))
    act(() => {
      vi.advanceTimersByTime(15000) // total pipeline ~12300ms; advance past all 11 stages
    })
    expect(screen.getByRole('button', { name: /run simulation/i })).toBeInTheDocument()
  })
})
