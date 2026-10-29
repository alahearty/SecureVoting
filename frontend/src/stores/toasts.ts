import { ref } from 'vue'
import { defineStore } from 'pinia'

export type ToastTone = 'success' | 'error' | 'info'

export interface ToastMessage {
  id: number
  tone: ToastTone
  message: string
}

export const useToastStore = defineStore('toasts', () => {
  const messages = ref<ToastMessage[]>([])
  let nextId = 1

  function push(tone: ToastTone, message: string, timeout = 4500): void {
    const id = nextId++
    messages.value.push({ id, tone, message })
    window.setTimeout(() => remove(id), timeout)
  }

  function success(message: string): void {
    push('success', message)
  }

  function error(message: string): void {
    push('error', message, 6500)
  }

  function info(message: string): void {
    push('info', message)
  }

  function remove(id: number): void {
    messages.value = messages.value.filter((message) => message.id !== id)
  }

  return { messages, push, success, error, info, remove }
})
