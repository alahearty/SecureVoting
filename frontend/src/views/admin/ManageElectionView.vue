<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import {
  ArrowLeft,
  ArrowRight,
  Ban,
  CalendarClock,
  ClipboardCopy,
  FileUp,
  Lock,
  Plus,
  RefreshCw,
  ShieldCheck,
  UserCheck,
  X
} from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type {
  Announcement,
  CandidateAdmin,
  CreatePositionInput,
  Department,
  ElectionDetail,
  ElectionSummary,
  EligibleVoter,
  VoterImportResponse
} from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import ModalDialog from '@/components/ModalDialog.vue'
import PageHeader from '@/components/PageHeader.vue'
import StatusBadge from '@/components/StatusBadge.vue'
import { useConfirmStore } from '@/stores/confirm'
import { useToastStore } from '@/stores/toasts'
import { formatDateTime, toIsoLocal } from '@/utils/format'

type Tab = 'overview' | 'positions' | 'candidates' | 'voters' | 'announcements'
type LifecycleAction = 'schedule' | 'open' | 'close' | 'suspend' | 'resume' | 'cancel'

const route = useRoute()
const router = useRouter()
const confirm = useConfirmStore()
const toasts = useToastStore()
const electionId = computed(() => String(route.params.id))
const detail = ref<ElectionDetail | null>(null)
const summary = ref<ElectionSummary | null>(null)
const candidates = ref<CandidateAdmin[]>([])
const voters = ref<EligibleVoter[]>([])
const departments = ref<Department[]>([])
const announcements = ref<Announcement[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const activeTab = ref<Tab>('overview')
const actionName = ref<LifecycleAction | null>(null)
const actionReason = ref('')
const candidateDecision = ref<{ candidate: CandidateAdmin; action: 'approve' | 'reject' | 'disqualify' } | null>(null)
const candidateReason = ref('')
const positionOpen = ref(false)
const positionForm = reactive<CreatePositionInput>({
  title: '',
  description: '',
  displayOrder: 1,
  seatCount: 1,
  maxSelections: 1
})
const importFile = ref<File | null>(null)
const importResult = ref<VoterImportResponse | null>(null)
const announcementForm = reactive({
  title: '',
  body: '',
  audience: 'EligibleVoters'
})
const editOpen = ref(false)
const editForm = reactive({
  title: '',
  description: '',
  academicSession: '',
  departmentId: '',
  nominationOpensAt: '',
  nominationClosesAt: '',
  opensAt: '',
  closesAt: '',
  allowAbstention: true,
  tieHandling: 'RunoffRequired',
  nominationRequiresApproval: true
})

const tabs: Array<{ key: Tab; label: string }> = [
  { key: 'overview', label: 'Overview' },
  { key: 'positions', label: 'Positions' },
  { key: 'candidates', label: 'Candidates' },
  { key: 'voters', label: 'Voter register' },
  { key: 'announcements', label: 'Announcements' }
]

const pendingCandidates = computed(() => candidates.value.filter((candidate) => candidate.status === 'Pending'))
const canEditStructure = computed(() => detail.value?.status === 'Draft')
const canReview = computed(() => detail.value?.status === 'Closed')
const canOpen = computed(() => detail.value?.status === 'Scheduled')
const canClose = computed(() => detail.value?.status === 'Open')
const canSuspend = computed(() => detail.value?.status === 'Scheduled' || detail.value?.status === 'Open')
const canCancel = computed(() =>
  ['Draft', 'Scheduled', 'Open', 'Suspended'].includes(detail.value?.status ?? '')
)
const canResume = computed(() => detail.value?.status === 'Suspended')
const actionNeedsReason = computed(() =>
  actionName.value ? ['close', 'suspend', 'cancel'].includes(actionName.value) : false
)

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const [electionDetail, elections, candidateList, voterList, announcementList, departmentList] = await Promise.all([
      api<ElectionDetail>(`/admin/elections/${electionId.value}`),
      api<ElectionSummary[]>('/admin/elections'),
      api<CandidateAdmin[]>(`/admin/elections/${electionId.value}/candidates`),
      api<EligibleVoter[]>(`/admin/elections/${electionId.value}/voters`),
      api<Announcement[]>('/announcements'),
      api<Department[]>('/departments')
    ])
    detail.value = electionDetail
    summary.value = elections.find((election) => election.id === electionId.value) ?? null
    candidates.value = candidateList
    voters.value = voterList
    departments.value = departmentList
    announcements.value = announcementList.filter((announcement) => announcement.electionId === electionId.value)
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Election workspace could not be loaded.'
  } finally {
    loading.value = false
  }
}

