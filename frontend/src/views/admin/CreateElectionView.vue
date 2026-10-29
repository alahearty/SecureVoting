<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { RouterLink, useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/yup'
import * as yup from 'yup'
import { ArrowLeft, CalendarPlus } from '@lucide/vue'
import { api, ApiError } from '@/api/client'
import type { CreateElectionInput, Department, TieHandling } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useToastStore } from '@/stores/toasts'
import { toIsoLocal } from '@/utils/format'

const router = useRouter()
const toasts = useToastStore()
const serverError = ref('')
const departments = ref<Department[]>([])
const departmentsLoading = ref(true)

const isoDate = yup
  .string()
  .required('Required')
  .test('valid-date', 'Enter a valid date and time.', (value) => Boolean(value && !Number.isNaN(new Date(value).getTime())))

const schema = yup.object({
  title: yup.string().trim().min(5, 'Use at least 5 characters.').max(160).required('Title is required.'),
  description: yup.string().trim().max(2000).default(''),
  academicSession: yup.string().trim().required('Academic session is required.'),
  departmentId: yup.string().uuid('Select a department.').required('Department is required.'),
  nominationOpensAt: isoDate,
  nominationClosesAt: isoDate,
  opensAt: isoDate,
  closesAt: isoDate,
  allowAbstention: yup.boolean().default(true),
  tieHandling: yup.string().oneOf(['RunoffRequired', 'SharedOffice', 'ManualResolution']).required(),
  nominationRequiresApproval: yup.boolean().default(true)
})

async function loadDepartments(): Promise<void> {
  try {
    departments.value = await api<Department[]>('/departments')
    if (!departmentId.value && departments.value.length > 0) {
      departmentId.value = departments.value[0].id
    }
  } catch (caught) {
    serverError.value = caught instanceof ApiError ? caught.message : 'Departments could not be loaded.'
  } finally {
    departmentsLoading.value = false
  }
}

const now = new Date()
const defaultOpen = new Date(now.getTime() + 24 * 60 * 60 * 1000)
const defaultClose = new Date(now.getTime() + 30 * 60 * 60 * 1000)

const { handleSubmit, defineField, errors, isSubmitting } = useForm({
  initialValues: {
    title: '',
    description: '',
    academicSession: '2026/2027',
    departmentId: '',
    nominationOpensAt: toIsoLocal(now.toISOString()),
    nominationClosesAt: toIsoLocal(defaultOpen.toISOString()),
    opensAt: toIsoLocal(defaultOpen.toISOString()),
    closesAt: toIsoLocal(defaultClose.toISOString()),
    allowAbstention: true,
    tieHandling: 'RunoffRequired' as TieHandling,
    nominationRequiresApproval: true
  },
  validationSchema: toTypedSchema(schema)
})

const [title, titleAttrs] = defineField('title')
const [description, descriptionAttrs] = defineField('description')
const [academicSession, sessionAttrs] = defineField('academicSession')
const [departmentId, departmentAttrs] = defineField('departmentId')
const [nominationOpensAt, nominationOpenAttrs] = defineField('nominationOpensAt')
const [nominationClosesAt, nominationCloseAttrs] = defineField('nominationClosesAt')
const [opensAt, opensAttrs] = defineField('opensAt')
const [closesAt, closesAttrs] = defineField('closesAt')
const [allowAbstention, abstentionAttrs] = defineField('allowAbstention')
const [tieHandling, tieAttrs] = defineField('tieHandling')
const [nominationRequiresApproval, approvalAttrs] = defineField('nominationRequiresApproval')

function toIso(value: string): string {
  return new Date(value).toISOString()
}

onMounted(loadDepartments)

const submit = handleSubmit(async (values) => {
  serverError.value = ''
  try {
    const input: CreateElectionInput = {
      title: values.title ?? '',
      description: values.description ?? '',
      academicSession: values.academicSession ?? '',
      departmentId: values.departmentId ?? '',
      nominationOpensAt: toIso(values.nominationOpensAt ?? ''),
      nominationClosesAt: toIso(values.nominationClosesAt ?? ''),
      opensAt: toIso(values.opensAt ?? ''),
      closesAt: toIso(values.closesAt ?? ''),
      allowAbstention: values.allowAbstention ?? true,
      tieHandling: (values.tieHandling ?? 'RunoffRequired') as TieHandling,
      nominationRequiresApproval: values.nominationRequiresApproval ?? true
    }
    const created = await api<{ id: string }>('/admin/elections', { method: 'POST', body: input })
    toasts.success('Draft election created. Add positions and import the voter register next.')
    await router.push({ name: 'admin-election', params: { id: created.id } })
  } catch (caught) {
    if (caught instanceof ApiError) {
      serverError.value = caught.message
    } else {
      serverError.value = 'The election could not be created.'
    }
  }
})
</script>

