import { renderHook, act } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'

// ── hoisted mock connection so it's accessible inside vi.mock() factory ───────

const mockConnection = vi.hoisted(() => ({
  on:    vi.fn(),
  start: vi.fn().mockResolvedValue(undefined),
  stop:  vi.fn().mockResolvedValue(undefined),
}))

vi.mock('@microsoft/signalr', () => ({
  HubConnectionBuilder: vi.fn(function () {
    return {
      withUrl:               vi.fn().mockReturnThis(),
      withAutomaticReconnect: vi.fn().mockReturnThis(),
      configureLogging:      vi.fn().mockReturnThis(),
      build:                 vi.fn(() => mockConnection),
    }
  }),
  LogLevel: { None: 0 },
}))

import { useNotifications } from '../hooks/useNotifications'
import type { RealtimeNotification } from '../hooks/useNotifications'
import * as signalR from '@microsoft/signalr'

// ── Helpers ───────────────────────────────────────────────────────────────────

const note = (overrides?: Partial<RealtimeNotification>): RealtimeNotification => ({
  type: 'HashMismatch', message: 'Cap tampered', severity: 'Critical',
  occurredAt: '2026-01-01T10:00:00Z', ...overrides,
})

function getNotifCallback() {
  const call = mockConnection.on.mock.calls.find(([evt]: [string]) => evt === 'Notification')
  return call?.[1] as ((n: RealtimeNotification) => void) | undefined
}

// ── Setup ─────────────────────────────────────────────────────────────────────

beforeEach(() => {
  localStorage.clear()
  vi.clearAllMocks()
  mockConnection.start.mockResolvedValue(undefined)
  mockConnection.stop.mockResolvedValue(undefined)
})

// ── No-token guard ────────────────────────────────────────────────────────────

describe('useNotifications — no auth token', () => {
  it('starts with empty notifications', () => {
    const { result } = renderHook(() => useNotifications())
    expect(result.current.notifications).toEqual([])
  })

  it('does not create a SignalR connection when no token', () => {
    renderHook(() => useNotifications())
    expect(signalR.HubConnectionBuilder).not.toHaveBeenCalled()
  })

  it('dismiss on empty array is safe', () => {
    const { result } = renderHook(() => useNotifications())
    expect(() => act(() => result.current.dismiss(0))).not.toThrow()
    expect(result.current.notifications).toEqual([])
  })

  it('clearAll on empty array is safe', () => {
    const { result } = renderHook(() => useNotifications())
    act(() => result.current.clearAll())
    expect(result.current.notifications).toEqual([])
  })
})

// ── With auth token ───────────────────────────────────────────────────────────

describe('useNotifications — with auth token', () => {
  beforeEach(() => {
    localStorage.setItem('auth_token', 'test-token-xyz')
  })

  it('creates a SignalR connection on mount', () => {
    renderHook(() => useNotifications())
    expect(signalR.HubConnectionBuilder).toHaveBeenCalledTimes(1)
  })

  it('calls connection.start()', () => {
    renderHook(() => useNotifications())
    expect(mockConnection.start).toHaveBeenCalledTimes(1)
  })

  it('registers a Notification event handler', () => {
    renderHook(() => useNotifications())
    expect(mockConnection.on).toHaveBeenCalledWith('Notification', expect.any(Function))
  })

  it('adds incoming notification to the list', () => {
    const { result } = renderHook(() => useNotifications())
    const cb = getNotifCallback()!
    act(() => cb(note({ message: 'Alert A' })))
    expect(result.current.notifications).toHaveLength(1)
    expect(result.current.notifications[0].message).toBe('Alert A')
  })

  it('prepends newer notifications (latest first)', () => {
    const { result } = renderHook(() => useNotifications())
    const cb = getNotifCallback()!
    act(() => cb(note({ message: 'First' })))
    act(() => cb(note({ message: 'Second' })))
    expect(result.current.notifications[0].message).toBe('Second')
    expect(result.current.notifications[1].message).toBe('First')
  })

  it('respects maxHistory — trims to the given limit', () => {
    const { result } = renderHook(() => useNotifications(3))
    const cb = getNotifCallback()!
    act(() => {
      cb(note({ message: 'A' }))
      cb(note({ message: 'B' }))
      cb(note({ message: 'C' }))
      cb(note({ message: 'D' }))
    })
    expect(result.current.notifications).toHaveLength(3)
    expect(result.current.notifications.map(n => n.message)).toEqual(['D', 'C', 'B'])
  })

  it('dismiss removes the notification at the specified index', () => {
    const { result } = renderHook(() => useNotifications())
    const cb = getNotifCallback()!
    act(() => {
      cb(note({ message: 'X' }))
      cb(note({ message: 'Y' }))
    })
    act(() => result.current.dismiss(0)) // removes 'Y' (index 0 = latest)
    expect(result.current.notifications).toHaveLength(1)
    expect(result.current.notifications[0].message).toBe('X')
  })

  it('dismiss with out-of-range index leaves list unchanged', () => {
    const { result } = renderHook(() => useNotifications())
    const cb = getNotifCallback()!
    act(() => cb(note({ message: 'Z' })))
    act(() => result.current.dismiss(99))
    expect(result.current.notifications).toHaveLength(1)
  })

  it('clearAll empties all notifications', () => {
    const { result } = renderHook(() => useNotifications())
    const cb = getNotifCallback()!
    act(() => {
      cb(note())
      cb(note())
    })
    act(() => result.current.clearAll())
    expect(result.current.notifications).toEqual([])
  })

  it('stops the connection on unmount', () => {
    const { unmount } = renderHook(() => useNotifications())
    unmount()
    expect(mockConnection.stop).toHaveBeenCalled()
  })

  it('swallows start() rejection without throwing', async () => {
    mockConnection.start.mockRejectedValue(new Error('SignalR connect failed'))
    await expect(
      new Promise<void>(resolve => {
        const { unmount } = renderHook(() => useNotifications())
        setTimeout(() => { unmount(); resolve() }, 10)
      })
    ).resolves.not.toThrow()
  })

  it('calls stop() again if start resolves after unmount (StrictMode guard)', async () => {
    let resolveStart!: () => void
    mockConnection.start.mockReturnValue(
      new Promise<void>(res => { resolveStart = res })
    )

    const { unmount } = renderHook(() => useNotifications())
    unmount() // sets active = false; stop() called once

    await act(async () => { resolveStart() }) // start resolves after unmount → stop() again

    expect(mockConnection.stop.mock.calls.length).toBeGreaterThanOrEqual(2)
  })

  it('does not push notifications after unmount (active guard)', () => {
    const { result, unmount } = renderHook(() => useNotifications())
    const cb = getNotifCallback()!
    unmount()
    act(() => cb(note({ message: 'ghost' })))
    expect(result.current.notifications).toEqual([])
  })
})
