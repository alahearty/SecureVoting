<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { ArrowLeft, ArrowRight, CalendarDays, CheckCircle2, Clock3, LockKeyhole, Vote } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { ElectionDetail, ElectionSummary, VoteStatus } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import CandidateCard from '@/components/CandidateCard.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { formatDateTime, timeUntil } from '@/utils/format'

const route = useRoute()
const electionId = computed(() => String(route.params.id))
const detail = ref<ElectionDetail | null>(null)
const summary = ref<ElectionSummary | null>(null)
const voteStatus = ref<VoteStatus | null>(null)
const loading = ref(true)
const error = ref('')

const canVote = computed(
  () =>
    detail.value?.status === 'Open' &&
    summary.value?.isEligible === true &&
    voteStatus.value?.hasVoted !== true
)
const hasVoted = computed(() => summary.value?.hasVoted === true || voteStatus.value?.hasVoted === true)
const isPublished = computed(() => detail.value?.status === 'Published' || Boolean(summary.value?.currentResultId))

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const [electionDetail, elections] = await Promise.all([
      api<ElectionDetail>(`/student/elections/${electionId.value}`),
      api<ElectionSummary[]>('/student/elections')
    ])
    detail.value = electionDetail
    summary.value = elections.find((election) => election.id === electionId.value) ?? null
    if (electionDetail.status === 'Open') {
      voteStatus.value = await api<VoteStatus>(`/student/elections/${electionId.value}/vote-status`)
    }
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Election details could not be loaded.'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <RouterLink to="/student/elections" class="inline-flex items-center gap-2 text-xs font-bold text-brand-700">
      <ArrowLeft class="h-4 w-4" /> Back to elections
    </RouterLink>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="5" />

    <template v-else-if="detail && summary">
      <PageHeader
        eyebrow="Departmental election"
        :title="detail.title"
        :description="`${detail.department} (${detail.departmentCode}) · ${detail.description}`"
      >
        <template #actions>
          <StatusBadge :status="detail.status" />
          <RouterLink v-if="isPublished" :to="{ name: 'published-result', params: { id: detail.id } }">
            <AppButton variant="gold">View published result</AppButton>
          </RouterLink>
          <RouterLink v-else-if="canVote" :to="{ name: 'ballot', params: { id: detail.id } }">
            <AppButton><Vote class="h-4 w-4" /> Cast your ballot</AppButton>
          </RouterLink>
          <AppButton v-else-if="hasVoted" variant="secondary" disabled>
            <CheckCircle2 class="h-4 w-4" /> Ballot recorded
          </AppButton>
        </template>
      </PageHeader>

      <div class="grid gap-5 lg:grid-cols-[1fr_320px]">
        <div class="space-y-5">
          <section class="surface p-5 sm:p-6">
            <div class="flex flex-wrap items-center gap-2">
              <p class="eyebrow">Approved candidates</p>
              <span class="ml-auto text-xs font-semibold text-ink-500">{{ detail.positions.length }} positions</span>
            </div>
            <EmptyState
              v-if="detail.positions.length === 0"
              title="No approved positions yet"
              description="Candidate information will appear when the electoral officer approves the ballot."
              compact
            />
            <div v-for="position in detail.positions" :key="position.id" class="mt-6 first:mt-4">
              <div class="flex flex-wrap items-end gap-2">
                <h2 class="font-display text-2xl text-ink-950">{{ position.title }}</h2>
                <span class="rounded-full bg-ink-950/5 px-2 py-1 text-[10px] font-bold text-ink-600">
                  {{ position.seatCount }} seat{{ position.seatCount === 1 ? '' : 's' }}
                </span>
              </div>
              <p class="mt-1 text-sm leading-6 text-ink-500">{{ position.description }}</p>
              <div class="mt-4 grid gap-3 sm:grid-cols-2">
                <CandidateCard
                  v-for="candidate in position.candidates"
                  :key="candidate.id"
                  :candidate="candidate"
                  compact
                />
              </div>
            </div>
          </section>
        </div>

        <aside class="space-y-4">
          <section class="surface p-5">
            <p class="eyebrow">Election schedule</p>
            <dl class="mt-4 space-y-4 text-sm">
              <div>
                <dt class="text-[11px] font-bold tracking-wide text-ink-500 uppercase">Voting opens</dt>
                <dd class="mt-1 flex items-center gap-2 font-semibold text-ink-950">
                  <CalendarDays class="h-4 w-4 text-brand-700" /> {{ formatDateTime(detail.opensAt) }}
                </dd>
              </div>
              <div>
                <dt class="text-[11px] font-bold tracking-wide text-ink-500 uppercase">Voting closes</dt>
                <dd class="mt-1 flex items-center gap-2 font-semibold text-ink-950">
                  <Clock3 class="h-4 w-4 text-brand-700" /> {{ formatDateTime(detail.closesAt) }}
                </dd>
              </div>
              <div class="rounded-xl bg-brand-50 px-3 py-2 text-xs font-semibold text-brand-800">
                {{ detail.status === 'Open' ? timeUntil(detail.closesAt) : 'Window managed by the electoral office' }}
              </div>
            </dl>
          </section>

          <section class="rounded-2xl bg-brand-950 p-5 text-white">
            <LockKeyhole class="h-5 w-5 text-gold-300" />
            <h2 class="mt-3 font-display text-xl">Ballot privacy</h2>
            <p class="mt-2 text-xs leading-5 text-brand-100">
              Your eligibility and ballot entitlement are stored separately from your choices. No API or exported report
              links your identity to your selections.
            </p>
            <p class="mt-3 text-[11px] font-semibold text-gold-200">
              Your vote cannot be changed once accepted.
            </p>
          </section>

          <RouterLink v-if="detail.status === 'Published'" :to="{ name: 'published-result', params: { id: detail.id } }">
            <AppButton variant="secondary" block>
              Read the official result <ArrowRight class="h-4 w-4" />
            </AppButton>
          </RouterLink>
        </aside>
      </div>
    </template>
  </div>
</template>
