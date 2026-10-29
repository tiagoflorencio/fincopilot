<script setup lang="ts">
import { ArcElement, Chart, DoughnutController, Tooltip, type TooltipItem } from 'chart.js'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { formatBrl, formatPercent } from '../lib/format'
import { assetKindLabels } from '../lib/portfolio'
import type { AllocationSlice } from '../types/portfolio'

// Registro seletivo (tree-shaking): só o que a rosca usa entra no bundle.
Chart.register(DoughnutController, ArcElement, Tooltip)

const props = defineProps<{ slices: AllocationSlice[]; total: number }>()

const canvas = ref<HTMLCanvasElement | null>(null)
let chart: Chart<'doughnut'> | null = null

// Canvas não entende var(--x): as cores são lidas dos tokens CSS do tema atual.
function themeColors() {
  const style = getComputedStyle(canvas.value!)
  const token = (name: string) => style.getPropertyValue(name).trim()
  return {
    series: props.slices.map((s) => token(`--series-${s.kind.replace('_', '-')}`)),
    surface: token('--surface'),
    border: token('--border'),
    text: token('--text'),
    textStrong: token('--text-strong'),
  }
}

function render() {
  if (!canvas.value) return
  chart?.destroy()
  const c = themeColors()

  chart = new Chart(canvas.value, {
    type: 'doughnut',
    data: {
      labels: props.slices.map((s) => assetKindLabels[s.kind]),
      datasets: [
        {
          data: props.slices.map((s) => s.value),
          backgroundColor: c.series,
          // Borda na cor da superfície = espaço de 2px entre as fatias, que separa cores vizinhas.
          borderColor: c.surface,
          borderWidth: 2,
          hoverOffset: 4,
        },
      ],
    },
    options: {
      cutout: '64%',
      maintainAspectRatio: false,
      animation: matchMedia('(prefers-reduced-motion: reduce)').matches ? false : { duration: 400 },
      plugins: {
        // A legenda é HTML (ao lado), com rótulo, % e valor; a do Chart.js ficaria só com a cor.
        legend: { display: false },
        tooltip: {
          backgroundColor: c.surface,
          borderColor: c.border,
          borderWidth: 1,
          titleColor: c.textStrong,
          bodyColor: c.text,
          padding: 10,
          displayColors: true,
          boxPadding: 4,
          callbacks: {
            label: (item: TooltipItem<'doughnut'>) => {
              const slice = props.slices[item.dataIndex]
              return ` ${formatBrl(slice.value)} (${formatPercent(slice.weightPercent)})`
            },
          },
        },
      },
    },
  })
}

// Troca de tema do sistema: o canvas precisa ser redesenhado com as novas cores.
const darkQuery = matchMedia('(prefers-color-scheme: dark)')

onMounted(() => {
  render()
  darkQuery.addEventListener('change', render)
})

onBeforeUnmount(() => {
  darkQuery.removeEventListener('change', render)
  chart?.destroy()
  chart = null
})

watch(() => props.slices, render, { deep: true })

const ariaLabel = computed(
  () => `Alocação por tipo de ativo: ${props.slices.map((s) => `${assetKindLabels[s.kind]} ${formatPercent(s.weightPercent)}`).join(', ')}.`,
)
</script>

<template>
  <div class="donut">
    <canvas ref="canvas" role="img" :aria-label="ariaLabel"></canvas>
    <div class="center" aria-hidden="true">
      <span class="center-label">Total</span>
      <strong class="center-value">{{ formatBrl(total) }}</strong>
    </div>
  </div>
</template>

<style scoped>
.donut {
  position: relative;
  width: 100%;
  max-width: 220px;
  aspect-ratio: 1;
}

.center {
  position: absolute;
  inset: 0;
  display: grid;
  place-content: center;
  text-align: center;
  pointer-events: none;
}

.center-label {
  font-size: 12px;
  color: var(--text-muted);
}

.center-value {
  font-size: 15px;
  color: var(--text-strong);
  font-variant-numeric: tabular-nums;
}
</style>
