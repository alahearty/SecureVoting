<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { ShieldX } from '@lucide/vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
</script>

<template>
  <div class="surface mx-auto max-w-lg p-8 text-center">
    <span class="mx-auto grid h-14 w-14 place-items-center rounded-2xl bg-red-50 text-red-700">
      <ShieldX class="h-7 w-7" />
    </span>
    <h1 class="mt-5 font-display text-3xl text-ink-950">Access denied</h1>
    <p class="mt-2 text-sm leading-6 text-ink-500">
      Your account does not hold the role required for this workspace. If this is unexpected, contact the
      electoral office or system administrator.
    </p>
    <RouterLink :to="auth.homePath" class="mt-6 inline-flex text-sm font-semibold text-brand-700">
      Return to your workspace
    </RouterLink>
  </div>
</template>
