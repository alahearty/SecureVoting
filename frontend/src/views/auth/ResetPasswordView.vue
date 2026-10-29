<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/yup'
import * as yup from 'yup'
import AuthLayout from '@/components/AuthLayout.vue'
import AppButton from '@/components/AppButton.vue'
import { ApiError, api } from '@/api/client'
import { useToastStore } from '@/stores/toasts'

const route = useRoute()
const router = useRouter()
const toasts = useToastStore()
const serverError = ref('')
const completed = ref(false)

const schema = yup.object({
  identifier: yup.string().trim().required('Enter your matriculation number or email.'),
  password: yup
    .string()
    .min(12, 'Use at least 12 characters.')
    .matches(/[A-Z]/, 'Include an uppercase letter.')
    .matches(/[a-z]/, 'Include a lowercase letter.')
    .matches(/\d/, 'Include a number.')
    .matches(/[^A-Za-z0-9]/, 'Include a symbol.')
    .required('Choose a password.'),
  confirmPassword: yup
    .string()
    .oneOf([yup.ref('password')], 'Passwords do not match.')
    .required('Confirm your password.')
})

const { handleSubmit, defineField, errors, isSubmitting } = useForm({
  initialValues: {
    identifier: typeof route.query.identifier === 'string' ? route.query.identifier : '',
    password: '',
    confirmPassword: ''
  },
  validationSchema: toTypedSchema(schema)
})
const [identifier, identifierAttrs] = defineField('identifier')
const [password, passwordAttrs] = defineField('password')
const [confirmPassword, confirmAttrs] = defineField('confirmPassword')

const submit = handleSubmit(async (values) => {
  serverError.value = ''
  const token = typeof route.query.token === 'string' ? route.query.token : ''
  if (!token) {
    serverError.value = 'This reset link is incomplete. Request a new link.'
    return
  }

  try {
    await api('/auth/reset-password', {
      method: 'POST',
      body: {
        identifier: values.identifier,
        token,
        password: values.password,
        confirmPassword: values.confirmPassword
      }
    })
    completed.value = true
    toasts.success('Password reset completed.')
  } catch (error) {
    serverError.value =
      error instanceof ApiError ? error.message : 'The reset could not be completed.'
  }
})

async function goToLogin(): Promise<void> {
  await router.push('/login')
}
</script>

<template>
  <AuthLayout
    title="Choose a new password"
    subtitle="Use the reset link from your email. Reset links expire and are single-use."
  >
    <div v-if="completed" class="rounded-2xl border border-emerald-200 bg-emerald-50 p-5">
      <p class="font-semibold text-emerald-900">Password updated</p>
      <p class="mt-2 text-sm leading-6 text-emerald-800">You can now sign in with the new password.</p>
      <AppButton class="mt-4" @click="goToLogin">Back to sign in</AppButton>
    </div>

    <form v-else class="space-y-4" novalidate @submit="submit">
      <div v-if="serverError" class="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
        {{ serverError }}
      </div>
      <div>
        <label for="reset-identifier" class="field-label">Matriculation number or email</label>
        <input id="reset-identifier" v-model="identifier" v-bind="identifierAttrs" class="field" autocomplete="username" />
        <p v-if="errors.identifier" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.identifier }}</p>
      </div>
      <div>
        <label for="reset-password" class="field-label">New password</label>
        <input id="reset-password" v-model="password" v-bind="passwordAttrs" type="password" class="field" autocomplete="new-password" />
        <p v-if="errors.password" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.password }}</p>
      </div>
      <div>
        <label for="reset-confirm" class="field-label">Confirm password</label>
        <input id="reset-confirm" v-model="confirmPassword" v-bind="confirmAttrs" type="password" class="field" autocomplete="new-password" />
        <p v-if="errors.confirmPassword" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.confirmPassword }}</p>
      </div>
      <AppButton type="submit" size="lg" block :loading="isSubmitting">Reset password</AppButton>
      <p class="text-center text-sm text-ink-500">
        <RouterLink to="/forgot-password" class="font-semibold text-brand-700">Request another link</RouterLink>
      </p>
    </form>
  </AuthLayout>
</template>
