<script setup lang="ts">
import { onBeforeUnmount, watch } from 'vue'
import { X } from '@lucide/vue'

const props = withDefaults(
  defineProps<{
    open: boolean
    title: string
    description?: string
    maxWidth?: string
  }>(),
  { description: '', maxWidth: 'max-w-lg' }
)

const emit = defineEmits<{ close: [] }>()

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && props.open) {
    emit('close')
  }
}

watch(
  () => props.open,
  (open) => {
    document.body.style.overflow = open ? 'hidden' : ''
    if (open) {
      window.addEventListener('keydown', onKeydown)
    } else {
      window.removeEventListener('keydown', onKeydown)
    }
  }
)

onBeforeUnmount(() => {
  document.body.style.overflow = ''
  window.removeEventListener('keydown', onKeydown)
})
</script>

<template>
  <Teleport to="body">
    <Transition name="modal">
      <div
        v-if="open"
        class="fixed inset-0 z-[60] flex items-end justify-center bg-ink-950/55 p-0 backdrop-blur-[2px] sm:items-center sm:p-6"
        role="presentation"
        @mousedown.self="$emit('close')"
      >
        <section
          role="dialog"
          aria-modal="true"
          :aria-label="title"
          class="max-h-[92vh] w-full overflow-y-auto rounded-t-3xl bg-white p-6 shadow-2xl sm:rounded-3xl"
          :class="maxWidth"
        >
          <header class="mb-5 flex items-start justify-between gap-4">
            <div>
              <h2 class="font-display text-2xl tracking-tight text-ink-950">{{ title }}</h2>
              <p v-if="description" class="mt-1 text-sm leading-6 text-ink-500">{{ description }}</p>
            </div>
            <button
              type="button"
              class="focus-ring rounded-xl p-2 text-ink-500 hover:bg-ink-950/5"
              aria-label="Close dialog"
              @click="$emit('close')"
            >
              <X class="h-5 w-5" />
            </button>
          </header>
          <slot />
        </section>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.modal-enter-active,
.modal-leave-active {
  transition: opacity 160ms ease;
}
.modal-enter-from,
.modal-leave-to {
  opacity: 0;
}
.modal-enter-active section,
.modal-leave-active section {
  transition: transform 160ms ease;
}
.modal-enter-from section,
.modal-leave-to section {
  transform: translateY(12px);
}
</style>
