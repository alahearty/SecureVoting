<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { AlertTriangle, ArrowLeft, Check, ChevronLeft, ClipboardCheck, LockKeyhole, ShieldCheck } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { Ballot, BallotAccepted, BallotPosition } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import CandidateCard from '@/components/CandidateCard.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useConfirmStore } from '@/stores/confirm'
import { useToastStore } from '@/stores/toasts'
import { formatDateTime } from '@/utils/format'

const route = useRoute()
const router = useRouter()
const confirm = useConfirmStore()
const toasts = useToastStore()
const electionId = String(route.params.id)
const ballot = ref<Ballot | null>(null)
const loading = ref(true)
const error = ref('')
const step = ref<'ballot' | 'review'>('ballot')
const submitting = ref(false)
const acknowledged = ref(false)
const selections = reactive<Record<string, string[]>>({})
const abstentions = reactive<Record<string, boolean>>({})

const allPositionsComplete = computed(() => {
  if (!ballot.value) return false
  return ballot.value.positions.every((position) => {
    if (abstentions[position.id]) return position.allowAbstention
    const selected = selections[position.id] ?? []
    return selected.length > 0 && selected.length <= position.maxSelections
  })
})

const selectedCount = computed(() =>
  Object.values(selections).reduce((count, candidates) => count + candidates.length, 0)
)

function isSelected(positionId: string, candidateId: string): boolean {
  return selections[positionId]?.includes(candidateId) ?? false
}

function toggleCandidate(position: BallotPosition, candidateId: string): void {
  abstentions[position.id] = false
  const current = selections[position.id] ?? []
  if (position.maxSelections === 1) {
    selections[position.id] = [candidateId]
    return
  }

  if (current.includes(candidateId)) {
    selections[position.id] = current.filter((id) => id !== candidateId)
  } else if (current.length < position.maxSelections) {
    selections[position.id] = [...current, candidateId]
  }
}

function toggleAbstain(position: BallotPosition): void {
  if (!position.allowAbstention) return
  abstentions[position.id] = !abstentions[position.id]
  if (abstentions[position.id]) {
    selections[position.id] = []
  }
}

function positionChoice(position: BallotPosition): string {
  if (abstentions[position.id]) return 'Explicit abstention'
  const ids = selections[position.id] ?? []
  const names = ids
    .map((id) => position.candidates.find((candidate) => candidate.id === id)?.displayName)
    .filter(Boolean)
  return names.length > 0 ? names.join(', ') : 'Not selected'
}

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    ballot.value = await api<Ballot>(`/student/elections/${electionId}/ballot`)
    if (ballot.value.hasVoted) {
      await router.replace({ name: 'ballot-confirmed', params: { id: electionId } })
    }
  } catch (caught) {
    if (caught instanceof ApiError && caught.status === 409) {
      await router.replace({ name: 'ballot-confirmed', params: { id: electionId } })
      return
    }
    error.value = caught instanceof ApiError ? caught.message : 'The ballot could not be loaded.'
  } finally {
    loading.value = false
  }
}

