<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowRight, BarChart3 } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { ElectionSummary } from '@/api/types'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDateTime } from '@/utils/format'

const elections = ref<ElectionSummary[]>([])
const loading = ref(true)
const error = ref('')

const published = computed(() => elections.value.filter((election) => election.status === 'Published'))

async function load(): Promise<void> {
  loading.value = true
  try {
    elections.value = await api<ElectionSummary[]>('/student/elections')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Published results could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="Official records"
      title="Published results"
      description="Only results approved by an independent observer and published by the Super Administrator appear here."
    />

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="3" />
    <EmptyState
      v-else-if="published.length === 0"
      title="No results published yet"
      description="Result snapshots remain under review until the required verification and publication approvals are complete."
    />
    <div v-else class="grid gap-4 md:grid-cols-2">
      <RouterLink
        v-for="election in published"
        :key="election.id"
        :to="{ name: 'published-result', params: { id: election.id } }"
        class="surface group p-5 transition hover:-translate-y-0.5 hover:border-brand-300"
      >
        <div class="flex items-center gap-2">
          <StatusBadge :status="election.status" />
          <span class="text-xs text-ink-500">{{ election.department }} · {{ election.academicSession }}</span>
        </div>
        <h2 class="mt-3 font-display text-2xl leading-tight text-ink-950">{{ election.title }}</h2>
        <div class="mt-4 flex items-center justify-between border-t border-ink-500/10 pt-4 text-xs">
          <span class="flex items-center gap-2 text-ink-500">
            <BarChart3 class="h-4 w-4" />
            {{ election.acceptedBallots }} accepted ballots
          </span>
          <span class="inline-flex items-center gap-1 font-bold text-brand-700">
            Read result <ArrowRight class="h-3.5 w-3.5 transition group-hover:translate-x-0.5" />
          </span>
        </div>
        <p v-if="election.publishedAt" class="mt-2 text-[11px] text-ink-500">
          Published {{ formatDateTime(election.publishedAt) }}
        </p>
      </RouterLink>
    </div>
  </div>
</template>
