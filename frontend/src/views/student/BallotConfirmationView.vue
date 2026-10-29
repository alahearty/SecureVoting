<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { CheckCircle2, Clipboard, Clock3, LockKeyhole } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { BallotAccepted, VoteStatus } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import LoadingState from '@/components/LoadingState.vue'
import { formatDateTime } from '@/utils/format'
import { useToastStore } from '@/stores/toasts'

const route = useRoute()
const router = useRouter()
const toasts = useToastStore()
const electionId = String(route.params.id)
const status = ref<VoteStatus | null>(null)
const receipt = ref<BallotAccepted | null>(null)
const loading = ref(true)
const error = ref('')

function readReceipt(): BallotAccepted | null {
  try {
    const raw = sessionStorage.getItem('securevote-last-receipt')
    if (!raw) return null
    const parsed = JSON.parse(raw) as BallotAccepted
    return parsed.electionId === electionId ? parsed : null
  } catch {
    return null
  }
}

async function load(): Promise<void> {
  loading.value = true
  try {
    receipt.value = readReceipt()
    status.value = await api<VoteStatus>(`/student/elections/${electionId}/vote-status`)
    if (!status.value.hasVoted && !receipt.value) {
      await router.replace({ name: 'student-election', params: { id: electionId } })
    }
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Confirmation status could not be loaded.'
  } finally {
    loading.value = false
  }
}

async function copyReference(): Promise<void> {
  if (!receipt.value) return
  await navigator.clipboard.writeText(receipt.value.confirmationReference)
  toasts.success('Confirmation reference copied.')
}

onMounted(load)
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-5">
    <LoadingState v-if="loading" :rows="3" />
    <div v-else-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <template v-else-if="status">
      <section class="surface overflow-hidden">
        <div class="bg-brand-950 px-6 py-8 text-white sm:px-10">
          <div class="flex h-14 w-14 items-center justify-center rounded-2xl bg-emerald-400/15 text-emerald-300">
            <CheckCircle2 class="h-8 w-8" />
          </div>
          <p class="mt-5 text-xs font-bold tracking-[0.2em] text-gold-300 uppercase">Ballot accepted</p>
          <h1 class="mt-2 font-display text-4xl leading-tight">Your vote has been recorded.</h1>
          <p class="mt-3 max-w-xl text-sm leading-6 text-brand-100">
            The entitlement for this election has been consumed. Your identity was not stored with your selections.
          </p>
        </div>

        <div class="space-y-5 p-6 sm:p-10">
          <div class="grid gap-3 sm:grid-cols-2">
            <div class="rounded-2xl border border-ink-500/10 bg-paper p-4">
              <p class="text-[10px] font-bold tracking-[0.16em] text-ink-500 uppercase">Recorded at</p>
              <p class="mt-2 flex items-center gap-2 text-sm font-semibold text-ink-950">
                <Clock3 class="h-4 w-4 text-brand-700" />
                {{ formatDateTime(status.votedAt ?? receipt?.submittedAtUtc ?? '') }}
              </p>
            </div>
            <div class="rounded-2xl border border-ink-500/10 bg-paper p-4">
              <p class="text-[10px] font-bold tracking-[0.16em] text-ink-500 uppercase">Election</p>
              <p class="mt-2 text-sm font-semibold text-ink-950">One accepted ballot</p>
            </div>
          </div>

          <div v-if="receipt" class="rounded-2xl border border-gold-300/60 bg-gold-50 p-5">
            <p class="text-[10px] font-bold tracking-[0.16em] text-gold-700 uppercase">Non-revealing confirmation reference</p>
            <p class="mt-2 break-all font-mono text-sm font-semibold text-ink-950">{{ receipt.confirmationReference }}</p>
            <AppButton variant="secondary" size="sm" class="mt-3" @click="copyReference">
              <Clipboard class="h-3.5 w-3.5" /> Copy reference
            </AppButton>
            <p class="mt-3 text-[11px] leading-5 text-gold-700">
              Keep this reference for your records. It does not reveal your selections and cannot be used to retrieve them.
            </p>
          </div>

          <div class="flex gap-3 rounded-2xl bg-brand-50 p-4 text-xs leading-5 text-brand-900">
            <LockKeyhole class="mt-0.5 h-5 w-5 shrink-0" />
            <p>
              The confirmation reference proves submission to you, but the system intentionally provides no public
              receipt-to-ballot lookup. This protects ballot secrecy.
            </p>
          </div>

          <div class="flex flex-col gap-2 sm:flex-row">
            <RouterLink :to="{ name: 'student-dashboard' }" class="flex-1">
              <AppButton block>Back to dashboard</AppButton>
            </RouterLink>
            <RouterLink :to="{ name: 'student-election', params: { id: electionId } }" class="flex-1">
              <AppButton variant="secondary" block>View election</AppButton>
            </RouterLink>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>