async function submit(): Promise<void> {
  if (!ballot.value || !allPositionsComplete.value || !acknowledged.value) return
  const confirmed = await confirm.ask({
    title: 'Submit your ballot?',
    message:
      'This is your final confirmation. Your ballot cannot be edited or withdrawn after acceptance, and your identity will not be attached to the choices.',
    confirmLabel: 'Submit ballot',
    tone: 'default'
  })
  if (!confirmed) return

  submitting.value = true
  error.value = ''
  try {
    const accepted = await api<BallotAccepted>(`/student/elections/${electionId}/ballots`, {
      method: 'POST',
      body: {
        positions: ballot.value.positions.map((position) => ({
          positionId: position.id,
          candidateIds: abstentions[position.id] ? [] : (selections[position.id] ?? [])
        }))
      }
    })
    sessionStorage.setItem('securevote-last-receipt', JSON.stringify(accepted))
    for (const position of ballot.value.positions) {
      delete selections[position.id]
      delete abstentions[position.id]
    }
    await router.replace({ name: 'ballot-confirmed', params: { id: electionId } })
  } catch (caught) {
    if (caught instanceof ApiError && caught.status === 409) {
      toasts.info('A ballot has already been recorded for this election.')
      await router.replace({ name: 'ballot-confirmed', params: { id: electionId } })
      return
    }
    error.value =
      caught instanceof ApiError ? caught.message : 'Your ballot could not be submitted. Nothing was accepted.'
  } finally {
    submitting.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <RouterLink :to="{ name: 'student-election', params: { id: electionId } }" class="inline-flex items-center gap-2 text-xs font-bold text-brand-700">
      <ArrowLeft class="h-4 w-4" /> Back to election
    </RouterLink>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <LoadingState v-if="loading" :rows="5" />

    <template v-else-if="ballot">
      <PageHeader
        eyebrow="Private ballot"
        :title="step === 'ballot' ? 'Make your selections' : 'Review before submission'"
        :description="step === 'ballot'
          ? 'Choose one candidate for each position, or record an explicit abstention where the rules allow.'
          : 'Check every position carefully. This is your only opportunity to change the ballot.'"
      >
        <template #actions>
          <span class="rounded-full bg-white px-3 py-2 text-xs font-bold text-ink-700 shadow-sm">
            {{ ballot.positions.length }} positions
          </span>
        </template>
      </PageHeader>

      <div class="mb-5 flex items-center gap-2 text-xs font-semibold text-ink-500">
        <span class="flex items-center gap-2" :class="step === 'ballot' ? 'text-brand-800' : ''">
          <span class="grid h-6 w-6 place-items-center rounded-full bg-brand-700 text-white">1</span> Ballot
        </span>
        <span class="h-px flex-1 bg-ink-500/15" />
        <span class="flex items-center gap-2" :class="step === 'review' ? 'text-brand-800' : ''">
          <span class="grid h-6 w-6 place-items-center rounded-full" :class="step === 'review' ? 'bg-brand-700 text-white' : 'bg-ink-950/10 text-ink-500'">2</span> Review
        </span>
        <span class="h-px flex-1 bg-ink-500/15" />
        <span class="flex items-center gap-2">
          <span class="grid h-6 w-6 place-items-center rounded-full bg-ink-950/10 text-ink-500">3</span> Confirm
        </span>
      </div>

      <div v-if="step === 'ballot'" class="grid gap-5 lg:grid-cols-[1fr_300px]">
        <div class="space-y-4">
          <section v-for="position in ballot.positions" :key="position.id" class="surface p-5 sm:p-6">
            <div class="flex flex-wrap items-start gap-2">
              <div>
                <p class="eyebrow">Position</p>
                <h2 class="mt-1 font-display text-2xl text-ink-950">{{ position.title }}</h2>
              </div>
              <span class="ml-auto rounded-full bg-brand-50 px-3 py-1 text-[11px] font-bold text-brand-800">
                Choose {{ position.maxSelections }} candidate{{ position.maxSelections === 1 ? '' : 's' }}
              </span>
            </div>
            <p class="mt-2 text-sm leading-6 text-ink-500">{{ position.description }}</p>
            <div class="mt-4 grid gap-3">
              <button
                v-for="candidate in position.candidates"
                :key="candidate.id"
                type="button"
                class="focus-ring text-left"
                :data-testid="`candidate-${candidate.id}`"
                :aria-pressed="isSelected(position.id, candidate.id)"
                @click="toggleCandidate(position, candidate.id)"
              >
                <CandidateCard
                  :candidate="candidate"
                  :selected="isSelected(position.id, candidate.id)"
                  selectable
                  compact
                />
              </button>
            </div>
            <button
              v-if="position.allowAbstention"
              type="button"
              class="focus-ring mt-4 inline-flex items-center gap-2 rounded-xl border px-3 py-2 text-xs font-bold"
              :class="abstentions[position.id] ? 'border-gold-400 bg-gold-50 text-gold-800' : 'border-ink-500/15 text-ink-500 hover:bg-ink-950/5'"
              :aria-pressed="abstentions[position.id]"
              @click="toggleAbstain(position)"
            >
              <Check class="h-4 w-4" :class="abstentions[position.id] ? '' : 'opacity-0'" />
              {{ abstentions[position.id] ? 'Abstention recorded' : 'Abstain from this position' }}
            </button>
          </section>
        </div>

        <aside class="space-y-4">
          <section class="surface p-5">
            <p class="eyebrow">Ballot rules</p>
            <ul class="mt-3 space-y-3 text-xs leading-5 text-ink-600">
              <li class="flex gap-2"><Check class="mt-0.5 h-4 w-4 shrink-0 text-brand-700" /> Every position must be reviewed.</li>
              <li class="flex gap-2"><Check class="mt-0.5 h-4 w-4 shrink-0 text-brand-700" /> Maximum selections are shown per position.</li>
              <li v-if="ballot.positions.some((position) => position.allowAbstention)" class="flex gap-2"><Check class="mt-0.5 h-4 w-4 shrink-0 text-brand-700" /> Abstention is allowed only where indicated.</li>
              <li class="flex gap-2"><AlertTriangle class="mt-0.5 h-4 w-4 shrink-0 text-gold-600" /> Your ballot cannot be changed after submission.</li>
            </ul>
          </section>
          <section class="rounded-2xl bg-brand-950 p-5 text-white">
            <LockKeyhole class="h-5 w-5 text-gold-300" />
            <p class="mt-3 font-display text-xl">Closes {{ formatDateTime(ballot.closesAt) }}</p>
            <p class="mt-2 text-xs leading-5 text-brand-100">The trusted server clock decides whether your submission is accepted.</p>
          </section>
          <AppButton data-testid="review-ballot" size="lg" block :disabled="!allPositionsComplete" @click="step = 'review'">
            Review ballot
          </AppButton>
          <p v-if="!allPositionsComplete" class="text-center text-[11px] text-ink-500">
            Complete every position to continue.
          </p>
        </aside>
      </div>

      <div v-else class="grid gap-5 lg:grid-cols-[1fr_300px]">
        <section class="surface p-5 sm:p-6">
          <div class="flex items-center justify-between gap-3">
            <div>
              <p class="eyebrow">Your choices</p>
              <h2 class="mt-1 font-display text-2xl">Review the full ballot</h2>
            </div>
            <button type="button" class="text-xs font-bold text-brand-700" @click="step = 'ballot'">
              Edit selections
            </button>
          </div>
          <div class="mt-5 divide-y divide-ink-500/10">
            <div v-for="position in ballot.positions" :key="position.id" class="py-4 first:pt-0 last:pb-0">
              <p class="text-xs font-bold tracking-wide text-ink-500 uppercase">{{ position.title }}</p>
              <p class="mt-1 text-sm font-semibold text-ink-950">{{ positionChoice(position) }}</p>
            </div>
          </div>
          <label class="mt-6 flex cursor-pointer items-start gap-3 rounded-xl border border-brand-200 bg-brand-50/60 p-4">
            <input v-model="acknowledged" type="checkbox" class="mt-0.5 h-4 w-4 accent-brand-700" />
            <span class="text-xs leading-5 text-brand-900">
              I have reviewed every position. I understand that submission is final and that no one can connect my identity
              to these selections through ordinary reports or receipts.
            </span>
          </label>
        </section>
        <aside class="space-y-4">
          <section class="surface p-5">
            <p class="eyebrow">Submission</p>
            <p class="mt-2 text-sm leading-6 text-ink-500">
              {{ selectedCount }} candidate selection{{ selectedCount === 1 ? '' : 's' }} across
              {{ ballot.positions.length }} positions.
            </p>
            <div class="mt-4 flex items-center gap-2 rounded-xl bg-ink-950/5 p-3 text-xs text-ink-600">
              <ShieldCheck class="h-5 w-5 shrink-0 text-brand-700" />
              The server re-validates eligibility, candidate approval and the one-vote entitlement.
            </div>
          </section>
          <AppButton size="lg" block :loading="submitting" :disabled="!acknowledged" @click="submit">
            <ClipboardCheck class="h-4 w-4" /> Submit final ballot
          </AppButton>
          <button type="button" class="focus-ring w-full text-xs font-bold text-ink-500" @click="step = 'ballot'">
            <ChevronLeft class="mr-1 inline h-3.5 w-3.5" /> Back to ballot
          </button>
        </aside>
      </div>
    </template>
  </div>
</template>
