<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { CheckCircle2, ChevronDown, RefreshCw, Search, ShieldAlert } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { AuditLog, AuditVerification } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDateTime } from '@/utils/format'

const logs = ref<AuditLog[]>([])
const verification = ref<AuditVerification | null>(null)
const loading = ref(true)
const verifying = ref(false)
const error = ref('')
const eventFilter = ref('')
const actorFilter = ref('')

async function load(): Promise<void> {
  loading.value = true
  try {
    const params = new URLSearchParams({ pageSize: '150' })
    if (eventFilter.value.trim()) params.set('eventType', eventFilter.value.trim())
    if (actorFilter.value.trim()) params.set('actor', actorFilter.value.trim())
    logs.value = await api<AuditLog[]>(`/audit?${params.toString()}`)
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Audit records could not be loaded.'
  } finally {
    loading.value = false
  }
}

async function verify(): Promise<void> {
  verifying.value = true
  error.value = ''
  try {
    verification.value = await api<AuditVerification>('/audit/verify', { method: 'POST' })
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Audit verification could not be completed.'
  } finally {
    verifying.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="Tamper evidence"
      title="Audit trail"
      description="Read-only events for access, configuration, election lifecycle, candidate decisions and result publication. Ballot choices are never recorded here."
    >
      <template #actions>
        <AppButton variant="secondary" :loading="loading" @click="load"><RefreshCw class="h-4 w-4" /> Refresh</AppButton>
        <AppButton :loading="verifying" @click="verify"><CheckCircle2 class="h-4 w-4" /> Verify chain</AppButton>
      </template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <div
      v-if="verification"
      class="rounded-2xl border p-4 text-sm"
      :class="verification.isValid ? 'border-emerald-200 bg-emerald-50 text-emerald-900' : 'border-red-200 bg-red-50 text-red-900'"
    >
      <div class="flex items-center gap-2 font-semibold">
        <CheckCircle2 v-if="verification.isValid" class="h-5 w-5" />
        <ShieldAlert v-else class="h-5 w-5" />
        {{ verification.isValid ? 'Audit chain verified' : 'Audit chain integrity failure' }}
      </div>
      <p class="mt-1">{{ verification.message }} · {{ verification.entriesChecked }} entries checked.</p>
      <p v-if="verification.brokenAtSequence" class="mt-1 font-mono text-xs">Broken at sequence {{ verification.brokenAtSequence }}</p>
    </div>

    <div class="surface flex flex-col gap-3 p-4 sm:flex-row">
      <label class="relative flex-1">
        <span class="sr-only">Filter event type</span>
        <Search class="pointer-events-none absolute top-1/2 left-3 h-4 w-4 -translate-y-1/2 text-ink-500" />
        <input v-model="eventFilter" class="field pl-9" placeholder="Filter event type" @keyup.enter="load" />
      </label>
      <input v-model="actorFilter" class="field sm:w-56" placeholder="Filter actor" @keyup.enter="load" />
      <AppButton variant="secondary" @click="load">Apply filters</AppButton>
    </div>

    <LoadingState v-if="loading" :rows="5" />
    <EmptyState v-else-if="logs.length === 0" title="No audit events" description="Events will appear as users and administrators act in the system." />
    <div v-else class="surface overflow-hidden">
      <div class="hidden grid-cols-[90px_1.2fr_1fr_0.7fr_0.8fr] gap-3 border-b border-ink-500/10 px-5 py-3 text-[10px] font-bold tracking-wide text-ink-500 uppercase lg:grid">
        <span>Sequence</span><span>Event</span><span>Actor</span><span>Outcome</span><span>Time</span>
      </div>
      <div class="divide-y divide-ink-500/10">
        <details v-for="entry in logs" :key="entry.eventId" class="group px-5 py-4">
          <summary class="grid cursor-pointer list-none gap-2 lg:grid-cols-[90px_1.2fr_1fr_0.7fr_0.8fr] lg:items-center">
            <span class="font-mono text-xs text-ink-500">#{{ entry.sequence }}</span>
            <span class="text-sm font-semibold text-ink-950">{{ entry.eventType }}</span>
            <span class="truncate text-xs text-ink-600">{{ entry.actorDisplay }}</span>
            <StatusBadge :status="entry.outcome" />
            <span class="text-xs text-ink-500">{{ formatDateTime(entry.occurredAt) }}</span>
            <ChevronDown class="ml-auto h-4 w-4 text-ink-500 lg:hidden" />
          </summary>
          <div class="mt-3 grid gap-3 rounded-xl bg-paper p-4 text-xs sm:grid-cols-2">
            <div><p class="font-bold text-ink-500">Entity</p><p class="mt-1 text-ink-800">{{ entry.entityType }} {{ entry.entityId }}</p></div>
            <div><p class="font-bold text-ink-500">Correlation</p><p class="mt-1 break-all text-ink-800">{{ entry.correlationId ?? '—' }}</p></div>
            <div class="sm:col-span-2"><p class="font-bold text-ink-500">Details</p><pre class="mt-1 overflow-x-auto whitespace-pre-wrap text-[11px] text-ink-700">{{ entry.detailsJson }}</pre></div>
            <div class="sm:col-span-2"><p class="font-bold text-ink-500">Hash</p><p class="mt-1 break-all font-mono text-[10px] text-ink-500">{{ entry.hash }}</p></div>
          </div>
        </details>
      </div>
    </div>
  </div>
</template>
