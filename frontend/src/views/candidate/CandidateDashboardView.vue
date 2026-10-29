<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import {
  ArrowRight,
  Camera,
  CheckCircle2,
  FileText,
  Megaphone,
  PenLine,
  UserRound
} from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type {
  CandidateDashboard,
  ElectionDetail,
  ElectionSummary,
  PositionPublic
} from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import ModalDialog from '@/components/ModalDialog.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useAuthStore } from '@/stores/auth'
import { useConfirmStore } from '@/stores/confirm'
import { useToastStore } from '@/stores/toasts'
import { formatDateTime } from '@/utils/format'

const auth = useAuthStore()
const confirm = useConfirmStore()
const toasts = useToastStore()
const dashboard = ref<CandidateDashboard | null>(null)
const elections = ref<ElectionSummary[]>([])
const selectedElection = ref<ElectionDetail | null>(null)
const selectedElectionId = ref('')
const selectedPositionId = ref('')
const manifesto = ref('')
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const editOpen = ref(false)
const withdrawOpen = ref(false)
const withdrawReason = ref('')
const photoInput = ref<HTMLInputElement | null>(null)
const editName = ref('')
const editManifesto = ref('')

const nomination = computed(() => dashboard.value?.nomination ?? null)
const canEdit = computed(() => nomination.value?.status === 'Pending' || nomination.value?.status === 'Rejected')
const positions = computed<PositionPublic[]>(() => selectedElection.value?.positions ?? [])
const eligibleElections = computed(() =>
  elections.value.filter((election) => ['Draft', 'Scheduled'].includes(election.status))
)

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const [data, electionList] = await Promise.all([
      api<CandidateDashboard>('/dashboard/candidate'),
      api<ElectionSummary[]>('/candidate/elections')
    ])
    dashboard.value = data
    elections.value = electionList
    if (data.nomination) {
      editName.value = data.nomination.displayName
      editManifesto.value = data.nomination.manifesto
    }
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Candidate workspace could not be loaded.'
  } finally {
    loading.value = false
  }
}

async function selectElection(): Promise<void> {
  selectedPositionId.value = ''
  selectedElection.value = null
  if (!selectedElectionId.value) return
  try {
    selectedElection.value = await api<ElectionDetail>(`/candidate/elections/${selectedElectionId.value}`)
    selectedPositionId.value = selectedElection.value.positions[0]?.id ?? ''
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Election details could not be loaded.'
  }
}

async function nominate(): Promise<void> {
  if (!selectedElectionId.value || !selectedPositionId.value || manifesto.value.trim().length < 20) {
    error.value = 'Choose an election, position and write at least 20 characters of manifesto.'
    return
  }
  busy.value = true
  error.value = ''
  try {
    await api('/candidate/nominations', {
      method: 'POST',
      body: {
        electionId: selectedElectionId.value,
        positionId: selectedPositionId.value,
        manifesto: manifesto.value
      }
    })
    toasts.success('Nomination submitted for approval.')
    manifesto.value = ''
    await load()
    await auth.refresh()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The nomination could not be submitted.'
  } finally {
    busy.value = false
  }
}

async function updateProfile(): Promise<void> {
  if (!nomination.value) return
  busy.value = true
  try {
    await api(`/candidate/nominations/${nomination.value.id}`, {
      method: 'PUT',
      body: { displayName: editName.value, manifesto: editManifesto.value }
    })
    toasts.success('Nomination profile resubmitted.')
    editOpen.value = false
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The profile could not be updated.'
  } finally {
    busy.value = false
  }
}

async function withdraw(): Promise<void> {
  if (!nomination.value || withdrawReason.value.trim().length < 5) {
    error.value = 'Add a withdrawal reason.'
    return
  }
  const confirmed = await confirm.ask({
    title: 'Withdraw your nomination?',
    message: 'This removes your candidacy from the approved ballot if voting has not opened. It cannot be undone automatically.',
    confirmLabel: 'Withdraw nomination',
    tone: 'danger'
  })
  if (!confirmed) return
  busy.value = true
  try {
    await api(`/candidate/nominations/${nomination.value.id}/withdraw`, {
      method: 'POST',
      body: { reason: withdrawReason.value }
    })
    toasts.success('Nomination withdrawn.')
    withdrawOpen.value = false
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The nomination could not be withdrawn.'
  } finally {
    busy.value = false
  }
}

function pickPhoto(): void {
  photoInput.value?.click()
}

