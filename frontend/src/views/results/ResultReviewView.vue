<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowRight, BadgeCheck, Search } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { ElectionSummary } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useAuthStore } from '@/stores/auth'
import { formatDateTime } from '@/utils/format'

const auth = useAuthStore()
const elections = ref<ElectionSummary[]>([])
const loading = ref(true)
const error = ref('')
const query = ref('')

const detailRoute = computed(() =>
  auth.hasRole('Observer') ? 'observer-result' : 'admin-result-detail'
)

const filtered = computed(() =>
  elections.value
    .filter((election) =>
      ['Closed', 'UnderReview', 'Published'].includes(election.status)
    )
    .filter((election) =>
      `${election.title} ${election.academicSession}`.toLowerCase().includes(query.value.toLowerCase())
    )
)

async function load(): Promise<void> {
  loading.value = true
  try {
    elections.value = await api<ElectionSummary[]>('/result-review/elections')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Result review data could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="Verification workflow"
      title="Result review"
      description="Closed elections enter result review. An independent observer verifies the snapshot, then the Super Administrator publishes it."
    >
      <template #actions>
        <AppButton variant="secondary" @click="load">Refresh</AppButton>
      </template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <div class="surface flex items-center gap-2 p-4">
      <Search class="h-4 w-4 text-ink-500" />
      <input v-model="query" class="w-full bg-transparent text-sm outline-none" placeholder="Search result snapshots" />
    </div>

    <LoadingState v-if="loading" :rows="4" />
    <EmptyState
      v-else-if="filtered.length === 0"
      title="No elections in result review"
      description="Close an election to create a provisional snapshot and begin verification."
    />

    <div v-else class="surface overflow-hidden">
      <div class="hidden grid-cols-[1.6fr_0.8fr_0.7fr_0.7fr_0.5fr] gap-4 border-b border-ink-500/10 px-5 py-3 text-[10px] font-bold tracking-wide text-ink-500 uppercase md:grid">
        <span>Election</span>
        <span>Status</span>
        <span>Eligible</span>
        <span>Accepted ballots</span>
        <span class="text-right">Review</span>
      </div>
      <div class="divide-y divide-ink-500/10">
        <RouterLink
          v-for="election in filtered"
          :key="election.id"
          :to="{ name: detailRoute, params: { id: election.id } }"
          class="group grid gap-3 px-5 py-4 transition hover:bg-brand-50/50 md:grid-cols-[1.6fr_0.8fr_0.7fr_0.7fr_0.5fr] md:items-center"
        >
          <div>
            <p class="font-semibold text-ink-950">{{ election.title }}</p>
            <p class="mt-1 text-xs text-ink-500">
              {{ election.department }} · {{ election.academicSession }} · closes {{ formatDateTime(election.closesAt) }}
            </p>
          </div>
          <StatusBadge :status="election.status" />
          <span class="text-sm text-ink-700">{{ election.eligibleVoters }}</span>
          <span class="text-sm text-ink-700">{{ election.acceptedBallots }}</span>
          <span class="inline-flex items-center gap-1 text-xs font-bold text-brand-700 md:justify-end">
            Open <ArrowRight class="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
          </span>
        </RouterLink>
      </div>
    </div>

    <div class="flex items-start gap-3 rounded-2xl border border-brand-200 bg-brand-50/60 p-4 text-xs leading-5 text-brand-900">
      <BadgeCheck class="mt-0.5 h-5 w-5 shrink-0" />
      <p>
        Result snapshots are aggregate-only. This workflow never provides individual ballot choices or voter-to-choice
        mappings.
      </p>
    </div>
  </div>
</template>
