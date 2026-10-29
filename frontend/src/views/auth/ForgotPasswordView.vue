<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/yup'
import * as yup from 'yup'
import AuthLayout from '@/components/AuthLayout.vue'
import AppButton from '@/components/AppButton.vue'
import { ApiError, api } from '@/api/client'

const serverError = ref('')
const sent = ref(false)

const schema = yup.object({
  identifier: yup.string().trim().required('Enter your matriculation number or email.')
})

const { handleSubmit, defineField, errors, isSubmitting } = useForm({
  validationSchema: toTypedSchema(schema)
})
const [identifier, identifierAttrs] = defineField('identifier')

const submit = handleSubmit(async (values) => {
  serverError.value = ''
  try {
    await api('/auth/forgot-password', {
      method: 'POST',
      body: { identifier: values.identifier }
    })
    sent.value = true
  } catch (error) {
    serverError.value =
      error instanceof ApiError ? error.message : 'The request could not be completed. Try again later.'
  }
})
</script>

<template>
  <AuthLayout
    title="Password recovery"
    subtitle="Request a time-limited reset link. Account existence is never disclosed."
  >
    <div v-if="sent" class="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
      <p class="font-semibold text-emerald-900">Request received</p>
      <p class="mt-2 text-sm leading-6 text-emerald-800">
        If the account exists and email delivery is configured, a reset link has been sent.
        Contact the electoral office if you do not receive it.
      </p>
      <RouterLink to="/login" class="mt-4 inline-flex text-sm font-semibold text-brand-700">Back to sign in</RouterLink>
    </div>

    <form v-else class="space-y-4" novalidate @submit="submit">
      <div v-if="serverError" class="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
        {{ serverError }}
      </div>
      <div>
        <label for="recovery" class="field-label">Matriculation number or email</label>
        <input id="recovery" v-model="identifier" v-bind="identifierAttrs" class="field" autocomplete="username" />
        <p v-if="errors.identifier" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.identifier }}</p>
      </div>
      <AppButton type="submit" size="lg" block :loading="isSubmitting">Send reset link</AppButton>
      <p class="text-center text-sm text-ink-500">
        <RouterLink to="/login" class="font-semibold text-brand-700">Return to sign in</RouterLink>
      </p>
    </form>
  </AuthLayout>
</template>
