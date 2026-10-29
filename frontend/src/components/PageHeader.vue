<script setup lang="ts">
withDefaults(
  defineProps<{
    eyebrow?: string
    title: string
    description?: string
  }>(),
  { eyebrow: '', description: '' }
)
</script>

<template>
  <div class="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
    <div class="max-w-3xl">
      <p v-if="eyebrow" class="eyebrow mb-2">{{ eyebrow }}</p>
      <h1 class="page-title">{{ title }}</h1>
      <p v-if="description" class="mt-3 max-w-2xl text-sm leading-6 text-ink-500">
        {{ description }}
      </p>
    </div>
    <div v-if="$slots.actions" class="flex flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </div>
</template>
