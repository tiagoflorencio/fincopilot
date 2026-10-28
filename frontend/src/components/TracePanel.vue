<script setup lang="ts">
import { computed } from 'vue'
import { formatDuration, formatTokens, formatUsd } from '../lib/format'
import type { Turn } from '../types/agent'
import TraceStep from './TraceStep.vue'

const props = defineProps<{ turn: Turn | null }>()

const toolCount = computed(() => props.turn?.steps.filter((s) => s.kind === 'tool').length ?? 0)
</script>

<template>
  <aside class="trace" aria-label="Trace do agente">
    <header class="trace-header">
      <h2>Trace do agente</h2>
      <p v-if="!turn" class="hint">Cada passo do agente aparece aqui: raciocínio, ferramentas chamadas, entradas e saídas.</p>
      <p v-else class="question" :title="turn.question">“{{ turn.question }}”</p>
    </header>

    <dl v-if="turn?.usage" class="stats">
      <div>
        <dt>Iterações</dt>
        <dd>{{ turn.usage.iterations }}</dd>
      </div>
      <div>
        <dt>Ferramentas</dt>
        <dd>{{ toolCount }}</dd>
      </div>
      <div>
        <dt>Tempo</dt>
        <dd>{{ formatDuration(turn.usage.durationMs) }}</dd>
      </div>
      <div>
        <dt>Custo</dt>
        <dd>{{ formatUsd(turn.usage.costUsd) }}</dd>
      </div>
      <div class="tokens">
        <dt>Tokens</dt>
        <dd>
          {{ formatTokens(turn.usage.inputTokens) }} entrada ·
          {{ formatTokens(turn.usage.outputTokens) }} saída ·
          {{ formatTokens(turn.usage.cacheReadTokens) }} cache
        </dd>
      </div>
    </dl>

    <ol v-if="turn" class="timeline">
      <TraceStep v-for="(step, i) in turn.steps" :key="i" :step="step" />
      <li v-if="turn.status === 'running'" class="waiting">Aguardando o modelo…</li>
    </ol>
  </aside>
</template>

<style scoped>
.trace {
  display: flex;
  flex-direction: column;
  min-height: 0;
  border-left: 1px solid var(--border);
  background: var(--surface-2);
}

.trace-header {
  padding: 20px 20px 12px;
}

h2 {
  margin: 0;
  font-size: 15px;
  font-weight: 600;
  color: var(--text-strong);
}

.hint,
.question {
  margin: 6px 0 0;
  font-size: 13px;
  color: var(--text-muted);
}

.question {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 1px;
  margin: 0 20px 16px;
  border: 1px solid var(--border);
  border-radius: 8px;
  overflow: hidden;
  background: var(--border);
}

.stats > div {
  padding: 8px 10px;
  background: var(--surface);
}

.stats .tokens { grid-column: 1 / -1; }

dt {
  font-size: 11px;
  color: var(--text-muted);
}

dd {
  margin: 2px 0 0;
  font-size: 14px;
  font-weight: 600;
  color: var(--text-strong);
  font-variant-numeric: tabular-nums;
}

.tokens dd {
  font-size: 12px;
  font-weight: 500;
}

.timeline {
  flex: 1;
  margin: 0;
  padding: 4px 20px 20px;
  overflow-y: auto;
}

.waiting {
  list-style: none;
  padding-left: 22px;
  font-size: 13px;
  color: var(--text-muted);
  animation: pulse 1.4s ease-in-out infinite;
}

@keyframes pulse { 50% { opacity: 0.4; } }
</style>
