<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowRight, ClipboardCheck } from '@lucide/vue'
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

const reviewable = computed(() =>
  elections.value.filter((election) => ['Closed', 'UnderReview', 'Published'].includes(election.status))
)

async function load(): Promise<void> {
  loading.value = true
  try {
    elections.value = await api<ElectionSummary[]>('/observer/elections')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Election review data could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="Independent review"
      title="Election review"
      description="Inspect aggregate configuration, turnout and result snapshots. Verification decisions are recorded in the audit trail."
    >
      <template #actions><AppButton variant="secondary" @click="load">Refresh</AppButton></template>
    </PageHeader>
    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="3" />
    <EmptyState
      v-else-if="reviewable.length === 0"
      title="No elections in review"
      description="Closed elections will appear here for independent aggregate review."
    />
    <div v-else class="grid gap-4 md:grid-cols-2">
      <RouterLink
        v-for="election in reviewable"
        :key="election.id"
        :to="{ name: 'observer-result', params: { id: election.id } }"
        class="surface group p-5 transition hover:-translate-y-0.5 hover:border-brand-300"
      >
        <div class="flex flex-wrap items-center gap-2">
          <StatusBadge :status="election.status" />
          <span class="text-xs text-ink-500">{{ election.department }} · {{ election.academicSession }}</span>
        </div>
        <h2 class="mt-3 font-display text-2xl leading-tight text-ink-950">{{ election.title }}</h2>
        <div class="mt-4 grid grid-cols-2 gap-2 text-xs text-ink-500">
          <p>{{ election.eligibleVoters }} eligible</p>
          <p>{{ election.acceptedBallots }} accepted ballots</p>
          <p class="col-span-2">Closes {{ formatDateTime(election.closesAt) }}</p>
        </div>
        <div class="mt-4 flex items-center justify-between border-t border-ink-500/10 pt-3 text-xs">
          <span class="inline-flex items-center gap-2"><ClipboardCheck class="h-4 w-4" /> Aggregate review only</span>
          <ArrowRight class="h-4 w-4 text-brand-700 transition group-hover:translate-x-0.5" />
        </div>
      </RouterLink>
    </div>
  </div>
</template>
