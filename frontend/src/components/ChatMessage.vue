<script setup lang="ts">
import { computed } from 'vue'
import { formatDuration, formatUsd, toolLabel } from '../lib/format'
import { renderMarkdown } from '../lib/markdown'
import type { Turn } from '../types/agent'

const props = defineProps<{ turn: Turn; selected: boolean }>()
defineEmits<{ select: [] }>()

const html = computed(() => renderMarkdown(props.turn.answer))

const runningTool = computed(() => {
  const step = [...props.turn.steps].reverse().find((s) => s.kind === 'tool' && s.status === 'running')
  return step?.kind === 'tool' ? toolLabel(step.name) : null
})
</script>

<template>
  <article class="turn">
    <div class="bubble bubble--user">{{ turn.question }}</div>

    <div class="answer" :class="{ 'is-selected': selected }">
      <div v-if="turn.status === 'running' && !turn.answer" class="status">
        <span class="spinner" aria-hidden="true"></span>
        {{ runningTool ? `Consultando: ${runningTool}` : 'Analisando…' }}
      </div>

      <!-- eslint-disable-next-line vue/no-v-html -- sanitizado com DOMPurify em renderMarkdown -->
      <div v-if="turn.answer" class="markdown" v-html="html"></div>

      <p v-if="turn.error" class="error" role="alert">{{ turn.error }}</p>

      <footer v-if="turn.status !== 'running'" class="meta">
        <span v-if="turn.usage">{{ formatDuration(turn.usage.durationMs) }} · {{ formatUsd(turn.usage.costUsd) }}</span>
        <button type="button" class="link" @click="$emit('select')">
          {{ selected ? 'Exibindo o trace' : 'Ver trace' }}
        </button>
      </footer>
    </div>
  </article>
</template>

<style scoped>
.turn {
  display: grid;
  gap: 12px;
}

.bubble--user {
  justify-self: end;
  max-width: 80%;
  padding: 10px 14px;
  border-radius: 14px 14px 4px 14px;
  background: var(--accent);
  color: var(--on-accent);
  white-space: pre-wrap;
  overflow-wrap: anywhere;
}

.answer {
  padding: 2px 0 2px 14px;
  border-left: 2px solid transparent;
  transition: border-color 0.2s;
}

.answer.is-selected { border-left-color: var(--accent); }

.status {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  color: var(--text-muted);
}

.error {
  margin: 8px 0 0;
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--danger-bg);
  color: var(--danger);
  font-size: 14px;
}

.meta {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-top: 8px;
  font-size: 12px;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.link {
  padding: 0;
  border: 0;
  background: none;
  color: var(--accent-text);
  font: inherit;
  cursor: pointer;
}

.link:hover { text-decoration: underline; }

.spinner {
  width: 14px;
  height: 14px;
  border: 2px solid var(--border);
  border-top-color: var(--accent);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }
</style>
