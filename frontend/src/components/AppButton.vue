<script setup lang="ts">
import { computed } from 'vue'
import { LoaderCircle } from '@lucide/vue'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger' | 'gold'
type Size = 'sm' | 'md' | 'lg'

const props = withDefaults(
  defineProps<{
    variant?: Variant
    size?: Size
    type?: 'button' | 'submit' | 'reset'
    loading?: boolean
    disabled?: boolean
    block?: boolean
  }>(),
  {
    variant: 'primary',
    size: 'md',
    type: 'button',
    loading: false,
    disabled: false,
    block: false
  }
)

defineEmits<{ click: [event: MouseEvent] }>()

const classes = computed(() => {
  const base =
    'focus-ring inline-flex items-center justify-center gap-2 rounded-xl font-semibold transition disabled:cursor-not-allowed disabled:opacity-55'
  const variants: Record<Variant, string> = {
    primary: 'bg-brand-700 text-white shadow-sm hover:bg-brand-800',
    secondary: 'border border-brand-700/20 bg-white text-brand-800 hover:bg-brand-50',
    ghost: 'text-ink-700 hover:bg-ink-950/5',
    danger: 'bg-red-700 text-white hover:bg-red-800',
    gold: 'bg-gold-400 text-ink-950 shadow-sm hover:bg-gold-300'
  }
  const sizes: Record<Size, string> = {
    sm: 'px-3 py-2 text-xs',
    md: 'px-4 py-2.5 text-sm',
    lg: 'px-6 py-3.5 text-base'
  }
  return [base, variants[props.variant], sizes[props.size], props.block ? 'w-full' : '']
})
</script>

<template>
  <button
    :type="type"
    :class="classes"
    :disabled="loading || disabled"
    @click="$emit('click', $event)"
  >
    <LoaderCircle v-if="loading" class="h-4 w-4 animate-spin" aria-hidden="true" />
    <slot />
  </button>
</template>
