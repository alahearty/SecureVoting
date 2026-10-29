const lagos = 'Africa/Lagos'

export function formatDate(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: lagos,
    day: '2-digit',
    month: 'short',
    year: 'numeric'
  }).format(date)
}

export function formatDateTime(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: lagos,
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date)
}

export function formatShortDateTime(value: string | Date | null | undefined): string {
  if (!value) return '—'
  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: lagos,
    day: '2-digit',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit'
  }).format(date)
}

export function formatNumber(value: number): string {
  return new Intl.NumberFormat('en-NG').format(value)
}

export function formatPercent(value: number, digits = 1): string {
  return `${new Intl.NumberFormat('en-NG', {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits
  }).format(value)}%`
}

export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

export function isPast(value: string): boolean {
  return new Date(value).getTime() < Date.now()
}

export function timeUntil(value: string): string {
  const milliseconds = new Date(value).getTime() - Date.now()
  if (milliseconds <= 0) return 'closed'
  const minutes = Math.floor(milliseconds / 60000)
  if (minutes < 60) return `${minutes} min remaining`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} hr remaining`
  return `${Math.ceil(hours / 24)} days remaining`
}

export function toIsoLocal(value: string): string {
  const date = new Date(value)
  const offset = date.getTimezoneOffset()
  return new Date(date.getTime() - offset * 60000).toISOString().slice(0, 16)
}

export function roleLabel(role: string): string {
  const labels: Record<string, string> = {
    SuperAdministrator: 'Super Administrator',
    ElectoralOfficer: 'Electoral Officer',
    Student: 'Student',
    Candidate: 'Candidate',
    Observer: 'Independent Observer'
  }
  return labels[role] ?? role
}
