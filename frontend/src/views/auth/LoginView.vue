<script setup lang="ts">
import { computed, ref } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { useForm } from 'vee-validate'
import { toTypedSchema } from '@vee-validate/yup'
import * as yup from 'yup'
import { Eye, EyeOff, KeyRound } from '@lucide/vue'
import AuthLayout from '@/components/AuthLayout.vue'
import AppButton from '@/components/AppButton.vue'
import { ApiError } from '@/api/client'
import { useAuthStore } from '@/stores/auth'
import { useToastStore } from '@/stores/toasts'

const auth = useAuthStore()
const toasts = useToastStore()
const route = useRoute()
const router = useRouter()
const showPassword = ref(false)
const serverError = ref('')

const schema = yup.object({
  identifier: yup.string().trim().required('Enter your matriculation number or email.'),
  password: yup.string().required('Enter your password.')
})

const { handleSubmit, defineField, errors, isSubmitting } = useForm({
  validationSchema: toTypedSchema(schema)
})

const [identifier, identifierAttrs] = defineField('identifier')
const [password, passwordAttrs] = defineField('password')

const redirectTarget = computed(() => {
  const target = route.query.redirect
  return typeof target === 'string' && target.startsWith('/') && !target.startsWith('//')
    ? target
    : auth.homePath
})

const submit = handleSubmit(async (values) => {
  serverError.value = ''
  try {
    await auth.login(values.identifier ?? '', values.password ?? '')
    toasts.success('Signed in securely.')
    await router.push(redirectTarget.value)
  } catch (error) {
    serverError.value =
      error instanceof ApiError ? error.message : 'Sign-in failed. Check your connection and try again.'
  }
})
</script>

<template>
  <AuthLayout
    title="Welcome back"
    subtitle="Use your matriculation number or the email address on your departmental account."
  >
    <form class="space-y-4" novalidate @submit="submit">
      <div v-if="serverError" class="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-800" role="alert">
        {{ serverError }}
      </div>

      <div>
        <label for="identifier" class="field-label">Matriculation number or email</label>
        <input
          id="identifier"
          v-model="identifier"
          v-bind="identifierAttrs"
          type="text"
          autocomplete="username"
          class="field"
          placeholder="IAUE/CS/22/001"
          :aria-invalid="Boolean(errors.identifier)"
        />
        <p v-if="errors.identifier" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.identifier }}</p>
      </div>

      <div>
        <div class="mb-2 flex items-center justify-between gap-3">
          <label for="password" class="field-label !mb-0">Password</label>
          <RouterLink to="/forgot-password" class="text-xs font-semibold text-brand-700 hover:text-brand-900">
            Forgot password?
          </RouterLink>
        </div>
        <div class="relative">
          <input
            id="password"
            v-model="password"
            v-bind="passwordAttrs"
            :type="showPassword ? 'text' : 'password'"
            autocomplete="current-password"
            class="field pr-12"
            placeholder="Enter your password"
            :aria-invalid="Boolean(errors.password)"
          />
          <button
            type="button"
            class="focus-ring absolute inset-y-0 right-0 grid w-11 place-items-center rounded-r-xl text-ink-500"
            :aria-label="showPassword ? 'Hide password' : 'Show password'"
            @click="showPassword = !showPassword"
          >
            <EyeOff v-if="showPassword" class="h-4 w-4" />
            <Eye v-else class="h-4 w-4" />
          </button>
        </div>
        <p v-if="errors.password" class="mt-1.5 text-xs font-medium text-red-700">{{ errors.password }}</p>
      </div>

      <AppButton type="submit" size="lg" block :loading="isSubmitting || auth.busy">
        <KeyRound class="h-4 w-4" />
        Sign in
      </AppButton>
    </form>

    <div class="mt-6 border-t border-ink-500/10 pt-5 text-sm text-ink-500">
      <p>
        First time here? Use the one-time activation code issued by the electoral officer.
      </p>
      <RouterLink to="/activate" class="mt-2 inline-flex font-semibold text-brand-700 hover:text-brand-900">
        Activate your student account
      </RouterLink>
    </div>
  </AuthLayout>
</template>
