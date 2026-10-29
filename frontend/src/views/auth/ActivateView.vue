<script setup lang="ts">
import { ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/yup'
import * as yup from 'yup'
import AuthLayout from '@/components/AuthLayout.vue'
import AppButton from '@/components/AppButton.vue'
import { ApiError } from '@/api/client'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toasts'

const auth = useAuthStore()
const toasts = useToastStore()
const route = useRoute()
const router = useRouter()
const serverError = ref('')

const schema = yup.object({
  matricNumber: yup.string().trim().required('Enter your matriculation number.'),
  token: yup.string().trim().required('Enter the activation token.'),
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
    matricNumber:
      typeof route.query.matric === 'string' ? route.query.matric : '',
    token: '',
    password: '',
    confirmPassword: ''
  },
  validationSchema: toTypedSchema(schema)
})

const [matricNumber, matricAttrs] = defineField('matricNumber')
const [token, tokenAttrs] = defineField('token')
const [password, passwordAttrs] = defineField('password')
const [confirmPassword, confirmAttrs] = defineField('confirmPassword')

const submit = handleSubmit(async (values) => {
  serverError.value = ''
  try {
    await auth.activate(
      values.matricNumber ?? '',
      values.token ?? '',
      values.password ?? '',
      values.confirmPassword ?? ''
    )
    toasts.success('Your account is active.')
    await router.push(auth.homePath)
  } catch (error) {
    serverError.value =
      error instanceof ApiError ? error.message : 'Activation failed. Check your code and try again.'
  }
})
</script>

<template>
  <AuthLayout
    title="Activate your account"
    subtitle="Use the one-time code issued to you by the departmental electoral office."
  >
    <form class="space-y-4" novalidate @submit="submit">
      <div v-if="serverError" class="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
        {{ serverError }}
      </div>

      <div>
        <label for="matric" class="field-label">Matriculation number</label>
        <input id="matric" v-model="matricNumber" v-bind="matricAttrs" class="field" autocomplete="username" />
        <p v-if="errors.matricNumber" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.matricNumber }}</p>
      </div>

      <div>
        <label for="token" class="field-label">Activation token</label>
        <input id="token" v-model="token" v-bind="tokenAttrs" class="field font-mono text-xs" autocomplete="one-time-code" />
        <p v-if="errors.token" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.token }}</p>
      </div>

      <div>
        <label for="new-password" class="field-label">New password</label>
        <input
          id="new-password"
          v-model="password"
          v-bind="passwordAttrs"
          type="password"
          class="field"
          autocomplete="new-password"
        />
        <p class="mt-1.5 text-[11px] leading-5 text-ink-500">
          At least 12 characters with uppercase, lowercase, number and symbol.
        </p>
        <p v-if="errors.password" class="mt-1 text-xs font-medium text-red-700">{{ errors.password }}</p>
      </div>

      <div>
        <label for="confirm-password" class="field-label">Confirm password</label>
        <input
          id="confirm-password"
          v-model="confirmPassword"
          v-bind="confirmAttrs"
          type="password"
          class="field"
          autocomplete="new-password"
        />
        <p v-if="errors.confirmPassword" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.confirmPassword }}</p>
      </div>

      <AppButton type="submit" size="lg" block :loading="isSubmitting || auth.busy">
        Activate account
      </AppButton>
    </form>
    <p class="mt-5 text-center text-sm text-ink-500">
      Already activated?
      <RouterLink to="/login" class="font-semibold text-brand-700 hover:text-brand-900">Sign in</RouterLink>
    </p>
  </AuthLayout>
</template>