function openEdit(): void {
  if (!detail.value) return
  editForm.title = detail.value.title
  editForm.description = detail.value.description
  editForm.academicSession = detail.value.academicSession
  editForm.departmentId = detail.value.departmentId
  editForm.nominationOpensAt = toIsoLocal(detail.value.nominationOpensAt)
  editForm.nominationClosesAt = toIsoLocal(detail.value.nominationClosesAt)
  editForm.opensAt = toIsoLocal(detail.value.opensAt)
  editForm.closesAt = toIsoLocal(detail.value.closesAt)
  editForm.allowAbstention = detail.value.rule.allowAbstention
  editForm.tieHandling = detail.value.rule.tieHandling
  editForm.nominationRequiresApproval = detail.value.rule.nominationRequiresApproval
  editOpen.value = true
}

async function saveEdit(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    await api(`/admin/elections/${electionId.value}`, {
      method: 'PUT',
      body: {
        ...editForm,
        nominationOpensAt: new Date(editForm.nominationOpensAt).toISOString(),
        nominationClosesAt: new Date(editForm.nominationClosesAt).toISOString(),
        opensAt: new Date(editForm.opensAt).toISOString(),
        closesAt: new Date(editForm.closesAt).toISOString()
      }
    })
    toasts.success('Draft election updated.')
    editOpen.value = false
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The draft could not be updated.'
  } finally {
    busy.value = false
  }
}

function openAction(action: LifecycleAction): void {
  actionName.value = action
  actionReason.value = ''
}

async function performAction(): Promise<void> {
  if (!actionName.value) return
  if (actionNeedsReason.value && actionReason.value.trim().length < 5) {
    error.value = 'Add a short reason before continuing.'
    return
  }

  const action = actionName.value
  const labels: Record<LifecycleAction, string> = {
    schedule: 'Schedule this election?',
    open: 'Open voting now?',
    close: 'Close voting?',
    suspend: 'Suspend this election?',
    resume: 'Resume this election?',
    cancel: 'Cancel this election?'
  }
  const confirmed = await confirm.ask({
    title: labels[action],
    message:
      action === 'open'
        ? 'The trusted server clock must be inside the voting window. Ballots will be accepted only while the election is open.'
        : action === 'cancel'
          ? 'This is a terminal action. Election evidence and audit history are preserved.'
          : 'This action is recorded in the tamper-evident audit trail.',
    confirmLabel: 'Continue',
    tone: action === 'cancel' ? 'danger' : 'default'
  })
  if (!confirmed) return

  busy.value = true
  error.value = ''
  try {
    await api(`/admin/elections/${electionId.value}/actions/${action}`, {
      method: 'POST',
      body: { reason: actionReason.value }
    })
    toasts.success(`Election ${action}d.`)
    actionName.value = null
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The election action could not be completed.'
  } finally {
    busy.value = false
  }
}

async function reviewResults(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    await api(`/elections/${electionId.value}/results/review`, { method: 'POST' })
    toasts.success('Provisional result generated.')
    await router.push({ name: 'admin-result-detail', params: { id: electionId.value } })
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Result review could not be started.'
  } finally {
    busy.value = false
  }
}

async function createPosition(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    await api(`/admin/elections/${electionId.value}/positions`, {
      method: 'POST',
      body: positionForm
    })
    toasts.success('Position added.')
    positionOpen.value = false
    positionForm.title = ''
    positionForm.description = ''
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The position could not be added.'
  } finally {
    busy.value = false
  }
}

function selectCandidate(candidate: CandidateAdmin, action: 'approve' | 'reject' | 'disqualify'): void {
  candidateDecision.value = { candidate, action }
  candidateReason.value = ''
}

