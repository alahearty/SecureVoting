<script setup lang="ts">
import { computed, ref } from 'vue'
import type { BallotCandidate, CandidatePublic } from '@/api/types'
import { initials } from '@/utils/format'

const props = defineProps<{
  candidate: BallotCandidate | CandidatePublic
  selected?: boolean
  selectable?: boolean
  compact?: boolean
}>()

const imageFailed = ref(false)
const monogram = computed(() => initials(props.candidate.displayName))
</script>

<template>
  <div
    class="flex gap-3 rounded-2xl border p-4 transition"
    :class="[
      selectable ? 'cursor-pointer hover:border-brand-400/70 hover:bg-brand-50/40' : '',
      selected ? 'border-brand-500 bg-brand-50/70 ring-2 ring-brand-500/15' : 'border-ink-500/10 bg-white'
    ]"
  >
    <div class="relative h-14 w-14 shrink-0 overflow-hidden rounded-2xl bg-brand-100 text-brand-800">
      <img
        v-if="candidate.photoUrl && !imageFailed"
        :src="candidate.photoUrl"
        :alt="`${candidate.displayName} photograph`"
        class="h-full w-full object-cover"
        loading="lazy"
        @error="imageFailed = true"
      />
      <span v-else class="grid h-full w-full place-items-center font-display text-lg font-bold">
        {{ monogram }}
      </span>
    </div>
    <div class="min-w-0 flex-1">
      <div class="flex items-start justify-between gap-2">
        <div>
          <p class="font-semibold text-ink-950">{{ candidate.displayName }}</p>
          <p class="mt-1 text-xs font-semibold text-brand-700">Approved candidate</p>
        </div>
        <span v-if="selected" class="grid h-6 w-6 place-items-center rounded-full bg-brand-700 text-xs text-white">✓</span>
      </div>
      <p class="mt-2 line-clamp-3 text-xs leading-5 text-ink-500" :class="compact ? 'sm:line-clamp-2' : ''">
        {{ candidate.manifesto }}
      </p>
    </div>
  </div>
</template>
