<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowRight, CalendarDays, CheckCircle2, Search, Users } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { ElectionSummary } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDateTime } from '@/utils/format'

const elections = ref<ElectionSummary[]>([])
const loading = ref(true)
const error = ref('')
const query = ref('')
const filter = ref<'all' | 'open' | 'upcoming' | 'closed'>('all')

const filtered = computed(() => {
  const term = query.value.trim().toLowerCase()
  return elections.value.filter((election) => {
    if (term && !`${election.title} ${election.academicSession}`.toLowerCase().includes(term)) {
      return false
    }
    if (filter.value === 'open') return election.status === 'Open'
    if (filter.value === 'upcoming') return ['Scheduled', 'Draft'].includes(election.status)
    if (filter.value === 'closed') return ['Closed', 'UnderReview', 'Published'].includes(election.status)
    return true
  })
})

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    elections.value = await api<ElectionSummary[]>('/student/elections')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Elections could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="Ballot access"
      title="Your elections"
      description="These are the elections for which your student record is eligible. Eligibility is verified on the server."
    >
      <template #actions>
        <AppButton variant="secondary" @click="load">Refresh</AppButton>
      </template>
    </PageHeader>

    <div class="surface flex flex-col gap-3 p-4 sm:flex-row sm:items-center">
      <label class="relative flex-1">
        <span class="sr-only">Search elections</span>
        <Search class="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-ink-500" />
        <input v-model="query" class="field pl-9" placeholder="Search by election or academic session" />
      </label>
      <div class="flex gap-1 overflow-x-auto rounded-xl bg-ink-950/5 p-1" role="tablist" aria-label="Election filters">
        <button
          v-for="option in ['all', 'open', 'upcoming', 'closed'] as const"
          :key="option"
          type="button"
          class="focus-ring rounded-lg px-3 py-2 text-xs font-bold capitalize whitespace-nowrap"
          :class="filter === option ? 'bg-white text-brand-800 shadow-sm' : 'text-ink-500'"
          @click="filter = option"
        >
          {{ option }}
        </button>
      </div>
    </div>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <LoadingState v-if="loading" :rows="4" />

    <EmptyState
      v-else-if="filtered.length === 0"
      title="No matching elections"
      description="Try a different filter or check back after the electoral office publishes a schedule."
    />

    <div v-else class="grid gap-4 md:grid-cols-2">
      <RouterLink
        v-for="election in filtered"
        :key="election.id"
        :to="{ name: 'student-election', params: { id: election.id } }"
        class="surface group flex flex-col p-5 transition hover:-translate-y-0.5 hover:border-brand-300"
      >
        <div class="flex flex-wrap items-center gap-2">
          <StatusBadge :status="election.status" />
          <span class="text-xs text-ink-500">{{ election.department }} · {{ election.academicSession }}</span>
          <CheckCircle2 v-if="election.hasVoted" class="ml-auto h-4 w-4 text-emerald-700" aria-label="Ballot recorded" />
        </div>
        <h2 class="mt-3 font-display text-2xl leading-tight text-ink-950">{{ election.title }}</h2>
        <p class="mt-2 line-clamp-2 text-sm leading-6 text-ink-500">{{ election.description }}</p>
        <div class="mt-4 grid gap-2 text-xs text-ink-500">
          <p class="flex items-center gap-2"><CalendarDays class="h-4 w-4" /> Opens {{ formatDateTime(election.opensAt) }}</p>
          <p>Closes {{ formatDateTime(election.closesAt) }}</p>
          <p class="flex items-center gap-2"><Users class="h-4 w-4" /> {{ election.eligibleVoters }} eligible voters</p>
        </div>
        <div class="mt-auto flex items-center justify-between border-t border-ink-500/10 pt-4 text-xs">
          <span class="font-semibold text-ink-700">{{ election.positionCount }} positions</span>
          <span class="inline-flex items-center gap-1 font-bold text-brand-700">
            {{ election.hasVoted ? 'View ballot status' : 'Open election' }}
            <ArrowRight class="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
          </span>
        </div>
      </RouterLink>
    </div>
  </div>
</template>
