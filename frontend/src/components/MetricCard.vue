<script setup lang="ts">
defineProps<{
  label: string
  value: string
  detail?: string | null
  tone?: 'brand' | 'gold' | 'neutral'
}>()
</script>

<template>
  <div class="surface p-5">
    <p class="text-[11px] font-bold tracking-[0.16em] text-ink-500 uppercase">{{ label }}</p>
    <p
      class="mt-2 font-display text-3xl tracking-tight"
      :class="tone === 'gold' ? 'text-gold-600' : tone === 'brand' ? 'text-brand-800' : 'text-ink-950'"
    >
      {{ value }}
    </p>
    <p v-if="detail" class="mt-1 text-xs leading-5 text-ink-500">{{ detail }}</p>
  </div>
</template>
