<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowRight, CalendarPlus, Search } from '@lucide/vue'
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
const statusFilter = ref('all')
const departmentFilter = ref('all')

const filters = ['all', 'Draft', 'Scheduled', 'Open', 'Closed', 'UnderReview', 'Published']
const departments = computed(() => [...new Set(elections.value.map((election) => election.department))].sort())
const filtered = computed(() =>
  elections.value.filter((election) => {
    const matchesQuery = `${election.title} ${election.academicSession} ${election.department} ${election.departmentCode}`
      .toLowerCase()
      .includes(query.value.toLowerCase())
    return matchesQuery &&
      (statusFilter.value === 'all' || election.status === statusFilter.value) &&
      (departmentFilter.value === 'all' || election.department === departmentFilter.value)
  })
)

async function load(): Promise<void> {
  loading.value = true
  try {
    elections.value = await api<ElectionSummary[]>('/admin/elections')
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
      eyebrow="Electoral officer"
      title="Election administration"
      description="Prepare the register, approve candidates, open the voting window and move results through review."
    >
      <template #actions>
        <AppButton variant="secondary" @click="load">Refresh</AppButton>
        <RouterLink to="/admin/elections/new">
          <AppButton><CalendarPlus class="h-4 w-4" /> Create election</AppButton>
        </RouterLink>
      </template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <div class="surface flex flex-col gap-3 p-4 sm:flex-row">
      <label class="relative flex-1">
        <span class="sr-only">Search elections</span>
        <Search class="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-ink-500" />
        <input v-model="query" class="field pl-9" placeholder="Search title or academic session" />
      </label>
      <select v-model="departmentFilter" class="field sm:w-52" aria-label="Filter by department">
        <option value="all">All departments</option>
        <option v-for="department in departments" :key="department" :value="department">{{ department }}</option>
      </select>
      <select v-model="statusFilter" class="field sm:w-52" aria-label="Filter by election status">
        <option v-for="status in filters" :key="status" :value="status">
          {{ status === 'all' ? 'All statuses' : status.replace(/([a-z])([A-Z])/g, '$1 $2') }}
        </option>
      </select>
    </div>

    <LoadingState v-if="loading" :rows="4" />
    <EmptyState
      v-else-if="filtered.length === 0"
      title="No elections match"
      description="Create a draft election or change your filters."
    >
      <RouterLink to="/admin/elections/new" class="mt-4">
        <AppButton variant="secondary">Create election</AppButton>
      </RouterLink>
    </EmptyState>

    <div v-else class="grid gap-4 lg:grid-cols-2">
      <RouterLink
        v-for="election in filtered"
        :key="election.id"
        :to="{ name: 'admin-election', params: { id: election.id } }"
        class="surface group p-5 transition hover:-translate-y-0.5 hover:border-brand-300"
      >
        <div class="flex flex-wrap items-center gap-2">
          <StatusBadge :status="election.status" />
          <span class="text-xs text-ink-500">{{ election.academicSession }}</span>
          <span class="ml-auto text-[11px] font-semibold text-ink-500">{{ election.department }} · {{ election.departmentCode }}</span>
        </div>
        <h2 class="mt-3 font-display text-2xl leading-tight text-ink-950">{{ election.title }}</h2>
        <p class="mt-2 line-clamp-2 text-sm leading-6 text-ink-500">{{ election.description }}</p>
        <div class="mt-4 grid grid-cols-2 gap-2 rounded-xl bg-ink-950/[0.025] p-3 text-xs text-ink-600">
          <p>{{ election.eligibleVoters }} eligible</p>
          <p>{{ election.acceptedBallots }} ballots</p>
          <p>{{ election.positionCount }} positions</p>
          <p>{{ election.approvedCandidates }} candidates</p>
        </div>
        <div class="mt-3 flex items-center justify-between text-xs text-ink-500">
          <span>{{ formatDateTime(election.opensAt) }} → {{ formatDateTime(election.closesAt) }}</span>
          <ArrowRight class="h-4 w-4 text-brand-700 transition group-hover:translate-x-0.5" />
        </div>
      </RouterLink>
    </div>
  </div>
</template>
