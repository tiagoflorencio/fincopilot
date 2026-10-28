<script setup lang="ts">
import ChatPanel from './components/ChatPanel.vue'
import TracePanel from './components/TracePanel.vue'
import { useAgentChat } from './composables/useAgentChat'
import { formatTokens, formatUsd } from './lib/format'

const { turns, selectedTurn, selectedTurnId, isRunning, totals, send, stop, reset, select } = useAgentChat()
</script>

<template>
  <div class="app">
    <header class="topbar">
      <div class="brand">
        <span class="logo" aria-hidden="true">
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
            <path d="M3 17l6-6 4 4 8-8" />
            <path d="M15 7h6v6" />
          </svg>
        </span>
        <div>
          <strong>FinCopilot</strong>
          <span class="tagline">Agente de educação financeira</span>
        </div>
      </div>

      <div class="session">
        <span v-if="totals.tokens > 0" class="totals" title="Total da sessão">
          {{ formatTokens(totals.tokens) }} tokens · {{ formatUsd(totals.costUsd) }}
        </span>
        <button type="button" class="new-chat" :disabled="turns.length === 0" @click="reset">Nova conversa</button>
      </div>
    </header>

    <main class="layout">
      <ChatPanel
        :turns="turns"
        :selected-turn-id="selectedTurnId"
        :is-running="isRunning"
        @send="send"
        @select="select"
        @stop="stop"
      />
      <TracePanel :turn="selectedTurn" />
    </main>
  </div>
</template>

<style scoped>
.app {
  display: flex;
  flex-direction: column;
  height: 100dvh;
}

.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 12px 20px;
  border-bottom: 1px solid var(--border);
  background: var(--surface);
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
}

.brand strong {
  display: block;
  font-size: 15px;
  color: var(--text-strong);
}

.tagline {
  font-size: 12px;
  color: var(--text-muted);
}

.logo {
  display: grid;
  place-items: center;
  width: 32px;
  height: 32px;
  border-radius: 8px;
  background: var(--accent);
  color: var(--on-accent);
}

.session {
  display: flex;
  align-items: center;
  gap: 14px;
}

.totals {
  font-size: 12px;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.new-chat {
  padding: 7px 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: transparent;
  color: var(--text-strong);
  font: inherit;
  font-size: 13px;
  cursor: pointer;
}

.new-chat:disabled {
  opacity: 0.4;
  cursor: default;
}

.layout {
  flex: 1;
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(320px, 420px);
  min-height: 0;
}

@media (max-width: 900px) {
  .layout {
    grid-template-columns: 1fr;
    grid-template-rows: minmax(0, 1fr) minmax(0, 40%);
  }

  .layout :deep(.trace) {
    border-left: 0;
    border-top: 1px solid var(--border);
  }

  .tagline,
  .totals { display: none; }
}
</style>
