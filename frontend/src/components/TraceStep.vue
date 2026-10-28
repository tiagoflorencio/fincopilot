<script setup lang="ts">
import { formatDuration, formatJson, toolLabel } from '../lib/format'
import type { TraceStep } from '../types/agent'

defineProps<{ step: TraceStep }>()
</script>

<template>
  <li v-if="step.kind === 'iteration'" class="step step--iteration">
    <span class="iteration-label">Iteração {{ step.iteration }}</span>
  </li>

  <li v-else-if="step.kind === 'thinking'" class="step step--thinking">
    <span class="dot" aria-hidden="true"></span>
    <details>
      <summary>Raciocínio</summary>
      <p class="prose-sm">{{ step.text }}</p>
    </details>
  </li>

  <li v-else-if="step.kind === 'note'" class="step step--note">
    <span class="dot" aria-hidden="true"></span>
    <p class="prose-sm">{{ step.text }}</p>
  </li>

  <li v-else class="step step--tool" :class="{ 'is-error': step.isError, 'is-running': step.status === 'running' }">
    <span class="dot" aria-hidden="true"></span>
    <details>
      <summary>
        <span class="tool-label">{{ toolLabel(step.name) }}</span>
        <code class="tool-name">{{ step.name }}</code>
        <span class="tool-meta">
          <span v-if="step.status === 'running'" class="spinner" aria-label="executando"></span>
          <template v-else>
            <span v-if="step.isError" class="badge badge--error">erro</span>
            {{ formatDuration(step.durationMs ?? 0) }}
          </template>
        </span>
      </summary>
      <div class="io">
        <span class="io-label">Entrada</span>
        <pre>{{ formatJson(step.input) }}</pre>
        <template v-if="step.status === 'done'">
          <span class="io-label">Saída</span>
          <pre>{{ formatJson(step.output) }}</pre>
        </template>
      </div>
    </details>
  </li>
</template>

<style scoped>
.step {
  position: relative;
  padding: 0 0 14px 22px;
  list-style: none;
}

.step::before {
  content: '';
  position: absolute;
  left: 4px;
  top: 14px;
  bottom: 0;
  width: 1px;
  background: var(--border);
}

.dot {
  position: absolute;
  left: 0;
  top: 6px;
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: var(--text-muted);
}

.step--tool .dot { background: var(--accent); }
.step--tool.is-error .dot { background: var(--danger); }
.step--thinking .dot { background: var(--violet); }

.step--iteration {
  padding-left: 0;
  padding-bottom: 10px;
}

.step--iteration::before { display: none; }

.iteration-label {
  font-size: 11px;
  font-weight: 600;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--text-muted);
}

details > summary {
  cursor: pointer;
  list-style: none;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  font-size: 14px;
  color: var(--text-strong);
}

details > summary::-webkit-details-marker { display: none; }

.tool-label { font-weight: 600; }

.tool-name {
  font-family: var(--mono);
  font-size: 12px;
  color: var(--text-muted);
}

.tool-meta {
  margin-left: auto;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.badge {
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
}

.badge--error {
  background: var(--danger-bg);
  color: var(--danger);
}

.prose-sm {
  margin: 4px 0 0;
  font-size: 13px;
  line-height: 1.55;
  color: var(--text);
  white-space: pre-wrap;
}

.step--note .prose-sm { margin-top: 0; font-style: italic; }

.io {
  margin-top: 8px;
  display: grid;
  gap: 4px;
}

.io-label {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--text-muted);
}

pre {
  margin: 0 0 6px;
  padding: 10px;
  max-height: 260px;
  overflow: auto;
  border-radius: 6px;
  background: var(--code-bg);
  font-family: var(--mono);
  font-size: 12px;
  line-height: 1.5;
  color: var(--text-strong);
}

.spinner {
  width: 12px;
  height: 12px;
  border: 2px solid var(--border);
  border-top-color: var(--accent);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
}

@keyframes spin { to { transform: rotate(360deg); } }
</style>
