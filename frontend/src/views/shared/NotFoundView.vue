<script setup lang="ts">
import { RouterLink } from 'vue-router'
import { Compass } from '@lucide/vue'
</script>

<template>
  <main class="grid min-h-screen place-items-center bg-paper px-4">
    <div class="surface max-w-lg p-8 text-center">
      <Compass class="mx-auto h-10 w-10 text-brand-700" />
      <p class="mt-5 text-xs font-bold tracking-[0.2em] text-gold-600 uppercase">404</p>
      <h1 class="mt-2 font-display text-4xl text-ink-950">This page is not on the ballot.</h1>
      <p class="mt-3 text-sm leading-6 text-ink-500">
        The link may be outdated or the workspace may have moved. Return to your dashboard to continue.
      </p>
      <RouterLink to="/" class="mt-6 inline-flex font-semibold text-brand-700 hover:text-brand-900">
        Return to SecureVote
      </RouterLink>
    </div>
  </main>
</template>
