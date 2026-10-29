<script setup lang="ts">
import { Inbox } from '@lucide/vue'

withDefaults(
  defineProps<{
    title: string
    description?: string
    compact?: boolean
  }>(),
  { description: '', compact: false }
)
</script>

<template>
  <div
    class="flex flex-col items-center justify-center rounded-2xl border border-dashed border-ink-500/20 bg-white/60 text-center"
    :class="compact ? 'px-5 py-8' : 'px-6 py-14'"
  >
    <span class="mb-3 rounded-2xl bg-brand-50 p-3 text-brand-700">
      <Inbox class="h-6 w-6" aria-hidden="true" />
    </span>
    <h3 class="font-display text-xl text-ink-950">{{ title }}</h3>
    <p v-if="description" class="mt-2 max-w-md text-sm leading-6 text-ink-500">{{ description }}</p>
    <slot />
  </div>
</template>
