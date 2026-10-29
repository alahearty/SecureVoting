import { ref } from 'vue'
import { defineStore } from 'pinia'

export interface ConfirmOptions {
  title: string
  message: string
  confirmLabel?: string
  cancelLabel?: string
  tone?: 'default' | 'danger'
}

export const useConfirmStore = defineStore('confirm', () => {
  const isOpen = ref(false)
  const options = ref<ConfirmOptions>({
    title: '',
    message: ''
  })
  let resolver: ((value: boolean) => void) | null = null

  function ask(next: ConfirmOptions): Promise<boolean> {
    options.value = next
    isOpen.value = true
    return new Promise<boolean>((resolve) => {
      resolver = resolve
    })
  }

  function resolve(value: boolean): void {
    isOpen.value = false
    resolver?.(value)
    resolver = null
  }

  return { isOpen, options, ask, resolve }
})
