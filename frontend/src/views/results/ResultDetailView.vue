<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import {
  ArrowLeft,
  BadgeCheck,
  ClipboardCheck,
  History,
  LockKeyhole,
  ShieldCheck
} from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { ElectionDetail, ElectionResult } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import LoadingState from '@/components/LoadingState.vue'
import ModalDialog from '@/components/ModalDialog.vue'
import PageHeader from '@/components/PageHeader.vue'
import ResultReport from '@/components/ResultReport.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirmStore } from '@/stores/confirm'
import { useToastStore } from '@/stores/toasts'

const route = useRoute()
const auth = useAuthStore()
const confirm = useConfirmStore()
const toasts = useToastStore()
const electionId = computed(() => String(route.params.id))
const detail = ref<ElectionDetail | null>(null)
const result = ref<ElectionResult | null>(null)
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const actionNotes = ref('')
const recountOpen = ref(false)
const recountReason = ref('')

const canReview = computed(
  () => auth.hasRole('ElectoralOfficer') && detail.value?.status === 'Closed'
)
const canVerify = computed(
  () => auth.hasRole('Observer') && result.value?.status === 'Provisional' && detail.value?.status === 'UnderReview'
)
const canPublish = computed(
  () => auth.hasRole('SuperAdministrator') && result.value?.status === 'Verified' && detail.value?.status === 'UnderReview'
)
const canRecount = computed(
  () => auth.hasRole('SuperAdministrator') && result.value?.status === 'Published'
)
const exportPath = computed(() =>
  result.value?.status === 'Published'
    ? `/elections/${electionId.value}/results/published/export.csv`
    : `/elections/${electionId.value}/results/latest/export.csv`
)
const backRoute = computed(() =>
  auth.hasRole('Observer') ? '/observer/elections' : '/admin/results'
)

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    detail.value = await api<ElectionDetail>(`/result-review/elections/${electionId.value}`)
    try {
      result.value = await api<ElectionResult>(`/elections/${electionId.value}/results/latest`)
    } catch (caught) {
      if (caught instanceof ApiError && caught.status === 404) {
        result.value = null
      } else {
        throw caught
      }
    }
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Result review details could not be loaded.'
  } finally {
    loading.value = false
  }
}

async function requestReview(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    result.value = await api<ElectionResult>(`/elections/${electionId.value}/results/review`, { method: 'POST' })
    toasts.success('Provisional result generated. Observer verification is now required.')
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Result review could not be started.'
  } finally {
    busy.value = false
  }
}

async function verify(): Promise<void> {
  if (!result.value || actionNotes.value.trim().length < 5) {
    error.value = 'Add verification notes before submitting.'
    return
  }
  busy.value = true
  error.value = ''
  try {
    await api(`/elections/${electionId.value}/results/${result.value.id}/verify`, {
      method: 'POST',
      body: { notes: actionNotes.value }
    })
    toasts.success('Observer verification recorded.')
    actionNotes.value = ''
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Verification could not be recorded.'
  } finally {
    busy.value = false
  }
}

async function publish(): Promise<void> {
  if (!result.value || actionNotes.value.trim().length < 5) {
    error.value = 'Add publication notes before submitting.'
    return
  }
  const confirmed = await confirm.ask({
    title: 'Publish this result?',
    message:
      'Published results are immutable through the normal workflow. A correction requires a documented recount and another verification cycle.',
    confirmLabel: 'Publish officially',
    tone: 'danger'
  })
  if (!confirmed) return

  busy.value = true
  error.value = ''
  try {
    await api(`/elections/${electionId.value}/results/${result.value.id}/publish`, {
      method: 'POST',
      body: { notes: actionNotes.value }
    })
    toasts.success('Official result published.')
    actionNotes.value = ''
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The result could not be published.'
  } finally {
    busy.value = false
  }
}

