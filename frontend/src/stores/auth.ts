import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { api, clearCsrf, fetchCsrf } from '@/api/client'
import type { Me, Meta, Role } from '@/api/types'

export const useAuthStore = defineStore('auth', () => {
  const meta = ref<Meta | null>(null)
  const user = ref<Me | null>(null)
  const bootstrapped = ref(false)
  const bootstrapping = ref(false)
  const busy = ref(false)
  let bootstrapPromise: Promise<void> | null = null

  const isAuthenticated = computed(() => user.value !== null)
  const roles = computed(() => user.value?.roles ?? [])
  const isDemo = computed(() => meta.value?.demoMode ?? false)
  const displayName = computed(() => user.value?.displayName ?? '')
  const homePath = computed(() => {
    if (!user.value) return '/login'
    if (user.value.roles.includes('SuperAdministrator')) return '/admin'
    if (user.value.roles.includes('ElectoralOfficer')) return '/admin'
    if (user.value.roles.includes('Observer')) return '/observer'
    if (user.value.roles.includes('Candidate')) return '/candidate'
    if (user.value.roles.includes('Student')) return '/student'
    return '/login'
  })

  function hasRole(...expected: Role[]): boolean {
    return expected.some((role) => roles.value.includes(role))
  }

  function canAccessCandidatePortal(): boolean {
    return hasRole('Student', 'Candidate') || user.value?.hasCandidateProfile === true
  }

  async function bootstrap(): Promise<void> {
    if (bootstrapped.value) return
    if (bootstrapPromise) return bootstrapPromise
    bootstrapping.value = true
    bootstrapPromise = (async () => {
      const metaTask = api<Meta>('/meta').then((value) => {
        meta.value = value
      })
      const meTask = api<Me>('/auth/me')
        .then((value) => {
          user.value = value
        })
        .catch(() => {
          user.value = null
        })
      await Promise.allSettled([metaTask, meTask, fetchCsrf()])
      bootstrapped.value = true
      bootstrapping.value = false
    })()

    try {
      await bootstrapPromise
    } finally {
      bootstrapPromise = null
    }
  }

  async function login(identifier: string, password: string): Promise<Me> {
    busy.value = true
    try {
      const me = await api<Me>('/auth/login', {
        method: 'POST',
        body: { identifier, password }
      })
      user.value = me
      await fetchCsrf(true)
      if (!meta.value) {
        meta.value = await api<Meta>('/meta')
      }
      bootstrapped.value = true
      return me
    } finally {
      busy.value = false
    }
  }

  async function activate(
    matricNumber: string,
    token: string,
    password: string,
    confirmPassword: string
  ): Promise<Me> {
    busy.value = true
    try {
      const me = await api<Me>('/auth/activate', {
        method: 'POST',
        body: { matricNumber, token, password, confirmPassword }
      })
      user.value = me
      await fetchCsrf(true)
      if (!meta.value) {
        meta.value = await api<Meta>('/meta')
      }
      bootstrapped.value = true
      return me
    } finally {
      busy.value = false
    }
  }

  async function refresh(): Promise<void> {
    const me = await api<Me>('/auth/me')
    user.value = me
  }

  async function logout(): Promise<void> {
    try {
      await api<void>('/auth/logout', { method: 'POST' })
    } finally {
      clearSession()
      await fetchCsrf(true)
    }
  }

  function clearSession(): void {
    user.value = null
    clearCsrf()
  }

  return {
    meta,
    user,
    bootstrapped,
    bootstrapping,
    busy,
    isAuthenticated,
    roles,
    isDemo,
    displayName,
    homePath,
    hasRole,
    canAccessCandidatePortal,
    bootstrap,
    login,
    activate,
    refresh,
    logout,
    clearSession
  }
})
