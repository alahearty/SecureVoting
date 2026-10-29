<script setup lang="ts">
withDefaults(defineProps<{ label?: string; rows?: number }>(), {
  label: 'Loading',
  rows: 3
})
</script>

<template>
  <div class="space-y-3" role="status" :aria-label="label">
    <div
      v-for="row in rows"
      :key="row"
      class="h-16 animate-pulse rounded-2xl border border-ink-500/10 bg-white/70"
    />
    <span class="sr-only">{{ label }}</span>
  </div>
</template>