async function uploadPhoto(event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || !nomination.value) return
  if (file.size > 2 * 1024 * 1024) {
    error.value = 'Choose a photo smaller than 2 MB.'
    return
  }
  const form = new FormData()
  form.append('photo', file)
  busy.value = true
  try {
    await api(`/candidate/nominations/${nomination.value.id}/photo`, {
      method: 'POST',
      body: form
    })
    toasts.success('Candidate photo updated.')
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The photo could not be uploaded.'
  } finally {
    busy.value = false
    input.value = ''
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="Candidate portal"
      title="Your nomination"
      description="Manage your candidate profile and follow approval. Approved details freeze when voting opens."
    >
      <template #actions>
        <AppButton variant="secondary" @click="load">Refresh</AppButton>
      </template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="4" />

    <template v-else>
      <div v-if="nomination" class="grid gap-5 lg:grid-cols-[1.1fr_0.9fr]">
        <section class="surface p-5 sm:p-6">
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="flex items-center gap-4">
              <div class="grid h-20 w-20 place-items-center overflow-hidden rounded-2xl bg-brand-100 font-display text-2xl font-bold text-brand-800">
                <img v-if="nomination.photoUrl" :src="nomination.photoUrl" :alt="nomination.displayName" class="h-full w-full object-cover" />
                <span v-else>{{ nomination.displayName.slice(0, 2).toUpperCase() }}</span>
              </div>
              <div>
                <StatusBadge :status="nomination.status" />
                <h2 class="mt-2 font-display text-3xl">{{ nomination.displayName }}</h2>
                <p class="mt-1 text-sm text-ink-500">{{ nomination.departmentName }} · {{ nomination.electionTitle }} · {{ nomination.positionTitle }}</p>
              </div>
            </div>
            <div class="flex flex-wrap gap-2">
              <AppButton v-if="canEdit" variant="secondary" size="sm" @click="editOpen = true"><PenLine class="h-4 w-4" /> Edit</AppButton>
              <AppButton variant="secondary" size="sm" @click="pickPhoto"><Camera class="h-4 w-4" /> Photo</AppButton>
              <input ref="photoInput" type="file" accept="image/jpeg,image/png,image/webp" class="hidden" @change="uploadPhoto" />
              <AppButton v-if="nomination.status === 'Pending' || nomination.status === 'Approved'" variant="danger" size="sm" @click="withdrawOpen = true">Withdraw</AppButton>
            </div>
          </div>

          <div class="mt-6 rounded-2xl bg-paper p-5">
            <p class="eyebrow">Manifesto</p>
            <p class="mt-2 whitespace-pre-line text-sm leading-7 text-ink-700">{{ nomination.manifesto }}</p>
          </div>

          <dl class="mt-5 grid gap-3 text-sm sm:grid-cols-2">
            <div><dt class="text-xs font-bold text-ink-500 uppercase">Submitted</dt><dd class="mt-1 font-semibold">{{ formatDateTime(nomination.submittedAt) }}</dd></div>
            <div><dt class="text-xs font-bold text-ink-500 uppercase">Reviewed</dt><dd class="mt-1 font-semibold">{{ nomination.reviewedAt ? formatDateTime(nomination.reviewedAt) : 'Awaiting review' }}</dd></div>
          </dl>
          <p v-if="nomination.rejectionReason" class="mt-4 rounded-xl bg-red-50 p-3 text-xs text-red-800">Rejection reason: {{ nomination.rejectionReason }}</p>
        </section>

        <aside class="space-y-4">
          <section class="rounded-2xl bg-brand-950 p-6 text-white">
            <FileText class="h-6 w-6 text-gold-300" />
            <h2 class="mt-3 font-display text-2xl">Campaign responsibly.</h2>
            <p class="mt-2 text-sm leading-6 text-brand-100">
              Approved candidates cannot access voter data or cast ballots on behalf of students. Your manifesto and photo
              are public to eligible voters in this election.
            </p>
          </section>
          <section class="surface p-5">
            <p class="eyebrow">Campaign checklist</p>
            <ul class="mt-3 space-y-3 text-xs leading-5 text-ink-600">
              <li class="flex gap-2"><CheckCircle2 class="h-4 w-4 shrink-0 text-brand-700" /> Submit a complete, accurate manifesto.</li>
              <li class="flex gap-2"><CheckCircle2 class="h-4 w-4 shrink-0 text-brand-700" /> Use a clear candidate photo under 2 MB.</li>
              <li class="flex gap-2"><CheckCircle2 class="h-4 w-4 shrink-0 text-brand-700" /> Watch for the officer's approval decision.</li>
            </ul>
          </section>
        </aside>
      </div>

      <div v-else class="grid gap-5 lg:grid-cols-[1.1fr_0.9fr]">
        <section class="surface p-5 sm:p-6">
          <div class="flex items-center gap-2">
            <UserRound class="h-5 w-5 text-brand-700" />
            <p class="eyebrow">Nomination form</p>
          </div>
          <h2 class="mt-2 font-display text-3xl">Put your name forward.</h2>
          <p class="mt-2 text-sm leading-6 text-ink-500">
            You must be eligible for the election. The electoral officer reviews every nomination before it appears on the ballot.
          </p>
          <div v-if="eligibleElections.length === 0" class="mt-5">
            <EmptyState title="No open nomination window" description="No election currently accepts new nominations for your student register." compact />
          </div>
          <div v-else class="mt-5 space-y-4">
            <div>
              <label for="election" class="field-label">Election</label>
              <select id="election" v-model="selectedElectionId" class="field" @change="selectElection">
                <option value="">Select an election</option>
                <option v-for="election in eligibleElections" :key="election.id" :value="election.id">
                  {{ election.department }} · {{ election.academicSession }} · {{ election.title }}
                </option>
              </select>
            </div>
            <div v-if="selectedElection">
              <label for="position" class="field-label">Position</label>
              <select id="position" v-model="selectedPositionId" class="field">
                <option v-for="position in positions" :key="position.id" :value="position.id">{{ position.title }}</option>
              </select>
            </div>
            <div>
              <label for="manifesto" class="field-label">Manifesto</label>
              <textarea id="manifesto" v-model="manifesto" class="field min-h-40" placeholder="Why should students vote for you? Be specific and constructive." />
              <p class="mt-1.5 text-[11px] text-ink-500">Minimum 20 characters. Avoid unverifiable claims or personal attacks.</p>
            </div>
            <AppButton :loading="busy" @click="nominate">Submit nomination</AppButton>
          </div>
        </section>
        <aside class="rounded-2xl bg-brand-950 p-6 text-white">
          <Megaphone class="h-6 w-6 text-gold-300" />
          <h2 class="mt-4 font-display text-3xl">Your campaign is part of the record.</h2>
          <p class="mt-3 text-sm leading-6 text-brand-100">
            Nominations, approval decisions and withdrawals are auditable. Once the ballot opens, approved candidate details
            are frozen to protect the integrity of the vote.
          </p>
          <RouterLink to="/candidate" class="mt-6 inline-flex items-center gap-2 text-sm font-bold text-gold-200">
            Read the rules <ArrowRight class="h-4 w-4" />
          </RouterLink>
        </aside>
      </div>

      <section v-if="dashboard?.announcements.length" class="surface p-5">
        <p class="eyebrow">Candidate notices</p>
        <div class="mt-3 space-y-3">
          <article v-for="announcement in dashboard.announcements" :key="announcement.id" class="border-b border-ink-500/10 pb-3 last:border-0">
            <p class="text-sm font-semibold">{{ announcement.title }}</p>
            <p class="mt-1 text-xs text-ink-500">{{ formatDateTime(announcement.publishedAt) }}</p>
            <p class="mt-1 text-xs leading-5 text-ink-600">{{ announcement.body }}</p>
          </article>
        </div>
      </section>
    </template>
  </div>

  <ModalDialog :open="editOpen" title="Edit nomination profile" description="Saving resets the nomination to pending officer review." @close="editOpen = false">
    <div v-if="nomination" class="space-y-3">
      <label class="field-label" for="edit-name">Display name</label>
      <input id="edit-name" v-model="editName" class="field" />
      <label class="field-label" for="edit-manifesto">Manifesto</label>
      <textarea id="edit-manifesto" v-model="editManifesto" class="field min-h-40" />
      <div class="flex justify-end gap-2"><AppButton variant="secondary" @click="editOpen = false">Cancel</AppButton><AppButton :loading="busy" @click="updateProfile">Save and resubmit</AppButton></div>
    </div>
  </ModalDialog>

  <ModalDialog :open="withdrawOpen" title="Withdraw nomination" description="This action is preserved in the audit trail." @close="withdrawOpen = false">
    <label class="field-label" for="withdraw-reason">Reason</label>
    <textarea id="withdraw-reason" v-model="withdrawReason" class="field min-h-24" placeholder="Why are you withdrawing?" />
    <div class="mt-4 flex justify-end gap-2"><AppButton variant="secondary" @click="withdrawOpen = false">Keep nomination</AppButton><AppButton variant="danger" :loading="busy" @click="withdraw">Withdraw</AppButton></div>
  </ModalDialog>
</template>
