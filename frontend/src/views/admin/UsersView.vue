<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { Building2, Search, ShieldCheck, UserCog, UserX } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type {
  Department,
  DepartmentAssignment,
  DepartmentAssignmentInput,
  Role,
  UserSummary
} from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import LoadingState from '@/components/LoadingState.vue'
import ModalDialog from '@/components/ModalDialog.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useConfirmStore } from '@/stores/confirm'
import { useToastStore } from '@/stores/toasts'
import { formatDate, roleLabel } from '@/utils/format'

const confirm = useConfirmStore()
const toasts = useToastStore()
const users = ref<UserSummary[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const search = ref('')
const selected = ref<UserSummary | null>(null)
const selectedRoles = ref<Role[]>([])
const reason = ref('')
const suspendTarget = ref<UserSummary | null>(null)
const assignmentTarget = ref<UserSummary | null>(null)
const departments = ref<Department[]>([])
const assignmentDraft = ref<Record<string, { officer: boolean; observer: boolean }>>({})
const assignmentsLoading = ref(false)

const availableRoles: Role[] = ['SuperAdministrator', 'ElectoralOfficer', 'Student', 'Candidate', 'Observer']

async function load(): Promise<void> {
  loading.value = true
  try {
    users.value = await api<UserSummary[]>(`/admin/users?search=${encodeURIComponent(search.value)}`)
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'User accounts could not be loaded.'
  } finally {
    loading.value = false
  }
}

function openRoles(user: UserSummary): void {
  selected.value = user
  selectedRoles.value = [...user.roles]
  reason.value = ''
}

function toggleRole(role: Role): void {
  selectedRoles.value = selectedRoles.value.includes(role)
    ? selectedRoles.value.filter((item) => item !== role)
    : [...selectedRoles.value, role]
}

async function saveRoles(): Promise<void> {
  if (!selected.value) return
  busy.value = true
  try {
    await api(`/admin/users/${selected.value.id}/roles`, {
      method: 'PUT',
      body: { roles: selectedRoles.value }
    })
    toasts.success('Role assignment updated.')
    selected.value = null
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Roles could not be updated.'
  } finally {
    busy.value = false
  }
}

function openSuspend(user: UserSummary): void {
  suspendTarget.value = user
  reason.value = ''
}

async function openDepartments(user: UserSummary): Promise<void> {
  assignmentTarget.value = user
  assignmentsLoading.value = true
  error.value = ''
  try {
    const [departmentList, assignmentList] = await Promise.all([
      api<Department[]>('/admin/departments'),
      api<DepartmentAssignment[]>(`/admin/users/${user.id}/departments`)
    ])
    departments.value = departmentList
    const draft: Record<string, { officer: boolean; observer: boolean }> = {}
    for (const department of departmentList) {
      draft[department.id] = { officer: false, observer: false }
    }
    for (const assignment of assignmentList) {
      if (!draft[assignment.departmentId]) {
        draft[assignment.departmentId] = { officer: false, observer: false }
      }
      if (assignment.scope === 'ElectoralOfficer') draft[assignment.departmentId].officer = true
      if (assignment.scope === 'Observer') draft[assignment.departmentId].observer = true
    }
    assignmentDraft.value = draft
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Department assignments could not be loaded.'
  } finally {
    assignmentsLoading.value = false
  }
}

async function saveAssignments(): Promise<void> {
  if (!assignmentTarget.value) return
  busy.value = true
  try {
    const assignments: DepartmentAssignmentInput[] = []
    for (const [departmentId, scopes] of Object.entries(assignmentDraft.value)) {
      if (scopes.officer) assignments.push({ departmentId, scope: 'ElectoralOfficer' })
      if (scopes.observer) assignments.push({ departmentId, scope: 'Observer' })
    }
    await api(`/admin/users/${assignmentTarget.value.id}/departments`, {
      method: 'PUT',
      body: { assignments }
    })
    toasts.success('Department assignments updated.')
    assignmentTarget.value = null
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Department assignments could not be updated.'
  } finally {
    busy.value = false
  }
}

async function setActive(active: boolean): Promise<void> {
  if (!suspendTarget.value || reason.value.trim().length < 5) {
    error.value = 'Add a reason for the account status change.'
    return
  }
  const target = suspendTarget.value
  const confirmed = await confirm.ask({
    title: active ? 'Activate this account?' : 'Suspend this account?',
    message: active
      ? 'The user will be able to sign in again according to their roles.'
      : 'Active sessions will be invalidated at the next security-stamp check.',
    confirmLabel: active ? 'Activate' : 'Suspend',
    tone: active ? 'default' : 'danger'
  })
  if (!confirmed) return
  busy.value = true
  try {
    await api(`/admin/users/${target.id}/${active ? 'activate' : 'suspend'}`, {
      method: 'POST',
      body: { reason: reason.value }
    })
    toasts.success(active ? 'Account activated.' : 'Account suspended.')
    suspendTarget.value = null
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Account status could not be changed.'
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="Access control"
      title="User accounts"
      description="Assign only the roles required by the user's duties. Role changes are audited and invalidate active role claims."
    >
      <template #actions><AppButton variant="secondary" @click="load">Refresh</AppButton></template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <div class="surface flex items-center gap-2 p-4">
      <Search class="h-4 w-4 text-ink-500" />
      <input v-model="search" class="w-full bg-transparent text-sm outline-none" placeholder="Search name, email or matriculation" @keyup.enter="load" />
      <AppButton size="sm" variant="secondary" @click="load">Search</AppButton>
    </div>

    <LoadingState v-if="loading" :rows="4" />
    <div v-else class="surface overflow-hidden">
      <div class="divide-y divide-ink-500/10">
        <div v-for="user in users" :key="user.id" class="flex flex-col gap-3 p-5 lg:flex-row lg:items-center">
          <div class="min-w-0 flex-1">
            <div class="flex flex-wrap items-center gap-2">
              <p class="font-semibold text-ink-950">{{ user.displayName }}</p>
              <span class="rounded-full px-2 py-1 text-[10px] font-bold" :class="user.isActive ? 'bg-emerald-50 text-emerald-800' : 'bg-red-50 text-red-800'">
                {{ user.isActive ? 'Active' : 'Suspended' }}
              </span>
              <span v-if="user.isLockedOut" class="rounded-full bg-orange-50 px-2 py-1 text-[10px] font-bold text-orange-800">Locked out</span>
            </div>
            <p class="mt-1 text-xs text-ink-500">{{ user.email ?? 'No email' }} · {{ user.matricNumber ?? 'No student register link' }}</p>
            <div class="mt-2 flex flex-wrap gap-1">
              <span v-for="role in user.roles" :key="role" class="rounded-full bg-brand-50 px-2 py-1 text-[10px] font-bold text-brand-800">{{ roleLabel(role) }}</span>
              <span v-if="user.roles.length === 0" class="text-[11px] text-ink-500">No roles assigned</span>
            </div>
          </div>
          <p class="text-xs text-ink-500">Created {{ formatDate(user.createdAt) }}</p>
          <div class="flex flex-wrap gap-2">
            <AppButton variant="secondary" size="sm" @click="openRoles(user)"><UserCog class="h-4 w-4" /> Roles</AppButton>
            <AppButton variant="secondary" size="sm" @click="openDepartments(user)"><Building2 class="h-4 w-4" /> Departments</AppButton>
            <AppButton v-if="user.isActive" variant="secondary" size="sm" @click="openSuspend(user)"><UserX class="h-4 w-4" /> Suspend</AppButton>
            <AppButton v-else variant="secondary" size="sm" @click="openSuspend(user)"><ShieldCheck class="h-4 w-4" /> Activate</AppButton>
          </div>
        </div>
        <p v-if="users.length === 0" class="p-6 text-sm text-ink-500">No user accounts match this search.</p>
      </div>
    </div>
  </div>

  <ModalDialog :open="Boolean(selected)" title="Assign roles" description="Server-side policies enforce these role boundaries." @close="selected = null">
    <div v-if="selected" class="space-y-4">
      <p class="text-sm text-ink-600">{{ selected.displayName }}</p>
      <label v-for="role in availableRoles" :key="role" class="flex cursor-pointer items-center gap-3 rounded-xl border border-ink-500/10 p-3">
        <input type="checkbox" :checked="selectedRoles.includes(role)" class="h-4 w-4 accent-brand-700" @change="toggleRole(role)" />
        <span class="text-sm font-semibold">{{ roleLabel(role) }}</span>
      </label>
      <div class="flex justify-end gap-2"><AppButton variant="secondary" @click="selected = null">Cancel</AppButton><AppButton :loading="busy" @click="saveRoles">Save roles</AppButton></div>
    </div>
  </ModalDialog>

  <ModalDialog
    :open="Boolean(assignmentTarget)"
    title="Department access"
    description="Assign an officer or observer to the departments they may work within. Super Administrators retain university-wide oversight."
    @close="assignmentTarget = null"
  >
    <div class="space-y-4">
      <p v-if="assignmentTarget" class="text-sm text-ink-600">{{ assignmentTarget.displayName }}</p>
      <div v-if="assignmentsLoading" class="text-sm text-ink-500">Loading departments...</div>
      <div v-else-if="departments.length === 0" class="rounded-xl border border-amber-200 bg-amber-50 p-3 text-xs text-amber-800">
        No departments are configured.
      </div>
      <div v-else class="max-h-80 space-y-2 overflow-y-auto">
        <div v-for="department in departments" :key="department.id" class="rounded-xl border border-ink-500/10 p-3">
          <p class="text-sm font-semibold text-ink-950">
            {{ department.name }}
            <span class="font-mono text-[11px] text-ink-500">{{ department.code }}</span>
          </p>
          <div class="mt-2 flex flex-wrap gap-4 text-xs">
            <label class="flex items-center gap-2">
              <input v-model="assignmentDraft[department.id].officer" type="checkbox" class="h-4 w-4 accent-brand-700" />
              Electoral Officer
            </label>
            <label class="flex items-center gap-2">
              <input v-model="assignmentDraft[department.id].observer" type="checkbox" class="h-4 w-4 accent-brand-700" />
              Observer
            </label>
          </div>
        </div>
      </div>
      <div class="flex justify-end gap-2">
        <AppButton variant="secondary" @click="assignmentTarget = null">Cancel</AppButton>
        <AppButton :loading="busy" @click="saveAssignments">Save assignments</AppButton>
      </div>
    </div>
  </ModalDialog>

  <ModalDialog :open="Boolean(suspendTarget)" :title="suspendTarget?.isActive ? 'Suspend account' : 'Activate account'" description="A reason is required and recorded in the audit trail." @close="suspendTarget = null">
    <div v-if="suspendTarget" class="space-y-3">
      <p class="text-sm text-ink-600">{{ suspendTarget.displayName }}</p>
      <textarea v-model="reason" class="field min-h-24" placeholder="Reason" />
      <div class="flex justify-end gap-2">
        <AppButton variant="secondary" @click="suspendTarget = null">Cancel</AppButton>
        <AppButton :variant="suspendTarget.isActive ? 'danger' : 'primary'" :loading="busy" @click="setActive(!suspendTarget.isActive)">
          {{ suspendTarget.isActive ? 'Suspend' : 'Activate' }}
        </AppButton>
      </div>
    </div>
  </ModalDialog>
</template>
