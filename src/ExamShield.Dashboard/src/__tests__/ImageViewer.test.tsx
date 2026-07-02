import { render, screen, fireEvent } from '@testing-library/react'
import { describe, it, expect } from 'vitest'
import ImageViewer from '../components/ImageViewer'

const SRC = '/captures/test-id/image'

describe('ImageViewer', () => {
  it('renders the image', () => {
    render(<ImageViewer src={SRC} alt="Test sheet" />)
    expect(screen.getByAltText('Test sheet')).toBeInTheDocument()
  })

  it('shows zoom-in and zoom-out buttons', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    expect(screen.getByTitle(/zoom in/i)).toBeInTheDocument()
    expect(screen.getByTitle(/zoom out/i)).toBeInTheDocument()
  })

  it('shows rotate button', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    expect(screen.getByTitle(/rotate/i)).toBeInTheDocument()
  })

  it('shows reset button', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    expect(screen.getByTitle(/reset/i)).toBeInTheDocument()
  })

  it('shows brightness slider', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    expect(screen.getByLabelText(/brightness/i)).toBeInTheDocument()
  })

  it('shows contrast slider', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    expect(screen.getByLabelText(/contrast/i)).toBeInTheDocument()
  })

  it('zoom-in increases scale', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    const img = screen.getByAltText('Test') as HTMLImageElement
    const initial = img.style.transform
    fireEvent.click(screen.getByTitle(/zoom in/i))
    expect(img.style.transform).not.toBe(initial)
  })

  it('rotate changes rotation', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    const img = screen.getByAltText('Test') as HTMLImageElement
    const initial = img.style.transform
    fireEvent.click(screen.getByTitle(/rotate/i))
    expect(img.style.transform).not.toBe(initial)
  })

  it('reset restores default state', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    const img = screen.getByAltText('Test') as HTMLImageElement
    fireEvent.click(screen.getByTitle(/zoom in/i))
    fireEvent.click(screen.getByTitle(/reset/i))
    expect(img.style.transform).toContain('scale(1)')
    expect(img.style.transform).toContain('rotate(0deg)')
  })

  it('brightness slider onChange updates the displayed percentage', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    const slider = screen.getByLabelText(/brightness/i)
    fireEvent.change(slider, { target: { value: '130' } })
    expect(screen.getByText('130%')).toBeInTheDocument()
  })

  it('contrast slider onChange updates the displayed percentage', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    const slider = screen.getByLabelText(/contrast/i)
    fireEvent.change(slider, { target: { value: '170' } })
    expect(screen.getByText('170%')).toBeInTheDocument()
  })

  it('zoom-out decreases scale', () => {
    render(<ImageViewer src={SRC} alt="Test" />)
    const img = screen.getByAltText('Test') as HTMLImageElement
    fireEvent.click(screen.getByTitle(/zoom in/i))
    const afterZoomIn = img.style.transform
    fireEvent.click(screen.getByTitle(/zoom out/i))
    expect(img.style.transform).not.toBe(afterZoomIn)
  })
})