async function recount(): Promise<void> {
  if (recountReason.value.trim().length < 5) {
    error.value = 'Add a reason for the recount.'
    return
  }
  busy.value = true
  error.value = ''
  try {
    await api(`/elections/${electionId.value}/results/recount`, {
      method: 'POST',
      body: { reason: recountReason.value }
    })
    recountOpen.value = false
    recountReason.value = ''
    toasts.success('Recount snapshot created. Observer verification is required again.')
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The recount could not be started.'
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <RouterLink :to="backRoute" class="inline-flex items-center gap-2 text-xs font-bold text-brand-700">
      <ArrowLeft class="h-4 w-4" /> Back to result review
    </RouterLink>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="5" />

    <template v-else-if="detail">
      <PageHeader eyebrow="Result verification" :title="detail.title" :description="`${detail.department} (${detail.departmentCode}) · Review aggregate evidence, record the required approval decision and publish only after the configured controls are satisfied.`">
        <template #actions>
          <StatusBadge :status="detail.status" />
          <AppButton variant="secondary" @click="load">Refresh</AppButton>
        </template>
      </PageHeader>

      <div class="grid gap-5 lg:grid-cols-[1fr_320px]">
        <div class="space-y-5">
          <div v-if="result">
            <ResultReport :result="result" :export-path="exportPath" />
          </div>
          <div v-else class="surface p-8 text-center">
            <History class="mx-auto h-8 w-8 text-brand-700" />
            <h2 class="mt-3 font-display text-2xl">No result snapshot yet</h2>
            <p class="mt-2 text-sm text-ink-500">Close the election, then request result review to generate a provisional snapshot.</p>
            <AppButton v-if="canReview" class="mt-4" :loading="busy" @click="requestReview">Generate provisional result</AppButton>
          </div>
        </div>

        <aside class="space-y-4">
          <section class="surface p-5">
            <p class="eyebrow">Workflow</p>
            <ol class="mt-4 space-y-3 text-xs leading-5 text-ink-600">
              <li class="flex gap-2"><span class="font-bold text-brand-700">01</span> Electoral officer generates a provisional snapshot.</li>
              <li class="flex gap-2"><span class="font-bold text-brand-700">02</span> Independent observer verifies aggregate reconciliation.</li>
              <li class="flex gap-2"><span class="font-bold text-brand-700">03</span> Super Administrator publishes the official version.</li>
            </ol>
          </section>

          <section v-if="canReview" class="surface p-5">
            <BadgeCheck class="h-5 w-5 text-brand-700" />
            <h2 class="mt-3 font-display text-xl">Start result review</h2>
            <p class="mt-1 text-xs leading-5 text-ink-500">This generates a versioned aggregate snapshot from accepted ballots.</p>
            <AppButton class="mt-3" block :loading="busy" @click="requestReview">Request review</AppButton>
          </section>

          <section v-if="canVerify" class="surface p-5">
            <ClipboardCheck class="h-5 w-5 text-violet-700" />
            <h2 class="mt-3 font-display text-xl">Observer verification</h2>
            <p class="mt-1 text-xs leading-5 text-ink-500">Record your reconciliation notes before verifying this snapshot.</p>
            <textarea v-model="actionNotes" class="field mt-3 min-h-24" placeholder="Verification notes" />
            <AppButton class="mt-3" block :loading="busy" :disabled="actionNotes.trim().length < 5" @click="verify">
              Verify snapshot
            </AppButton>
          </section>

          <section v-if="canPublish" class="surface p-5">
            <ShieldCheck class="h-5 w-5 text-brand-700" />
            <h2 class="mt-3 font-display text-xl">Publication approval</h2>
            <p class="mt-1 text-xs leading-5 text-ink-500">The observer has verified this result. Add publication notes to make it official.</p>
            <textarea v-model="actionNotes" class="field mt-3 min-h-24" placeholder="Publication notes" />
            <AppButton class="mt-3" block :loading="busy" :disabled="actionNotes.trim().length < 5" @click="publish">
              Publish official result
            </AppButton>
          </section>

          <section v-if="canRecount" class="surface p-5">
            <History class="h-5 w-5 text-gold-600" />
            <h2 class="mt-3 font-display text-xl">Authorised recount</h2>
            <p class="mt-1 text-xs leading-5 text-ink-500">Preserves the published version and opens a new review cycle.</p>
            <AppButton class="mt-3" variant="secondary" block @click="recountOpen = true">Request recount</AppButton>
          </section>

          <section v-if="result" class="rounded-2xl border border-ink-500/10 bg-white p-4">
            <p class="flex items-center gap-2 text-xs font-bold text-ink-700">
              <LockKeyhole class="h-4 w-4 text-brand-700" /> Immutable evidence
            </p>
            <p class="mt-2 break-all font-mono text-[10px] text-ink-500">{{ result.integrityHash }}</p>
          </section>
        </aside>
      </div>
    </template>
  </div>

  <ModalDialog
    :open="recountOpen"
    title="Request a recount"
    description="The published result will remain preserved. A new version enters the verification workflow."
    @close="recountOpen = false"
  >
    <label class="field-label" for="recount-reason">Reason</label>
    <textarea id="recount-reason" v-model="recountReason" class="field min-h-28" placeholder="Document the authorised reason" />
    <div class="mt-4 flex justify-end gap-2">
      <AppButton variant="secondary" @click="recountOpen = false">Cancel</AppButton>
      <AppButton :loading="busy" :disabled="recountReason.trim().length < 5" @click="recount">Create recount version</AppButton>
    </div>
  </ModalDialog>
</template>
