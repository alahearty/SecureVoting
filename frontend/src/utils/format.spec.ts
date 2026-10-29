import { describe, expect, it } from 'vitest'
import { formatDate, formatNumber, formatPercent, initials, roleLabel, timeUntil } from './format'

describe('format helpers', () => {
  it('formats dates in Africa/Lagos without invalid fallbacks', () => {
    expect(formatDate('2026-10-28T12:00:00Z')).toContain('2026')
    expect(formatDate(null)).toBe('—')
    expect(formatDate('not-a-date')).toBe('—')
  })

  it('formats percentages and numbers', () => {
    expect(formatPercent(12.345)).toBe('12.3%')
    expect(formatPercent(100)).toBe('100.0%')
    expect(formatNumber(1200)).toContain('1')
  })

  it('derives initials and role labels', () => {
    expect(initials('Adaeze N. Okafor')).toBe('AN')
    expect(roleLabel('ElectoralOfficer')).toBe('Electoral Officer')
    expect(roleLabel('Observer')).toBe('Independent Observer')
  })

  it('describes the remaining voting time', () => {
    expect(timeUntil(new Date(Date.now() + 120000).toISOString())).toContain('min remaining')
    expect(timeUntil(new Date(Date.now() - 1000).toISOString())).toBe('closed')
  })
})
