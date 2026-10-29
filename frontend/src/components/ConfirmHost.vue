<script setup lang="ts">
import { computed } from 'vue'
import { useConfirmStore } from '@/stores/confirm'
import ModalDialog from './ModalDialog.vue'
import AppButton from './AppButton.vue'

const confirm = useConfirmStore()
const tone = computed(() => confirm.options.tone ?? 'default')
</script>

<template>
  <ModalDialog
    :open="confirm.isOpen"
    :title="confirm.options.title"
    :description="confirm.options.message"
    max-width="max-w-md"
    @close="confirm.resolve(false)"
  >
    <div class="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
      <AppButton variant="secondary" @click="confirm.resolve(false)">
        {{ confirm.options.cancelLabel ?? 'Cancel' }}
      </AppButton>
      <AppButton
        :variant="tone === 'danger' ? 'danger' : 'primary'"
        @click="confirm.resolve(true)"
      >
        {{ confirm.options.confirmLabel ?? 'Continue' }}
      </AppButton>
    </div>
  </ModalDialog>
</template>