async function applyCandidateDecision(): Promise<void> {
  if (!candidateDecision.value) return
  const { candidate, action } = candidateDecision.value
  if (action !== 'approve' && candidateReason.value.trim().length < 5) {
    error.value = 'Add a reason for this decision.'
    return
  }

  busy.value = true
  error.value = ''
  try {
    const endpoints = {
      approve: `/admin/elections/${electionId.value}/candidates/${candidate.id}/approve`,
      reject: `/admin/elections/${electionId.value}/candidates/${candidate.id}/reject`,
      disqualify: `/admin/elections/${electionId.value}/candidates/${candidate.id}/disqualify`
    }
    await api(endpoints[action], {
      method: 'POST',
      body: action === 'approve' ? {} : { reason: candidateReason.value }
    })
    toasts.success(`Candidate ${action === 'approve' ? 'approved' : action + 'ed'}.`)
    candidateDecision.value = null
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The candidate decision could not be completed.'
  } finally {
    busy.value = false
  }
}

function onFileSelected(event: Event): void {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] ?? null
  importResult.value = null
}

async function importVoters(): Promise<void> {
  if (!importFile.value) {
    error.value = 'Choose a CSV file first.'
    return
  }

  busy.value = true
  error.value = ''
  try {
    const form = new FormData()
    form.append('file', importFile.value)
    importResult.value = await api<VoterImportResponse>(
      `/admin/elections/${electionId.value}/voters/import`,
      { method: 'POST', body: form }
    )
    toasts.success('Voter register processed.')
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The voter register could not be imported.'
  } finally {
    busy.value = false
  }
}

async function copyToken(token: string): Promise<void> {
  await navigator.clipboard.writeText(token)
  toasts.success('Activation token copied. Share it only through an approved channel.')
}

async function createAnnouncement(): Promise<void> {
  if (announcementForm.title.trim().length < 3 || announcementForm.body.trim().length < 5) {
    error.value = 'Add an announcement title and body.'
    return
  }
  busy.value = true
  try {
    await api('/admin/announcements', {
      method: 'POST',
      body: {
        title: announcementForm.title,
        body: announcementForm.body,
        audience: announcementForm.audience,
        electionId: electionId.value
      }
    })
    toasts.success('Announcement published.')
    announcementForm.title = ''
    announcementForm.body = ''
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The announcement could not be published.'
  } finally {
    busy.value = false
  }
}