<template>
  <div class="space-y-6">
    <RouterLink to="/admin/elections" class="inline-flex items-center gap-2 text-xs font-bold text-brand-700">
      <ArrowLeft class="h-4 w-4" /> Elections
    </RouterLink>
    <PageHeader
      eyebrow="New election"
      title="Create the draft"
      description="Define the schedule, rules and department. Positions and the voter register are added from the election workspace."
    />

    <form class="surface max-w-4xl space-y-6 p-5 sm:p-7" novalidate @submit="submit">
      <div v-if="serverError" class="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
        {{ serverError }}
      </div>

      <div class="grid gap-5 sm:grid-cols-2">
        <div class="sm:col-span-2">
          <label for="title" class="field-label">Election title</label>
          <input id="title" v-model="title" v-bind="titleAttrs" class="field" placeholder="2026/2027 Departmental Election" />
          <p v-if="errors.title" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.title }}</p>
        </div>
        <div>
          <label for="session" class="field-label">Academic session</label>
          <input id="session" v-model="academicSession" v-bind="sessionAttrs" class="field" placeholder="2026/2027" />
          <p v-if="errors.academicSession" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.academicSession }}</p>
        </div>
        <div>
          <label for="department" class="field-label">Department</label>
          <select id="department" v-model="departmentId" v-bind="departmentAttrs" class="field" :disabled="departmentsLoading">
            <option value="" disabled>{{ departmentsLoading ? 'Loading departments...' : 'Select an authorised department' }}</option>
            <option v-for="department in departments" :key="department.id" :value="department.id">
              {{ department.name }} ({{ department.code }})
            </option>
          </select>
          <p v-if="errors.departmentId" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.departmentId }}</p>
          <p v-if="!departmentsLoading && departments.length === 0" class="mt-1.5 text-xs font-medium text-amber-700">
            No active department is assigned to your account. Ask a university administrator to configure access.
          </p>
        </div>
        <div class="sm:col-span-2">
          <label for="description" class="field-label">Description</label>
          <textarea id="description" v-model="description" v-bind="descriptionAttrs" class="field min-h-24" placeholder="Purpose and scope of this election" />
        </div>
      </div>

      <div class="border-t border-ink-500/10 pt-6">
        <p class="eyebrow">Schedule</p>
        <div class="mt-4 grid gap-4 sm:grid-cols-2">
          <div>
            <label for="nom-open" class="field-label">Nominations open</label>
            <input id="nom-open" v-model="nominationOpensAt" v-bind="nominationOpenAttrs" type="datetime-local" class="field" />
            <p v-if="errors.nominationOpensAt" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.nominationOpensAt }}</p>
          </div>
          <div>
            <label for="nom-close" class="field-label">Nominations close</label>
            <input id="nom-close" v-model="nominationClosesAt" v-bind="nominationCloseAttrs" type="datetime-local" class="field" />
            <p v-if="errors.nominationClosesAt" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.nominationClosesAt }}</p>
          </div>
          <div>
            <label for="opens" class="field-label">Voting opens</label>
            <input id="opens" v-model="opensAt" v-bind="opensAttrs" type="datetime-local" class="field" />
            <p v-if="errors.opensAt" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.opensAt }}</p>
          </div>
          <div>
            <label for="closes" class="field-label">Voting closes</label>
            <input id="closes" v-model="closesAt" v-bind="closesAttrs" type="datetime-local" class="field" />
            <p v-if="errors.closesAt" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.closesAt }}</p>
          </div>
        </div>
        <p class="mt-2 text-[11px] leading-5 text-ink-500">
          Times are interpreted in your browser's local timezone and stored by the server in UTC. The server clock decides
          whether a ballot is accepted.
        </p>
      </div>

      <div class="border-t border-ink-500/10 pt-6">
        <p class="eyebrow">Ballot rules</p>
        <div class="mt-4 grid gap-4 sm:grid-cols-2">
          <div>
            <label for="tie" class="field-label">Tie handling</label>
            <select id="tie" v-model="tieHandling" v-bind="tieAttrs" class="field">
              <option value="RunoffRequired">Runoff required</option>
              <option value="SharedOffice">Shared office</option>
              <option value="ManualResolution">Manual resolution</option>
            </select>
          </div>
          <label class="flex items-start gap-3 rounded-xl border border-ink-500/10 p-3">
            <input v-model="allowAbstention" v-bind="abstentionAttrs" type="checkbox" class="mt-0.5 accent-brand-700" />
            <span>
              <span class="text-sm font-semibold text-ink-950">Allow abstention</span>
              <span class="mt-0.5 block text-[11px] leading-5 text-ink-500">Voters may explicitly abstain from a position.</span>
            </span>
          </label>
          <label class="flex items-start gap-3 rounded-xl border border-ink-500/10 p-3 sm:col-span-2">
            <input v-model="nominationRequiresApproval" v-bind="approvalAttrs" type="checkbox" class="mt-0.5 accent-brand-700" />
            <span>
              <span class="text-sm font-semibold text-ink-950">Require officer approval for nominations</span>
              <span class="mt-0.5 block text-[11px] leading-5 text-ink-500">Recommended. Unapproved nominations cannot be opened for voting.</span>
            </span>
          </label>
        </div>
      </div>

      <div class="flex flex-col-reverse gap-2 border-t border-ink-500/10 pt-5 sm:flex-row sm:justify-end">
        <RouterLink to="/admin/elections"><AppButton variant="secondary">Cancel</AppButton></RouterLink>
        <AppButton type="submit" :loading="isSubmitting"><CalendarPlus class="h-4 w-4" /> Create draft</AppButton>
      </div>
    </form>
  </div>
</template>
