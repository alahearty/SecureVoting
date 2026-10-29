<script setup lang="ts">
import { CircleCheck, CircleX, Info, X } from '@lucide/vue'
import { useToastStore } from '@/stores/toasts'

const toasts = useToastStore()
</script>

<template>
  <div class="pointer-events-none fixed inset-x-0 bottom-0 z-[70] flex flex-col items-center gap-2 p-4 sm:items-end">
    <TransitionGroup
      name="toast"
      tag="div"
      class="flex w-full max-w-sm flex-col gap-2"
    >
      <div
        v-for="message in toasts.messages"
        :key="message.id"
        class="pointer-events-auto flex w-full items-start gap-3 rounded-2xl border bg-white p-4 shadow-xl"
        :class="{
          'border-emerald-200': message.tone === 'success',
          'border-red-200': message.tone === 'error',
          'border-sky-200': message.tone === 'info'
        }"
        role="status"
      >
        <CircleCheck v-if="message.tone === 'success'" class="mt-0.5 h-5 w-5 shrink-0 text-emerald-700" />
        <CircleX v-else-if="message.tone === 'error'" class="mt-0.5 h-5 w-5 shrink-0 text-red-700" />
        <Info v-else class="mt-0.5 h-5 w-5 shrink-0 text-sky-700" />
        <p class="flex-1 text-sm leading-5 text-ink-900">{{ message.message }}</p>
        <button
          type="button"
          class="focus-ring rounded-lg p-1 text-ink-500 hover:bg-ink-950/5"
          aria-label="Dismiss notification"
          @click="toasts.remove(message.id)"
        >
          <X class="h-4 w-4" />
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition: all 180ms ease;
}
.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(8px);
}
</style>
