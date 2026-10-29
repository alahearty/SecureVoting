<script setup lang="ts">
import { computed, ref, type Component } from 'vue'
import { RouterLink, RouterView, useRoute, useRouter } from 'vue-router'
import {
  BarChart3,
  BadgeCheck,
  Building2,
  ClipboardCheck,
  LayoutDashboard,
  Landmark,
  LogOut,
  Megaphone,
  Menu,
  ScrollText,
  Settings,
  UserRound,
  Users,
  Vote,
  X
} from '@lucide/vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirmStore } from '@/stores/confirm'
import { useToastStore } from '@/stores/toasts'
import AppButton from './AppButton.vue'

interface NavItem {
  label: string
  to: string
  icon: Component
  exact?: boolean
}

const auth = useAuthStore()
const confirm = useConfirmStore()
const toasts = useToastStore()
const route = useRoute()
const router = useRouter()
const mobileOpen = ref(false)

const navigation = computed<NavItem[]>(() => {
  const items: NavItem[] = []
  if (auth.hasRole('Student', 'Candidate')) {
    items.push(
      { label: 'Student home', to: '/student', icon: LayoutDashboard, exact: true },
      { label: 'Elections', to: '/student/elections', icon: Vote },
      { label: 'Published results', to: '/student/results', icon: BarChart3 }
    )
  }

  if (auth.canAccessCandidatePortal()) {
    items.push({ label: 'Candidate portal', to: '/candidate', icon: UserRound })
  }

  if (auth.hasRole('ElectoralOfficer')) {
    items.push(
      { label: 'Admin overview', to: '/admin', icon: LayoutDashboard, exact: true },
      { label: 'Elections', to: '/admin/elections', icon: Vote },
      { label: 'Result review', to: '/admin/results', icon: BadgeCheck }
    )
  }

  if (auth.hasRole('SuperAdministrator')) {
    items.push(
      { label: 'System overview', to: '/admin', icon: LayoutDashboard, exact: true },
      { label: 'Result publication', to: '/admin/results', icon: BadgeCheck },
      { label: 'Elections', to: '/admin/elections', icon: Vote },
      { label: 'Departments', to: '/admin/departments', icon: Building2 },
      { label: 'User accounts', to: '/admin/users', icon: Users },
      { label: 'Settings', to: '/admin/settings', icon: Settings },
      { label: 'Audit trail', to: '/admin/audit', icon: ScrollText }
    )
  }

  if (auth.hasRole('Observer')) {
    items.push(
      { label: 'Observer overview', to: '/observer', icon: LayoutDashboard, exact: true },
      { label: 'Election review', to: '/observer/elections', icon: ClipboardCheck },
      { label: 'Audit trail', to: '/observer/audit', icon: ScrollText }
    )
  }

  return items
})

const initials = computed(() =>
  auth.displayName
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
)

const primaryRole = computed(() => {
  if (auth.hasRole('SuperAdministrator')) return 'Super Administrator'
  if (auth.hasRole('ElectoralOfficer')) return 'Electoral Officer'
  if (auth.hasRole('Observer')) return 'Independent Observer'
  if (auth.hasRole('Candidate')) return 'Candidate'
  return 'Student'
})

function isActive(item: NavItem): boolean {
  return item.exact ? route.path === item.to : route.path.startsWith(item.to)
}

async function signOut(): Promise<void> {
  const confirmed = await confirm.ask({
    title: 'Sign out of SecureVote?',
    message: 'Your secure session will end on this device.',
    confirmLabel: 'Sign out',
    tone: 'danger'
  })
  if (!confirmed) return
  await auth.logout()
  toasts.info('You have signed out.')
  await router.push({ name: 'login' })
}

function closeMobile(): void {
  mobileOpen.value = false
}
</script>

