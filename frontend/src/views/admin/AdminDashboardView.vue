<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { CalendarPlus, Megaphone, ScrollText, Settings, Users } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { AdminDashboard } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import BarChart from '@/components/BarChart.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import MetricCard from '@/components/MetricCard.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDateTime } from '@/utils/format'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const dashboard = ref<AdminDashboard | null>(null)
const loading = ref(true)
const error = ref('')

const isOfficer = computed(() => auth.hasRole('ElectoralOfficer'))
const canManageElections = computed(() => auth.hasRole('ElectoralOfficer', 'SuperAdministrator'))

async function load(): Promise<void> {
  loading.value = true
  try {
    dashboard.value = await api<AdminDashboard>('/dashboard/admin')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Administrative dashboard could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-7">
    <PageHeader
      eyebrow="Administration"
      :title="isOfficer ? 'Electoral operations' : 'System command centre'"
      :description="isOfficer
        ? 'Prepare elections, manage the voter register and move results through the controlled verification workflow.'
        : 'Monitor system activity, manage access and publish results only after independent verification.'"
    >
      <template #actions>
        <AppButton variant="secondary" @click="load">Refresh</AppButton>
        <RouterLink v-if="canManageElections" to="/admin/elections/new">
          <AppButton><CalendarPlus class="h-4 w-4" /> New election</AppButton>
        </RouterLink>
        <RouterLink v-else to="/admin/users">
          <AppButton><Users class="h-4 w-4" /> Manage users</AppButton>
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
          v-for="(metric, index) in dashboard.metrics"
          :key="metric.label"
          :label="metric.label"
          :value="metric.value"
          :detail="metric.detail"
          :tone="index === 0 ? 'brand' : index === 2 ? 'gold' : 'neutral'"
        />
      </div>

      <div class="grid gap-5 lg:grid-cols-[1.25fr_1fr]">
        <section class="surface p-5 sm:p-6">
          <div class="flex items-end justify-between gap-3">
            <div>
              <p class="eyebrow">Operational board</p>
              <h2 class="mt-1 font-display text-2xl">Elections needing attention</h2>
            </div>
            <RouterLink v-if="canManageElections" to="/admin/elections" class="text-xs font-bold text-brand-700">View all</RouterLink>
          </div>
          <EmptyState
            v-if="dashboard.activeElections.length === 0"
            title="No active administrative work"
            description="Create an election or schedule an existing draft to begin."
            compact
          />
          <div v-else class="mt-4 space-y-3">
            <RouterLink
              v-for="election in dashboard.activeElections"
              :key="election.id"
              :to="canManageElections ? { name: 'admin-election', params: { id: election.id } } : { name: 'admin-result-detail', params: { id: election.id } }"
              class="flex items-center justify-between gap-3 rounded-xl border border-ink-500/10 p-4 transition hover:border-brand-300 hover:bg-brand-50/30"
            >
              <div class="min-w-0">
                <p class="truncate text-sm font-semibold text-ink-950">{{ election.title }}</p>
                <p class="mt-1 text-xs text-ink-500">
                  {{ election.acceptedBallots }} accepted · closes {{ formatDateTime(election.closesAt) }}
                </p>
              </div>
              <StatusBadge :status="election.status" />
            </RouterLink>
          </div>
        </section>

        <section class="surface p-5 sm:p-6">
          <p class="eyebrow">Participation</p>
          <h2 class="mt-1 font-display text-2xl">Turnout by election</h2>
          <div class="mt-4"><BarChart :series="dashboard.turnoutByElection" suffix="%" /></div>
        </section>
      </div>

      <div class="grid gap-5 lg:grid-cols-[1fr_1fr]">
        <section class="surface p-5 sm:p-6">
          <div class="flex items-center justify-between">
            <div>
              <p class="eyebrow">Recent evidence</p>
              <h2 class="mt-1 font-display text-2xl">Administrative events</h2>
            </div>
            <RouterLink :to="isOfficer ? '/admin/elections' : '/admin/audit'" class="text-brand-700">
              <ScrollText class="h-5 w-5" />
            </RouterLink>
          </div>
          <div class="mt-4 divide-y divide-ink-500/10">
            <div v-for="event in dashboard.recentAudit" :key="event.eventId" class="py-3">
              <p class="text-sm font-semibold text-ink-950">{{ event.eventType }}</p>
              <p class="mt-1 text-xs text-ink-500">{{ event.actorDisplay }} · {{ formatDateTime(event.occurredAt) }}</p>
            </div>
            <p v-if="dashboard.recentAudit.length === 0" class="py-3 text-sm text-ink-500">No audit events yet.</p>
          </div>
        </section>

        <section class="surface bg-brand-950 p-6 text-white">
          <Megaphone class="h-6 w-6 text-gold-300" />
          <h2 class="mt-4 font-display text-3xl">Communicate the process, not the politics.</h2>
          <p class="mt-3 text-sm leading-6 text-brand-100">
            Publish schedules and notices through the announcement system. Never include a voter's selection or a
            private receipt in any message.
          </p>
          <div class="mt-5 flex flex-wrap gap-2">
            <RouterLink v-if="canManageElections" to="/admin/elections">
              <AppButton variant="gold" size="sm">Open elections</AppButton>
            </RouterLink>
            <template v-else>
              <RouterLink to="/admin/users"><AppButton variant="gold" size="sm"><Users class="h-4 w-4" /> Users</AppButton></RouterLink>
              <RouterLink to="/admin/settings"><AppButton variant="secondary" size="sm"><Settings class="h-4 w-4" /> Settings</AppButton></RouterLink>
            </template>
          </div>
        </section>
      </div>
    </template>
  </div>
</template>
