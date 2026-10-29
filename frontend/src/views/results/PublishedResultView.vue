<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { ArrowLeft } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { ElectionResult } from '@/api/types'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import ResultReport from '@/components/ResultReport.vue'
import { useAuthStore } from '@/stores/auth'

const route = useRoute()
const auth = useAuthStore()
const electionId = computed(() => String(route.params.id))
const result = ref<ElectionResult | null>(null)
const loading = ref(true)
const error = ref('')

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    result.value = await api<ElectionResult>(`/elections/${electionId.value}/results/published`)
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The published result could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-5">
    <RouterLink to="/student/results" class="no-print inline-flex items-center gap-2 text-xs font-bold text-brand-700">
      <ArrowLeft class="h-4 w-4" /> Published results
    </RouterLink>
    <PageHeader
      eyebrow="Official result"
      title="Election result"
      description="Aggregate result snapshot. This page intentionally excludes individual ballots and voter-to-choice mappings."
    />

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="4" />
    <ResultReport
      v-else-if="result"
      :result="result"
      :export-path="`/elections/${electionId}/results/published/export.csv`"
    />
    <div v-else class="surface p-8 text-center">
      <p class="font-display text-2xl text-ink-950">Result unavailable</p>
      <p class="mt-2 text-sm text-ink-500">This election may not have been officially published.</p>
      <RouterLink :to="auth.homePath" class="mt-4 inline-flex text-sm font-semibold text-brand-700">Return to dashboard</RouterLink>
    </div>
  </div>
</template>
