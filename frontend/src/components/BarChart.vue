<script setup lang="ts">
import { computed } from 'vue'
import {
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LinearScale,
  Title,
  Tooltip
} from 'chart.js'
import { Bar } from 'vue-chartjs'
import type { ChartSeries } from '@/api/types'

ChartJS.register(CategoryScale, LinearScale, BarElement, Title, Tooltip, Legend)

const props = defineProps<{
  series: ChartSeries
  horizontal?: boolean
  suffix?: string
}>()

const data = computed(() => ({
  labels: props.series.points.map((point) => point.label),
  datasets: [
    {
      label: props.series.label,
      data: props.series.points.map((point) => point.value),
      backgroundColor: '#247a52',
      hoverBackgroundColor: '#185f40',
      borderRadius: 7,
      maxBarThickness: 34
    }
  ]
}))

const options = computed(() => ({
  responsive: true,
  maintainAspectRatio: false,
  indexAxis: props.horizontal ? ('y' as const) : ('x' as const),
  plugins: {
    legend: { display: false }
  },
  scales: {
    x: {
      grid: { display: props.horizontal },
      ticks: { color: '#68776e', maxRotation: 0 }
    },
    y: {
      beginAtZero: true,
      grid: { color: 'rgba(104,119,110,0.12)' },
      ticks: { color: '#68776e' }
    }
  }
}))
</script>

<template>
  <div class="h-64 w-full">
    <Bar :data="data" :options="options" />
  </div>
</template>
