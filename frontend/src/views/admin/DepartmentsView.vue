<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Building2, Plus, RefreshCw, Save } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { Department } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import EmptyState from '@/components/EmptyState.vue'
import LoadingState from '@/components/LoadingState.vue'
import ModalDialog from '@/components/ModalDialog.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useConfirmStore } from '@/stores/confirm'
import { useToastStore } from '@/stores/toasts'
import { formatDateTime } from '@/utils/format'

const confirm = useConfirmStore()
const toasts = useToastStore()
const departments = ref<Department[]>([])
const loading = ref(true)
const busy = ref(false)
const error = ref('')
const createOpen = ref(false)
const editing = ref<Department | null>(null)
const form = reactive({ name: '', code: '', faculty: '' })

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    departments.value = await api<Department[]>('/admin/departments')
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'Departments could not be loaded.'
  } finally {
    loading.value = false
  }
}

function openCreate(): void {
  form.name = ''
  form.code = ''
  form.faculty = ''
  createOpen.value = true
}

function openEdit(department: Department): void {
  form.name = department.name
  form.code = department.code
  form.faculty = department.faculty ?? ''
  editing.value = department
}

async function create(): Promise<void> {
  busy.value = true
  error.value = ''
  try {
    await api<Department>('/admin/departments', { method: 'POST', body: { ...form } })
    toasts.success('Department created.')
    createOpen.value = false
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The department could not be created.'
  } finally {
    busy.value = false
  }
}

async function save(): Promise<void> {
  if (!editing.value) return
  busy.value = true
  error.value = ''
  try {
    await api(`/admin/departments/${editing.value.id}`, { method: 'PUT', body: { ...form } })
    toasts.success('Department updated.')
    editing.value = null
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The department could not be updated.'
  } finally {
    busy.value = false
  }
}

async function setActive(department: Department, active: boolean): Promise<void> {
  const confirmed = await confirm.ask({
    title: active ? 'Activate this department?' : 'Deactivate this department?',
    message: active
      ? 'New elections and imports will be allowed for this department.'
      : 'Existing elections and historical records are preserved. New elections and imports will be blocked.',
    confirmLabel: active ? 'Activate' : 'Deactivate',
    tone: active ? 'default' : 'danger'
  })
  if (!confirmed) return
  busy.value = true
  try {
    await api(`/admin/departments/${department.id}/${active ? 'activate' : 'deactivate'}`, { method: 'POST' })
    toasts.success(active ? 'Department activated.' : 'Department deactivated.')
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The department status could not be changed.'
  } finally {
    busy.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="University configuration"
      title="Departments"
      description="Manage the university departments that may hold independent departmental elections. Deactivation preserves history."
    >
      <template #actions>
        <AppButton variant="secondary" @click="load"><RefreshCw class="h-4 w-4" /> Refresh</AppButton>
        <AppButton @click="openCreate"><Plus class="h-4 w-4" /> Add department</AppButton>
      </template>
    </PageHeader>

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>

    <LoadingState v-if="loading" :rows="4" />
    <EmptyState
      v-else-if="departments.length === 0"
      title="No departments configured"
      description="Create the first department before creating departmental elections."
    >
      <AppButton class="mt-4" @click="openCreate"><Plus class="h-4 w-4" /> Add department</AppButton>
    </EmptyState>

    <div v-else class="surface overflow-hidden">
      <div class="hidden grid-cols-[1.5fr_0.7fr_1fr_0.7fr_0.9fr] gap-4 border-b border-ink-500/10 px-5 py-3 text-[10px] font-bold tracking-wide text-ink-500 uppercase md:grid">
        <span>Department</span>
        <span>Code</span>
        <span>Faculty</span>
        <span>Status</span>
        <span class="text-right">Actions</span>
      </div>
      <div class="divide-y divide-ink-500/10">
        <div v-for="department in departments" :key="department.id" class="grid gap-3 px-5 py-4 md:grid-cols-[1.5fr_0.7fr_1fr_0.7fr_0.9fr] md:items-center">
          <div class="flex items-center gap-3">
            <span class="grid h-9 w-9 place-items-center rounded-xl bg-brand-50 text-brand-800"><Building2 class="h-4 w-4" /></span>
            <div>
              <p class="font-semibold text-ink-950">{{ department.name }}</p>
              <p class="text-[11px] text-ink-500">Updated {{ formatDateTime(department.updatedAt) }}</p>
            </div>
          </div>
          <span class="font-mono text-xs font-semibold text-ink-700">{{ department.code }}</span>
          <span class="text-sm text-ink-600">{{ department.faculty ?? 'Not specified' }}</span>
          <span class="w-fit rounded-full px-2 py-1 text-[10px] font-bold" :class="department.isActive ? 'bg-emerald-50 text-emerald-800' : 'bg-red-50 text-red-800'">
            {{ department.isActive ? 'Active' : 'Inactive' }}
          </span>
          <div class="flex justify-start gap-2 md:justify-end">
            <AppButton variant="secondary" size="sm" @click="openEdit(department)">Edit</AppButton>
            <AppButton :variant="department.isActive ? 'danger' : 'secondary'" size="sm" @click="setActive(department, !department.isActive)">
              {{ department.isActive ? 'Deactivate' : 'Activate' }}
            </AppButton>
          </div>
        </div>
      </div>
    </div>

    <div class="surface p-5 text-xs leading-5 text-ink-600">
      <p class="font-semibold text-ink-800">Isolation rule</p>
      <p class="mt-1">Only assigned Electoral Officers and Observers can work within a department. Super Administrators retain university-wide oversight. Deactivating a department never deletes historical elections.</p>
    </div>
  </div>

  <ModalDialog :open="createOpen" title="Add department" description="Codes are normalized and must be unique." @close="createOpen = false">
    <div class="space-y-4">
      <label class="block"><span class="field-label">Name</span><input v-model="form.name" class="field" maxlength="160" placeholder="e.g. Computer Science" /></label>
      <label class="block"><span class="field-label">Code</span><input v-model="form.code" class="field" maxlength="20" placeholder="e.g. CSC" /></label>
      <label class="block"><span class="field-label">Faculty or school (optional)</span><input v-model="form.faculty" class="field" maxlength="160" placeholder="e.g. Faculty of Natural and Applied Sciences" /></label>
      <div class="flex justify-end gap-2">
        <AppButton variant="secondary" @click="createOpen = false">Cancel</AppButton>
        <AppButton :loading="busy" :disabled="!form.name || !form.code" @click="create"><Save class="h-4 w-4" /> Create</AppButton>
      </div>
    </div>
  </ModalDialog>

  <ModalDialog :open="Boolean(editing)" title="Edit department" description="Changing a code does not rewrite historical election results." @close="editing = null">
    <div class="space-y-4">
      <label class="block"><span class="field-label">Name</span><input v-model="form.name" class="field" maxlength="160" /></label>
      <label class="block"><span class="field-label">Code</span><input v-model="form.code" class="field" maxlength="20" /></label>
      <label class="block"><span class="field-label">Faculty or school (optional)</span><input v-model="form.faculty" class="field" maxlength="160" /></label>
      <div class="flex justify-end gap-2">
        <AppButton variant="secondary" @click="editing = null">Cancel</AppButton>
        <AppButton :loading="busy" :disabled="!form.name || !form.code" @click="save"><Save class="h-4 w-4" /> Save</AppButton>
      </div>
    </div>
  </ModalDialog>
</template>
