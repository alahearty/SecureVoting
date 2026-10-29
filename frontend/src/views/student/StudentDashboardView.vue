<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowRight, CalendarDays, CheckCircle2, Megaphone, ShieldCheck, Vote } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { StudentDashboard } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import MetricCard from '@/components/MetricCard.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDateTime, formatPercent } from '@/utils/format'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const dashboard = ref<StudentDashboard | null>(null)
const loading = ref(true)
const error = ref('')

const openElections = computed(() =>
  (dashboard.value?.elections ?? []).filter((election) => election.status === 'Open')
)
const upcoming = computed(() =>
  (dashboard.value?.elections ?? [])
    .filter((election) => ['Scheduled', 'Open'].includes(election.status))
    .slice(0, 3)
)
const votedCount = computed(() => (dashboard.value?.elections ?? []).filter((e) => e.hasVoted).length)
const turnout = computed(() => {
  const elections = dashboard.value?.elections ?? []
  const eligible = elections.reduce((sum, election) => sum + election.eligibleVoters, 0)
  const ballots = elections.reduce((sum, election) => sum + election.acceptedBallots, 0)
  return eligible === 0 ? 0 : (ballots / eligible) * 100
})

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    dashboard.value = await api<StudentDashboard>('/dashboard/student')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Your dashboard could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-7">
    <PageHeader
      eyebrow="Student workspace"
      :title="`Good to see you, ${auth.displayName.split(' ')[0] ?? 'student'}.`"
      description="Review open elections, confirm your eligibility and access your published departmental results."
    >
      <template #actions>
        <AppButton variant="secondary" @click="load">Refresh</AppButton>
        <RouterLink to="/student/elections">
          <AppButton>View elections <ArrowRight class="h-4 w-4" /></AppButton>
        </RouterLink>
      </template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <LoadingState v-if="loading" />

    <template v-else-if="dashboard">
      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard label="Open now" :value="String(openElections.length)" detail="Elections accepting ballots" tone="brand" />
        <MetricCard label="Your ballots" :value="String(votedCount)" detail="Accepted in this workspace" />
        <MetricCard label="Your elections" :value="String(dashboard.elections.length)" detail="Eligible departmental races" />
        <MetricCard label="Turnout snapshot" :value="formatPercent(turnout)" detail="Aggregate across your elections" tone="gold" />
      </div>

      <div class="grid gap-5 lg:grid-cols-[1.5fr_1fr]">
        <section class="space-y-3">
          <div class="flex items-end justify-between">
            <div>
              <p class="eyebrow">Voting desk</p>
              <h2 class="mt-1 font-display text-2xl text-ink-950">Your elections</h2>
            </div>
            <RouterLink to="/student/elections" class="text-xs font-bold text-brand-700">View all</RouterLink>
          </div>

          <EmptyState
            v-if="upcoming.length === 0"
            title="No open or scheduled elections"
            description="When the electoral officer publishes a schedule, it will appear here."
            compact
          />

          <RouterLink
            v-for="election in upcoming"
            :key="election.id"
            :to="{ name: 'student-election', params: { id: election.id } }"
            class="surface block p-5 transition hover:-translate-y-0.5 hover:border-brand-300"
          >
            <div class="flex flex-wrap items-center gap-2">
              <StatusBadge :status="election.status" />
              <span class="text-xs text-ink-500">{{ election.academicSession }}</span>
              <span v-if="election.hasVoted" class="ml-auto inline-flex items-center gap-1 text-xs font-bold text-emerald-700">
                <CheckCircle2 class="h-4 w-4" /> Ballot recorded
              </span>
            </div>
            <h3 class="mt-3 font-display text-xl text-ink-950">{{ election.title }}</h3>
            <div class="mt-3 grid gap-2 text-xs text-ink-500 sm:grid-cols-2">
              <p class="flex items-center gap-2"><CalendarDays class="h-4 w-4" /> {{ formatDateTime(election.opensAt) }}</p>
              <p>Closes {{ formatDateTime(election.closesAt) }}</p>
            </div>
            <div class="mt-4 flex items-center justify-between border-t border-ink-500/10 pt-3 text-xs">
              <span>{{ election.positionCount }} positions · {{ election.approvedCandidates }} approved candidates</span>
              <span class="font-bold text-brand-700">{{ election.hasVoted ? 'View status' : 'Review election' }}</span>
            </div>
          </RouterLink>

          <div class="rounded-2xl border border-brand-200 bg-brand-50/60 p-4 text-sm text-brand-900">
            <p class="flex items-center gap-2 font-semibold"><ShieldCheck class="h-4 w-4" /> One accepted ballot per election</p>
            <p class="mt-1 leading-6">
              Your eligibility is checked server-side. A ballot cannot be changed after acceptance, and your identity is
              never stored alongside your selections.
            </p>
          </div>
        </section>

        <aside class="space-y-5">
          <section class="surface p-5">
            <div class="flex items-center gap-2">
              <Megaphone class="h-4 w-4 text-gold-600" />
              <p class="eyebrow !text-gold-700">Notice board</p>
            </div>
            <div v-if="dashboard.announcements.length === 0" class="mt-4 text-sm text-ink-500">
              No published announcements yet.
            </div>
            <div v-else class="mt-4 space-y-4">
              <article v-for="announcement in dashboard.announcements.slice(0, 4)" :key="announcement.id">
                <p class="text-xs font-bold text-brand-700">{{ formatDateTime(announcement.publishedAt) }}</p>
                <h3 class="mt-1 text-sm font-semibold text-ink-950">{{ announcement.title }}</h3>
                <p class="mt-1 text-xs leading-5 text-ink-500">{{ announcement.body }}</p>
              </article>
            </div>
          </section>

          <section class="surface p-5">
            <div class="flex items-center gap-2">
              <Vote class="h-4 w-4 text-brand-700" />
              <p class="eyebrow">Voting checklist</p>
            </div>
            <ul class="mt-4 space-y-3 text-sm text-ink-700">
              <li class="flex gap-2"><span class="text-brand-600">01</span> Confirm your eligible election.</li>
              <li class="flex gap-2"><span class="text-brand-600">02</span> Read each manifesto carefully.</li>
              <li class="flex gap-2"><span class="text-brand-600">03</span> Review every position before submission.</li>
              <li class="flex gap-2"><span class="text-brand-600">04</span> Store your non-revealing confirmation reference.</li>
            </ul>
            <RouterLink to="/student/results" class="mt-5 inline-flex text-xs font-bold text-brand-700">
              View published results
            </RouterLink>
          </section>
        </aside>
      </div>
    </template>
  </div>
</template>
