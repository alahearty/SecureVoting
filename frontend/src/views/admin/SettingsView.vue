<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'

import { api, ApiError } from '@/api/client'
import type { SystemSetting } from '@/api/types'
import AppButton from '@/components/AppButton.vue'
import LoadingState from '@/components/LoadingState.vue'
import PageHeader from '@/components/PageHeader.vue'
import { useToastStore } from '@/stores/toasts'
import { formatDateTime } from '@/utils/format'

const toasts = useToastStore()
const settings = ref<SystemSetting[]>([])
const loading = ref(true)
const busy = ref('')
const error = ref('')
const values = ref<Record<string, string>>({})

const labels: Record<string, string> = {
  ApplicationName: 'Application name',
  InstitutionName: 'Institution name'
}

const editable = computed(() => settings.value.filter((setting) => setting.key in labels))

async function load(): Promise<void> {
  loading.value = true
  try {
    settings.value = await api<SystemSetting[]>('/admin/settings')
    values.value = Object.fromEntries(settings.value.map((setting) => [setting.key, setting.value]))
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'System settings could not be loaded.'
  } finally {
    loading.value = false
  }
}

async function save(key: string): Promise<void> {
  busy.value = key
  try {
    await api('/admin/settings', {
      method: 'PUT',
      body: { key, value: values.value[key] ?? '' }
    })
    toasts.success(`${labels[key] ?? key} updated.`)
    await load()
  } catch (caught) {
    error.value = caught instanceof ApiError ? caught.message : 'The setting could not be saved.'
  } finally {
    busy.value = ''
  }
}

onMounted(load)
</script>

<template>
  <div class="space-y-6">
    <PageHeader
      eyebrow="System configuration"
      title="Settings and controls"
      description="Editable display settings are intentionally narrow. Secrets, connection strings and SMTP credentials belong in environment configuration, never the browser."
    />

    <div v-if="error" class="rounded-2xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
      {{ error }}
    </div>
    <LoadingState v-if="loading" :rows="3" />
    <div v-else class="grid gap-5 lg:grid-cols-[1fr_360px]">
      <section class="surface p-5 sm:p-6">
        <p class="eyebrow">Presentation</p>
        <h2 class="mt-1 font-display text-2xl">Institution identity</h2>
        <div class="mt-5 space-y-5">
          <div v-for="setting in editable" :key="setting.key">
            <label :for="setting.key" class="field-label">{{ labels[setting.key] }}</label>
            <div class="flex gap-2">
              <input :id="setting.key" v-model="values[setting.key]" class="field" />
              <AppButton :loading="busy === setting.key" @click="save(setting.key)"><Save class="h-4 w-4" /> Save</AppButton>
            </div>
            <p class="mt-1 text-[11px] text-ink-500">Last updated {{ formatDateTime(setting.updatedAt) }}</p>
          </div>
          <p v-if="editable.length === 0" class="text-sm text-ink-500">No editable settings are available.</p>
        </div>
      </section>

      <aside class="space-y-4">
        <section class="surface p-5">
          <div class="flex items-center gap-2"><ShieldCheck class="h-5 w-5 text-brand-700" /><p class="eyebrow">Configured controls</p></div>
          <ul class="mt-4 space-y-3 text-xs leading-5 text-ink-600">
            <li class="flex gap-2"><CheckCircle2 class="h-4 w-4 shrink-0 text-brand-700" /> HttpOnly secure authentication cookie and CSRF validation.</li>
            <li class="flex gap-2"><CheckCircle2 class="h-4 w-4 shrink-0 text-brand-700" /> Server-side role policies and login rate limiting.</li>
            <li class="flex gap-2"><CheckCircle2 class="h-4 w-4 shrink-0 text-brand-700" /> Atomic one-vote entitlement and anonymous ballot model.</li>
            <li class="flex gap-2"><CheckCircle2 class="h-4 w-4 shrink-0 text-brand-700" /> Hash-chained audit log with verification endpoint.</li>
          </ul>
        </section>
        <section class="surface p-5">
          <div class="flex items-center gap-2"><Database class="h-5 w-5 text-brand-700" /><p class="eyebrow">Database</p></div>
          <p class="mt-3 text-xs leading-5 text-ink-600">
            PostgreSQL is the system of record. Run the documented backup and restore procedure from the deployment guide.
            This browser application has no database download endpoint.
          </p>
        </section>
        <section class="rounded-2xl bg-brand-950 p-5 text-white">
          <ScrollText class="h-5 w-5 text-gold-300" />
          <p class="mt-3 font-display text-xl">Secrets stay server-side.</p>
          <p class="mt-2 text-xs leading-5 text-brand-100">
            SMTP passwords, database credentials and Data Protection keys must be supplied by deployment environment or a
            secret manager. They are never sent to the SPA.
          </p>
        </section>
      </aside>
    </div>
  </div>
</template>
