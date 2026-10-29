<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { ArrowRight, BadgeCheck, ScrollText, ShieldCheck } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { ObserverDashboard } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import BarChart from '@/components/BarChart.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import MetricCard from '@/components/MetricCard.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDateTime, formatPercent } from '@/utils/format'

const dashboard = ref<ObserverDashboard | null>(null)
const loading = ref(true)
const error = ref('')

const reviewQueue = computed(() =>
  (dashboard.value?.elections ?? []).filter((election) =>
    ['Closed', 'UnderReview'].includes(election.status)
  )
)

async function load(): Promise<void> {
  loading.value = true
  try {
    dashboard.value = await api<ObserverDashboard>('/dashboard/observer')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Observer dashboard could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-7">
    <PageHeader
      eyebrow="Independent observer"
      title="Election assurance"
      description="Review aggregate evidence, verify result snapshots and inspect the tamper-evident audit trail."
    >
      <template #actions>
        <AppButton variant="secondary" @click="load">Refresh</AppButton>
        <RouterLink to="/observer/elections">
          <AppButton>Review results <ArrowRight class="h-4 w-4" /></AppButton>
        </RouterLink>
      </template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="4" />
    <template v-else-if="dashboard">
      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard
          v-for="metric in dashboard.metrics"
          :key="metric.label"
          :label="metric.label"
          :value="metric.value"
          :detail="metric.detail"
          :tone="metric.label === 'Awaiting verification' ? 'gold' : 'brand'"
        />
      </div>

      <div class="grid gap-5 lg:grid-cols-[1.2fr_1fr]">
        <section class="surface p-5 sm:p-6">
          <div class="flex items-center justify-between">
            <div>
              <p class="eyebrow">Review queue</p>
              <h2 class="mt-1 font-display text-2xl">Awaiting verification</h2>
            </div>
            <BadgeCheck class="h-5 w-5 text-brand-700" />
          </div>
          <EmptyState
            v-if="reviewQueue.length === 0"
            title="Queue is clear"
            description="No closed election is currently awaiting observer action."
            compact
          />
          <div v-else class="mt-4 space-y-3">
            <RouterLink
              v-for="election in reviewQueue"
              :key="election.id"
              :to="{ name: 'observer-result', params: { id: election.id } }"
              class="flex items-center justify-between gap-3 rounded-xl border border-ink-500/10 p-4 transition hover:border-brand-300 hover:bg-brand-50/30"
            >
              <div>
                <p class="text-sm font-semibold text-ink-950">{{ election.title }}</p>
                <p class="mt-1 text-xs text-ink-500">
                  {{ election.department }} · {{ election.acceptedBallots }} accepted · closes {{ formatDateTime(election.closesAt) }}
                </p>
              </div>
              <StatusBadge :status="election.status" />
            </RouterLink>
          </div>
        </section>

        <section class="surface p-5 sm:p-6">
          <p class="eyebrow">Turnout view</p>
          <h2 class="mt-1 font-display text-2xl">Aggregate participation</h2>
          <div class="mt-4">
            <BarChart :series="dashboard.turnoutByElection" suffix="%" />
          </div>
        </section>
      </div>

      <div class="grid gap-5 lg:grid-cols-2">
        <section class="surface p-5 sm:p-6">
          <div class="flex items-center justify-between">
            <div>
              <p class="eyebrow">Audit evidence</p>
              <h2 class="mt-1 font-display text-2xl">Recent events</h2>
            </div>
            <ScrollText class="h-5 w-5 text-brand-700" />
          </div>
          <div class="mt-4 divide-y divide-ink-500/10">
            <div v-for="event in dashboard.recentAudit" :key="event.eventId" class="py-3">
              <div class="flex items-center justify-between gap-2">
                <p class="text-sm font-semibold text-ink-950">{{ event.eventType }}</p>
                <span class="text-[10px] font-bold uppercase text-ink-500">{{ event.outcome }}</span>
              </div>
              <p class="mt-1 text-xs text-ink-500">{{ event.actorDisplay }} · {{ formatDateTime(event.occurredAt) }}</p>
            </div>
            <p v-if="dashboard.recentAudit.length === 0" class="py-3 text-sm text-ink-500">No audit events yet.</p>
          </div>
          <RouterLink to="/observer/audit" class="mt-4 inline-flex text-xs font-bold text-brand-700">
            Open audit trail
          </RouterLink>
        </section>

        <section class="rounded-2xl bg-brand-950 p-6 text-white">
          <ShieldCheck class="h-6 w-6 text-gold-300" />
          <h2 class="mt-4 font-display text-3xl">Independent review is a control, not a formality.</h2>
          <p class="mt-3 text-sm leading-6 text-brand-100">
            You can inspect election configuration and aggregate results. You cannot view individual ballots, change
            votes or administer the election.
          </p>
          <p class="mt-5 text-xs font-semibold text-gold-200">
            Overall turnout: {{ formatPercent(dashboard.elections.reduce((sum, e) => sum + (e.eligibleVoters ? e.acceptedBallots * 100 / e.eligibleVoters : 0), 0) / Math.max(1, dashboard.elections.length)) }}
          </p>
        </section>
      </div>
    </template>
  </div>
</template>