function cancelAction(): void {
  actionName.value = null
  actionReason.value = ''
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <RouterLink to="/admin/elections" class="inline-flex items-center gap-2 text-xs font-bold text-brand-700">
      <ArrowLeft class="h-4 w-4" /> All elections
    </RouterLink>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="5" />

    <template v-else-if="detail && summary">
      <PageHeader eyebrow="Election workspace" :title="detail.title" :description="`${detail.academicSession} · ${detail.department} (${detail.departmentCode})`">
        <template #actions>
          <StatusBadge :status="detail.status" />
          <AppButton variant="secondary" @click="load"><RefreshCw class="h-4 w-4" /> Refresh</AppButton>
          <RouterLink v-if="canReview" :to="{ name: 'admin-result-detail', params: { id: electionId } }">
            <AppButton variant="gold"><ShieldCheck class="h-4 w-4" /> Result review</AppButton>
          </RouterLink>
        </template>
      </PageHeader>

      <div class="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <div class="surface p-4"><p class="text-[10px] font-bold tracking-wider text-ink-500 uppercase">Eligible</p><p class="mt-1 font-display text-2xl">{{ summary.eligibleVoters }}</p></div>
        <div class="surface p-4"><p class="text-[10px] font-bold tracking-wider text-ink-500 uppercase">Accepted ballots</p><p class="mt-1 font-display text-2xl">{{ summary.acceptedBallots }}</p></div>
        <div class="surface p-4"><p class="text-[10px] font-bold tracking-wider text-ink-500 uppercase">Positions</p><p class="mt-1 font-display text-2xl">{{ detail.positions.length }}</p></div>
        <div class="surface p-4"><p class="text-[10px] font-bold tracking-wider text-ink-500 uppercase">Pending nominations</p><p class="mt-1 font-display text-2xl text-gold-600">{{ pendingCandidates.length }}</p></div>
      </div>

      <div class="flex gap-1 overflow-x-auto border-b border-ink-500/10 pb-px">
        <button
          v-for="tab in tabs"
          :key="tab.key"
          type="button"
          class="focus-ring border-b-2 px-3 py-3 text-xs font-bold whitespace-nowrap"
          :class="activeTab === tab.key ? 'border-brand-700 text-brand-800' : 'border-transparent text-ink-500 hover:text-ink-900'"
          @click="activeTab = tab.key"
        >
          {{ tab.label }}
          <span v-if="tab.key === 'candidates' && pendingCandidates.length" class="ml-1 rounded-full bg-gold-100 px-1.5 py-0.5 text-[10px] text-gold-800">{{ pendingCandidates.length }}</span>
        </button>
      </div>

      <div v-if="activeTab === 'overview'" class="grid gap-5 lg:grid-cols-[1.2fr_1fr]">
        <section class="surface p-5 sm:p-6">
          <p class="eyebrow">Controlled lifecycle</p>
          <h2 class="mt-1 font-display text-2xl">Move the election forward</h2>
          <div class="mt-5 flex flex-wrap gap-2">
            <AppButton v-if="detail.status === 'Draft'" variant="secondary" @click="openEdit">Edit draft</AppButton>
            <AppButton v-if="detail.status === 'Draft'" :loading="busy" @click="openAction('schedule')">
              <CalendarClock class="h-4 w-4" /> Schedule
            </AppButton>
            <AppButton v-if="canOpen" :loading="busy" @click="openAction('open')">
              <Lock class="h-4 w-4" /> Open voting
            </AppButton>
            <AppButton v-if="canClose" :loading="busy" @click="openAction('close')">
              <Ban class="h-4 w-4" /> Close voting
            </AppButton>
            <AppButton v-if="canSuspend" variant="secondary" :loading="busy" @click="openAction('suspend')">Suspend</AppButton>
            <AppButton v-if="canResume" variant="secondary" :loading="busy" @click="openAction('resume')">Resume</AppButton>
            <AppButton v-if="canCancel" variant="danger" :loading="busy" @click="openAction('cancel')">Cancel</AppButton>
            <AppButton v-if="canReview" :loading="busy" @click="reviewResults">Send for result review</AppButton>
            <RouterLink v-if="detail.status === 'UnderReview' || detail.status === 'Published'" :to="{ name: 'admin-result-detail', params: { id: electionId } }">
              <AppButton variant="secondary">Open result <ArrowRight class="h-4 w-4" /></AppButton>
            </RouterLink>
          </div>
          <div v-if="detail.suspensionReason" class="mt-4 rounded-xl bg-orange-50 p-3 text-xs text-orange-800">Suspended: {{ detail.suspensionReason }}</div>
          <div v-if="detail.cancellationReason" class="mt-4 rounded-xl bg-red-50 p-3 text-xs text-red-800">Cancelled: {{ detail.cancellationReason }}</div>
        </section>

        <section class="surface p-5 sm:p-6">
          <p class="eyebrow">Schedule</p>
          <dl class="mt-4 space-y-3 text-sm">
            <div class="flex justify-between gap-3"><dt class="text-ink-500">Nominations</dt><dd class="text-right font-semibold">{{ formatDateTime(detail.nominationOpensAt) }} → {{ formatDateTime(detail.nominationClosesAt) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-ink-500">Voting</dt><dd class="text-right font-semibold">{{ formatDateTime(detail.opensAt) }} → {{ formatDateTime(detail.closesAt) }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-ink-500">Tie handling</dt><dd class="font-semibold">{{ detail.rule.tieHandling.replace(/([a-z])([A-Z])/g, '$1 $2') }}</dd></div>
            <div class="flex justify-between gap-3"><dt class="text-ink-500">Abstention</dt><dd class="font-semibold">{{ detail.rule.allowAbstention ? 'Allowed' : 'Not allowed' }}</dd></div>
          </dl>
        </section>
      </div>

      <div v-else-if="activeTab === 'positions'" class="space-y-4">
        <div class="flex items-center justify-between gap-3">
          <div><p class="eyebrow">Structure</p><h2 class="mt-1 font-display text-2xl">Positions</h2></div>
          <AppButton v-if="canEditStructure" @click="positionOpen = true"><Plus class="h-4 w-4" /> Add position</AppButton>
        </div>
        <EmptyState v-if="detail.positions.length === 0" title="No positions yet" description="Add at least one position before scheduling." compact />
        <div v-else class="grid gap-4 md:grid-cols-2">
          <article v-for="position in detail.positions" :key="position.id" class="surface p-5">
            <div class="flex items-start justify-between gap-3">
              <div><h3 class="font-display text-2xl">{{ position.title }}</h3><p class="mt-1 text-xs text-ink-500">{{ position.candidates.length }} approved candidate(s)</p></div>
              <span class="rounded-full bg-brand-50 px-2 py-1 text-[10px] font-bold text-brand-800">{{ position.seatCount }} seat(s)</span>
            </div>
            <p class="mt-3 text-sm leading-6 text-ink-500">{{ position.description }}</p>
            <p class="mt-3 text-xs font-semibold text-ink-700">Maximum selections: {{ position.maxSelections }}</p>
          </article>
        </div>
      </div>

      <div v-else-if="activeTab === 'candidates'" class="space-y-4">
        <div><p class="eyebrow">Nominations</p><h2 class="mt-1 font-display text-2xl">Candidate approval</h2></div>
        <div v-if="candidates.length === 0" class="surface p-6 text-sm text-ink-500">No nominations have been submitted.</div>
        <div v-else class="surface overflow-hidden">
          <div class="divide-y divide-ink-500/10">
            <div v-for="candidate in candidates" :key="candidate.id" class="flex flex-col gap-3 p-5 sm:flex-row sm:items-center">
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-2">
                  <p class="font-semibold text-ink-950">{{ candidate.displayName }}</p>
                  <StatusBadge :status="candidate.status" />
                </div>
                <p class="mt-1 text-xs text-ink-500">{{ candidate.matricNumber }} · {{ candidate.positionTitle }}</p>
                <p class="mt-2 line-clamp-2 text-xs leading-5 text-ink-500">{{ candidate.manifesto }}</p>
              </div>
              <div class="flex flex-wrap gap-2">
                <AppButton v-if="candidate.status === 'Pending'" size="sm" :loading="busy" @click="selectCandidate(candidate, 'approve')"><UserCheck class="h-4 w-4" /> Approve</AppButton>
                <AppButton v-if="candidate.status === 'Pending'" variant="secondary" size="sm" :loading="busy" @click="selectCandidate(candidate, 'reject')"><X class="h-4 w-4" /> Reject</AppButton>
                <AppButton v-if="candidate.status === 'Approved'" variant="danger" size="sm" :loading="busy" @click="selectCandidate(candidate, 'disqualify')"><Ban class="h-4 w-4" /> Disqualify</AppButton>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div v-else-if="activeTab === 'voters'" class="space-y-4">
        <div><p class="eyebrow">Eligibility</p><h2 class="mt-1 font-display text-2xl">Voter register</h2></div>
        <section class="surface p-5">
          <div class="flex flex-col gap-3 sm:flex-row sm:items-end">
            <label class="flex-1">
              <span class="field-label">Import register CSV</span>
              <input type="file" accept=".csv,text/csv" class="field file:mr-3 file:rounded-lg file:border-0 file:bg-brand-50 file:px-3 file:py-1.5 file:text-xs file:font-bold file:text-brand-800" @change="onFileSelected" />
            </label>
            <AppButton :loading="busy" :disabled="!importFile" @click="importVoters"><FileUp class="h-4 w-4" /> Import</AppButton>
          </div>
          <p class="mt-2 text-[11px] leading-5 text-ink-500">Columns: matric, fullname, email, level, department. The department value must match this election's department name or code. Up to 5,000 rows and 2 MB. Activation tokens are shown once after import.</p>
          <div v-if="importResult" class="mt-4 rounded-xl bg-paper p-4 text-xs text-ink-700">
            <p><strong>{{ importResult.rowsProcessed }}</strong> processed · {{ importResult.accountsCreated }} new accounts · {{ importResult.eligibleCreated }} new eligible records.</p>
            <p v-if="importResult.errors.length" class="mt-2 text-red-700">{{ importResult.errors.length }} row error(s).</p>
            <ul v-if="importResult.errors.length" class="mt-2 list-inside list-disc text-red-700">
              <li v-for="issue in importResult.errors.slice(0, 6)" :key="`${issue.rowNumber}-${issue.message}`">Row {{ issue.rowNumber }}: {{ issue.message }}</li>
            </ul>
            <div v-if="importResult.activationTokens.length" class="mt-4">
              <p class="font-bold text-ink-950">One-time activation tokens</p>
              <p class="mt-1 text-[11px] text-ink-500">Distribute through an approved secure channel. These values are not stored in plaintext.</p>
              <div class="mt-2 space-y-2">
                <div v-for="credential in importResult.activationTokens" :key="credential.matricNumber" class="flex flex-wrap items-center gap-2 rounded-lg bg-white p-2">
                  <span class="font-semibold">{{ credential.matricNumber }}</span>
                  <code class="max-w-xs truncate text-[10px] text-ink-500">{{ credential.token }}</code>
                  <button type="button" class="focus-ring ml-auto rounded p-1 text-brand-700" aria-label="Copy activation token" @click="copyToken(credential.token)"><ClipboardCopy class="h-4 w-4" /></button>
                </div>
              </div>
            </div>
          </div>
        </section>
        <div class="surface overflow-hidden">
          <div class="hidden grid-cols-[1.2fr_1fr_0.7fr_0.7fr] gap-3 border-b border-ink-500/10 px-5 py-3 text-[10px] font-bold tracking-wide text-ink-500 uppercase sm:grid"><span>Student</span><span>Email</span><span>Level</span><span>Participation</span></div>
          <div class="divide-y divide-ink-500/10">
            <div v-for="voter in voters" :key="voter.studentId" class="grid gap-1 px-5 py-3 text-sm sm:grid-cols-[1.2fr_1fr_0.7fr_0.7fr] sm:items-center">
              <div><p class="font-semibold text-ink-950">{{ voter.fullName }}</p><p class="text-xs text-ink-500">{{ voter.matricNumber }}</p></div>
              <p class="truncate text-xs text-ink-600">{{ voter.email }}</p>
              <p class="text-xs text-ink-600">{{ voter.level }}</p>
              <p class="text-xs font-semibold" :class="voter.hasVoted ? 'text-emerald-700' : 'text-ink-500'">{{ voter.hasVoted ? `Voted ${formatDateTime(voter.votedAt)}` : 'Not voted' }}</p>
            </div>
          </div>
          <p v-if="voters.length === 0" class="p-5 text-sm text-ink-500">No eligible voters imported.</p>
        </div>
      </div>

      <div v-else class="grid gap-5 lg:grid-cols-[1fr_1fr]">
        <section class="surface p-5">
          <p class="eyebrow">Notice board</p>
          <h2 class="mt-1 font-display text-2xl">Publish an announcement</h2>
          <div class="mt-4 space-y-3">
            <input v-model="announcementForm.title" class="field" placeholder="Announcement title" />
            <textarea v-model="announcementForm.body" class="field min-h-32" placeholder="Message to the selected audience" />
            <select v-model="announcementForm.audience" class="field">
              <option value="EligibleVoters">Eligible voters</option>
              <option value="Candidates">Candidates</option>
              <option value="Observers">Observers</option>
              <option value="Everyone">Everyone</option>
            </select>
            <AppButton :loading="busy" @click="createAnnouncement">Publish notice</AppButton>
          </div>
        </section>
        <section class="surface p-5">
          <p class="eyebrow">Published</p>
          <h2 class="mt-1 font-display text-2xl">This election</h2>
          <div class="mt-4 space-y-4">
            <article v-for="announcement in announcements" :key="announcement.id" class="border-b border-ink-500/10 pb-3">
              <p class="text-sm font-semibold text-ink-950">{{ announcement.title }}</p>
              <p class="mt-1 text-xs text-ink-500">{{ formatDateTime(announcement.publishedAt) }}</p>
              <p class="mt-1 text-xs leading-5 text-ink-600">{{ announcement.body }}</p>
            </article>
            <p v-if="announcements.length === 0" class="text-sm text-ink-500">No announcements for this election.</p>
          </div>
        </section>
      </div>
    </template>
  </div>

  <ModalDialog :open="Boolean(actionName)" :title="actionName ? `${actionName} election` : 'Election action'" description="This action is audited and enforced by the server." @close="cancelAction">
    <label v-if="actionNeedsReason" for="action-reason" class="field-label">Reason</label>
    <textarea v-if="actionNeedsReason" id="action-reason" v-model="actionReason" class="field min-h-24" placeholder="Document the authorised reason" />
    <div class="mt-4 flex justify-end gap-2">
      <AppButton variant="secondary" @click="cancelAction">Cancel</AppButton>
      <AppButton :variant="actionName === 'cancel' ? 'danger' : 'primary'" :loading="busy" @click="performAction">Continue</AppButton>
    </div>
  </ModalDialog>

  <ModalDialog :open="editOpen" title="Edit draft election" description="Only draft elections can be changed through this form." max-width="max-w-2xl" @close="editOpen = false">
    <div class="space-y-4">
      <input v-model="editForm.title" class="field" placeholder="Election title" />
      <textarea v-model="editForm.description" class="field min-h-20" placeholder="Description" />
      <div class="grid gap-3 sm:grid-cols-2">
        <label><span class="field-label">Academic session</span><input v-model="editForm.academicSession" class="field" /></label>
        <label>
          <span class="field-label">Department</span>
          <select v-model="editForm.departmentId" class="field">
            <option v-for="department in departments" :key="department.id" :value="department.id">
              {{ department.name }} ({{ department.code }})
            </option>
          </select>
        </label>
        <label><span class="field-label">Nominations open</span><input v-model="editForm.nominationOpensAt" type="datetime-local" class="field" /></label>
        <label><span class="field-label">Nominations close</span><input v-model="editForm.nominationClosesAt" type="datetime-local" class="field" /></label>
        <label><span class="field-label">Voting opens</span><input v-model="editForm.opensAt" type="datetime-local" class="field" /></label>
        <label><span class="field-label">Voting closes</span><input v-model="editForm.closesAt" type="datetime-local" class="field" /></label>
      </div>
      <div class="grid gap-3 sm:grid-cols-2">
        <label><span class="field-label">Tie handling</span><select v-model="editForm.tieHandling" class="field"><option value="RunoffRequired">Runoff required</option><option value="SharedOffice">Shared office</option><option value="ManualResolution">Manual resolution</option></select></label>
        <label class="flex items-center gap-2 pt-7 text-sm font-semibold"><input v-model="editForm.allowAbstention" type="checkbox" class="h-4 w-4 accent-brand-700" /> Allow abstention</label>
      </div>
      <div class="flex justify-end gap-2"><AppButton variant="secondary" @click="editOpen = false">Cancel</AppButton><AppButton :loading="busy" @click="saveEdit">Save draft</AppButton></div>
    </div>
  </ModalDialog>

  <ModalDialog :open="positionOpen" title="Add a position" description="Positions can only be changed while the election is a draft." @close="positionOpen = false">
    <div class="space-y-3">
      <input v-model="positionForm.title" class="field" placeholder="Position title" />
      <textarea v-model="positionForm.description" class="field min-h-20" placeholder="Position description" />
      <div class="grid grid-cols-3 gap-3">
        <label><span class="field-label">Order</span><input v-model.number="positionForm.displayOrder" type="number" min="0" class="field" /></label>
        <label><span class="field-label">Seats</span><input v-model.number="positionForm.seatCount" type="number" min="1" class="field" /></label>
        <label><span class="field-label">Max choices</span><input v-model.number="positionForm.maxSelections" type="number" min="1" class="field" /></label>
      </div>
      <div class="flex justify-end gap-2"><AppButton variant="secondary" @click="positionOpen = false">Cancel</AppButton><AppButton :loading="busy" @click="createPosition">Add position</AppButton></div>
    </div>
  </ModalDialog>

  <ModalDialog :open="Boolean(candidateDecision)" :title="candidateDecision ? `Candidate ${candidateDecision.action}` : 'Candidate decision'" description="A reason is required for rejection or disqualification." @close="candidateDecision = null">
    <div v-if="candidateDecision" class="space-y-3">
      <p class="text-sm text-ink-600">{{ candidateDecision.candidate.displayName }} · {{ candidateDecision.candidate.positionTitle }}</p>
      <textarea v-if="candidateDecision.action !== 'approve'" v-model="candidateReason" class="field min-h-24" placeholder="Reason for the decision" />
      <div class="flex justify-end gap-2"><AppButton variant="secondary" @click="candidateDecision = null">Cancel</AppButton><AppButton :variant="candidateDecision.action === 'approve' ? 'primary' : 'danger'" :loading="busy" @click="applyCandidateDecision">Confirm</AppButton></div>
    </div>
  </ModalDialog>
</template>
