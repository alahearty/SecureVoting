<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  status: string
  subtle?: boolean
}>()

const label = computed(() =>
  props.status
    .replace(/([a-z])([A-Z])/g, '$1 $2')
    .replace('Under Review', 'Under review')
    .replace('Runoff Required', 'Runoff required')
)

const classes = computed(() => {
  const palette: Record<string, string> = {
    Draft: 'bg-slate-100 text-slate-700 ring-slate-200',
    Scheduled: 'bg-sky-50 text-sky-800 ring-sky-200',
    Open: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
    Closed: 'bg-amber-50 text-amber-800 ring-amber-200',
    UnderReview: 'bg-violet-50 text-violet-800 ring-violet-200',
    Published: 'bg-brand-700 text-white ring-brand-700',
    Suspended: 'bg-orange-50 text-orange-800 ring-orange-200',
    Cancelled: 'bg-red-50 text-red-800 ring-red-200',
    Pending: 'bg-amber-50 text-amber-800 ring-amber-200',
    Approved: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
    Rejected: 'bg-red-50 text-red-800 ring-red-200',
    Withdrawn: 'bg-slate-100 text-slate-700 ring-slate-200',
    Disqualified: 'bg-red-50 text-red-800 ring-red-200',
    Provisional: 'bg-sky-50 text-sky-800 ring-sky-200',
    Verified: 'bg-violet-50 text-violet-800 ring-violet-200',
    Superseded: 'bg-slate-100 text-slate-600 ring-slate-200',
    Success: 'bg-emerald-50 text-emerald-800 ring-emerald-200',
    Failure: 'bg-red-50 text-red-800 ring-red-200',
    Denied: 'bg-orange-50 text-orange-800 ring-orange-200'
  }
  return [
    'inline-flex items-center rounded-full px-2.5 py-1 text-[11px] font-bold tracking-wide ring-1 ring-inset',
    palette[props.status] ?? 'bg-ink-950/5 text-ink-700 ring-ink-500/20'
  ]
})
</script>

<template>
  <span :class="classes">{{ label }}</span>
</template>