<template>
  <div class="min-h-screen lg:flex">
    <header class="sticky top-0 z-40 border-b border-white/10 bg-brand-900 text-white lg:hidden">
      <div class="flex items-center justify-between px-4 py-3">
        <RouterLink to="/" class="flex items-center gap-2" @click="closeMobile">
          <span class="grid h-9 w-9 place-items-center rounded-xl bg-gold-400 text-ink-950">
            <Landmark class="h-5 w-5" />
          </span>
          <span class="font-display text-lg">SecureVote</span>
        </RouterLink>
        <button
          type="button"
          class="focus-ring rounded-xl p-2 hover:bg-white/10"
          :aria-expanded="mobileOpen"
          aria-label="Open navigation"
          @click="mobileOpen = !mobileOpen"
        >
          <X v-if="mobileOpen" class="h-5 w-5" />
          <Menu v-else class="h-5 w-5" />
        </button>
      </div>
    </header>

    <aside
      class="fixed inset-y-0 left-0 z-50 flex w-[280px] -translate-x-full flex-col bg-brand-950 text-white transition-transform duration-200 lg:sticky lg:top-0 lg:z-30 lg:min-h-screen lg:translate-x-0"
      :class="mobileOpen ? 'translate-x-0' : ''"
      aria-label="Primary navigation"
    >
      <div class="flex items-center justify-between border-b border-white/10 px-5 py-5">
        <RouterLink to="/" class="flex items-center gap-3" @click="closeMobile">
          <span class="grid h-11 w-11 place-items-center rounded-2xl bg-gold-400 text-ink-950 shadow-lg">
            <Landmark class="h-6 w-6" />
          </span>
          <div>
            <p class="font-display text-xl leading-none">SecureVote</p>
            <p class="mt-1 text-[10px] font-semibold tracking-[0.16em] text-brand-200 uppercase">
              University election platform
            </p>
          </div>
        </RouterLink>
        <button
          type="button"
          class="focus-ring rounded-lg p-1.5 text-brand-100 hover:bg-white/10 lg:hidden"
          aria-label="Close navigation"
          @click="closeMobile"
        >
          <X class="h-5 w-5" />
        </button>
      </div>

      <nav class="flex-1 space-y-1 overflow-y-auto px-3 py-5">
        <p class="px-3 pb-2 text-[10px] font-bold tracking-[0.18em] text-brand-300 uppercase">
          Workspace
        </p>
        <RouterLink
          v-for="item in navigation"
          :key="item.to"
          :to="item.to"
          class="focus-ring flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition"
          :class="isActive(item) ? 'bg-white text-brand-900 shadow-md' : 'text-brand-100 hover:bg-white/10'"
          @click="closeMobile"
        >
          <component :is="item.icon" class="h-[18px] w-[18px] shrink-0" aria-hidden="true" />
          {{ item.label }}
        </RouterLink>

        <div class="mt-6 rounded-2xl border border-gold-300/25 bg-white/5 p-3">
          <p class="flex items-center gap-2 text-xs font-bold text-gold-200">
            <Megaphone class="h-4 w-4" />
            Election integrity
          </p>
          <p class="mt-1 text-[11px] leading-5 text-brand-100">
            Ballots are anonymous aggregates. Your identity is never attached to your choices.
          </p>
        </div>
      </nav>

      <div class="border-t border-white/10 p-4">
        <div class="flex items-center gap-3 rounded-2xl bg-white/5 p-3">
          <span class="grid h-10 w-10 place-items-center rounded-full bg-gold-300 text-sm font-bold text-ink-950">
            {{ initials || 'SV' }}
          </span>
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-semibold">{{ auth.displayName }}</p>
            <p class="truncate text-[11px] text-brand-200">{{ primaryRole }}</p>
          </div>
        </div>
        <AppButton
          variant="ghost"
          size="sm"
          block
          class="mt-2 !justify-start !text-brand-100 hover:!bg-white/10"
          @click="signOut"
        >
          <LogOut class="h-4 w-4" />
          Sign out
        </AppButton>
        <p class="mt-2 px-2 text-[10px] text-brand-300">Version {{ auth.meta?.version ?? '1.0.0' }}</p>
      </div>
    </aside>

    <div
      v-if="mobileOpen"
      class="fixed inset-0 z-40 bg-ink-950/50 backdrop-blur-sm lg:hidden"
      aria-hidden="true"
      @click="closeMobile"
    />

    <div class="min-w-0 flex-1">
      <div
        v-if="auth.isDemo"
        class="border-b border-gold-300/70 bg-gold-100 px-4 py-2 text-center text-xs font-semibold text-gold-700"
      >
        DEMO WORKSPACE — synthetic students, candidates and ballots. Never use for a real election.
      </div>
      <main class="mx-auto w-full max-w-[1240px] px-4 py-6 sm:px-6 sm:py-8 lg:px-10">
        <RouterView />
      </main>
      <footer class="mx-auto flex w-full max-w-[1240px] flex-col gap-2 px-4 pb-8 text-[11px] leading-5 text-ink-500 sm:px-6 lg:px-10">
        <p>{{ auth.meta?.institution }} · University-wide departmental elections</p>
        <p>SecureVote records election evidence. It does not replace university electoral regulations or an independent audit.</p>
      </footer>
    </div>
  </div>
</template>
