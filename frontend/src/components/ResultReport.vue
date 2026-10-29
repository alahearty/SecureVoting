<script setup lang="ts">
import { computed } from 'vue'
import { FileDown, Printer, Sheet } from '@lucide/vue'
import type { ElectionResult } from '@/api/types'
import { download } from '@/api/client'
import { exportResultPdf } from '@/utils/resultPdf'
import { formatDateTime, formatNumber, formatPercent } from '@/utils/format'
import AppButton from './AppButton.vue'
import BarChart from './BarChart.vue'
import MetricCard from './MetricCard.vue'
import StatusBadge from './StatusBadge.vue'
import { useToastStore } from '@/stores/toasts'

const props = withDefaults(
  defineProps<{
    result: ElectionResult
    exportPath?: string
    showActions?: boolean
  }>(),
  { exportPath: '', showActions: true }
)

const toasts = useToastStore()

const chartSeries = computed(() => ({
  label: 'Votes by candidate',
  points: props.result.positions.flatMap((position) =>
    position.candidates.map((candidate) => ({
      label: candidate.candidateName,
      value: candidate.votes
    }))
  )
}))

function printPage(): void {
  window.print()
}

async function downloadCsv(): Promise<void> {
  if (!props.exportPath) return
  try {
    await download(props.exportPath, `securevote-result-v${props.result.version}.csv`)
  } catch {
    toasts.error('The CSV export could not be downloaded.')
  }
}
</script>

<template>
  <div class="space-y-5">
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div>
        <div class="flex flex-wrap items-center gap-2">
          <StatusBadge :status="result.status" />
          <span class="text-xs font-semibold text-ink-500">Version {{ result.version }}</span>
        </div>
        <h2 class="mt-3 font-display text-3xl tracking-tight text-ink-950">{{ result.electionTitle }}</h2>
        <p class="mt-2 text-sm text-ink-500">
          {{ result.status === 'Published' ? 'Published' : 'Generated' }}
          {{ formatDateTime(result.publishedAt ?? result.generatedAt) }}
          <span v-if="result.changeReason"> · Correction: {{ result.changeReason }}</span>
        </p>
      </div>
      <div v-if="showActions" class="no-print flex flex-wrap gap-2">
        <AppButton variant="secondary" size="sm" @click="exportResultPdf(result)">
          <FileDown class="h-4 w-4" /> PDF
        </AppButton>
        <AppButton v-if="exportPath" variant="secondary" size="sm" @click="downloadCsv">
          <Sheet class="h-4 w-4" /> CSV
        </AppButton>
        <AppButton variant="secondary" size="sm" @click="printPage">
          <Printer class="h-4 w-4" /> Print
        </AppButton>
      </div>
    </div>

    <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
      <MetricCard label="Eligible voters" :value="formatNumber(result.totalEligible)" detail="Official register count" />
      <MetricCard label="Accepted ballots" :value="formatNumber(result.totalBallots)" detail="Anonymous accepted ballots" tone="brand" />
      <MetricCard label="Turnout" :value="formatPercent(result.turnoutPercentage)" detail="Accepted / eligible" tone="gold" />
      <MetricCard label="Seat races" :value="String(result.positions.length)" detail="Positions in this snapshot" />
    </div>

    <section class="surface p-5 sm:p-6">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p class="eyebrow">Aggregate view</p>
          <h3 class="mt-1 font-display text-2xl text-ink-950">Votes by candidate</h3>
        </div>
        <p class="text-xs text-ink-500">No individual ballot data is exposed.</p>
      </div>
      <div class="mt-4">
        <BarChart :series="chartSeries" />
      </div>
    </section>

    <section class="surface overflow-hidden">
      <div class="border-b border-ink-500/10 px-5 py-4 sm:px-6">
        <p class="eyebrow">Official breakdown</p>
        <h3 class="mt-1 font-display text-2xl text-ink-950">Position results</h3>
      </div>
      <div class="divide-y divide-ink-500/10">
        <section v-for="position in result.positions" :key="position.positionId" class="p-5 sm:p-6">
          <div class="flex flex-wrap items-end justify-between gap-2">
            <div>
              <h4 class="font-display text-2xl text-ink-950">{{ position.positionTitle }}</h4>
              <p class="mt-1 text-xs text-ink-500">
                {{ position.seatCount }} seat{{ position.seatCount === 1 ? '' : 's' }} ·
                {{ position.abstentions }} abstention{{ position.abstentions === 1 ? '' : 's' }}
              </p>
            </div>
            <p v-if="position.tieNote" class="max-w-md rounded-xl bg-gold-50 px-3 py-2 text-xs font-semibold text-gold-800">
              {{ position.tieNote }}
            </p>
          </div>
          <div class="mt-4 overflow-x-auto">
            <table class="w-full min-w-[560px] text-left text-sm">
              <thead>
                <tr class="border-b border-ink-500/10 text-[10px] font-bold tracking-wide text-ink-500 uppercase">
                  <th class="py-2 pr-4">Candidate</th>
                  <th class="py-2 pr-4">Votes</th>
                  <th class="py-2 pr-4">Share</th>
                  <th class="py-2">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-ink-500/10">
                <tr v-for="candidate in position.candidates" :key="candidate.candidateId">
                  <td class="py-3 pr-4 font-semibold text-ink-950">{{ candidate.candidateName }}</td>
                  <td class="py-3 pr-4 text-ink-700">{{ candidate.votes }}</td>
                  <td class="py-3 pr-4 text-ink-700">{{ formatPercent(candidate.percentage) }}</td>
                  <td class="py-3">
                    <span
                      v-if="candidate.isWinner"
                      class="rounded-full bg-brand-50 px-2.5 py-1 text-[11px] font-bold text-brand-800"
                    >
                      Winner
                    </span>
                    <span
                      v-else-if="candidate.isTie"
                      class="rounded-full bg-gold-50 px-2.5 py-1 text-[11px] font-bold text-gold-800"
                    >
                      Tie
                    </span>
                    <span v-else class="text-xs text-ink-500">—</span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </section>

    <section class="surface p-5 sm:p-6">
      <p class="eyebrow">Verification record</p>
      <div class="mt-3 grid gap-4 sm:grid-cols-2">
        <div>
          <p class="text-xs font-bold text-ink-500">Integrity hash</p>
          <p class="mt-1 break-all font-mono text-[11px] text-ink-700">{{ result.integrityHash }}</p>
        </div>
        <div>
          <p class="text-xs font-bold text-ink-500">Approvals</p>
          <ul class="mt-1 space-y-2 text-xs text-ink-600">
            <li v-for="approval in result.approvals" :key="`${approval.approvalType}-${approval.createdAt}`">
              {{ approval.role }} · {{ approval.approvalType }} · {{ approval.decision }}
            </li>
            <li v-if="result.approvals.length === 0">No approvals recorded for this snapshot.</li>
          </ul>
        </div>
      </div>
    </section>
  </div>
</template>
