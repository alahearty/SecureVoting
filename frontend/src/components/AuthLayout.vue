<script setup lang="ts">
import { Landmark, LockKeyhole, ShieldCheck, Vote } from '@lucide/vue'
import { useAuthStore } from '@/stores/auth'

defineProps<{
  title: string
  subtitle: string
}>()

const auth = useAuthStore()
</script>

<template>
  <main class="min-h-screen bg-paper lg:grid lg:grid-cols-[1.05fr_0.95fr]">
    <section class="relative hidden overflow-hidden bg-brand-950 p-10 text-white lg:flex lg:flex-col lg:justify-between xl:p-16">
      <div class="absolute inset-0 opacity-25" aria-hidden="true">
        <div class="absolute -top-24 -right-24 h-[440px] w-[440px] rounded-full border-[80px] border-gold-300/35" />
        <div class="absolute bottom-[-180px] left-[-100px] h-[520px] w-[520px] rounded-full border-[100px] border-brand-400/30" />
      </div>
      <div class="relative">
        <div class="flex items-center gap-3">
          <span class="grid h-12 w-12 place-items-center rounded-2xl bg-gold-400 text-ink-950 shadow-lg">
            <Landmark class="h-6 w-6" />
          </span>
          <div>
            <p class="font-display text-2xl">IAUE SecureVote</p>
            <p class="text-[10px] font-bold tracking-[0.2em] text-brand-200 uppercase">
              Ignatius Ajuru University of Education
            </p>
          </div>
        </div>

        <div class="mt-20 max-w-lg">
          <p class="text-xs font-bold tracking-[0.22em] text-gold-300 uppercase">
            Confidential · One student · One ballot
          </p>
          <h1 class="mt-4 font-display text-5xl leading-[1.05] tracking-tight xl:text-6xl">
            Departmental elections built around evidence, not assumptions.
          </h1>
          <p class="mt-6 max-w-md text-base leading-7 text-brand-100">
            Verified student eligibility, a private ballot, atomic one-vote enforcement and an
            independently reviewable result trail.
          </p>
        </div>
      </div>

      <div class="relative grid gap-3 text-sm text-brand-100 sm:grid-cols-2">
        <div class="rounded-2xl border border-white/10 bg-white/5 p-4">
          <Vote class="h-5 w-5 text-gold-300" />
          <p class="mt-3 font-semibold text-white">Private selections</p>
          <p class="mt-1 text-xs leading-5 text-brand-200">Your identity is never stored beside your choices.</p>
        </div>
        <div class="rounded-2xl border border-white/10 bg-white/5 p-4">
          <ShieldCheck class="h-5 w-5 text-gold-300" />
          <p class="mt-3 font-semibold text-white">Server enforced</p>
          <p class="mt-1 text-xs leading-5 text-brand-200">Eligibility and one-vote rules are checked on the backend.</p>
        </div>
      </div>
    </section>

    <section class="flex min-h-screen items-center justify-center px-4 py-10 sm:px-8">
      <div class="w-full max-w-md">
        <div class="mb-8 flex items-center gap-3 lg:hidden">
          <span class="grid h-11 w-11 place-items-center rounded-2xl bg-brand-800 text-white">
            <Landmark class="h-6 w-6" />
          </span>
          <div>
            <p class="font-display text-xl text-ink-950">IAUE SecureVote</p>
            <p class="text-[10px] font-bold tracking-[0.18em] text-brand-600 uppercase">University election platform</p>
          </div>
        </div>

        <div class="surface p-6 sm:p-8">
          <div class="mb-6">
            <p class="eyebrow mb-2">Secure access</p>
            <h2 class="font-display text-3xl tracking-tight text-ink-950">{{ title }}</h2>
            <p class="mt-2 text-sm leading-6 text-ink-500">{{ subtitle }}</p>
          </div>
          <slot />
        </div>

        <div class="mt-5 flex items-center justify-center gap-2 text-[11px] text-ink-500">
          <LockKeyhole class="h-3.5 w-3.5" />
          <span>Protected by secure cookies, CSRF checks and rate limits.</span>
        </div>
        <p v-if="auth.isDemo" class="mt-3 text-center text-[11px] font-semibold text-gold-600">
          Demonstration workspace — synthetic election data only.
        </p>
      </div>
    </section>
  </main>
</template>
