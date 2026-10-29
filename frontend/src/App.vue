<script setup lang="ts">
import { ref } from 'vue'
import ChatPanel from './components/ChatPanel.vue'
import PortfolioDialog from './components/PortfolioDialog.vue'
import TracePanel from './components/TracePanel.vue'
import { useAgentChat } from './composables/useAgentChat'
import { usePortfolio } from './composables/usePortfolio'
import { formatTokens, formatUsd } from './lib/format'

const portfolio = usePortfolio()
// A carteira é lida no envio de cada mensagem: o agente sempre vê a versão atual.
const { turns, selectedTurn, selectedTurnId, isRunning, totals, send, stop, reset, select } = useAgentChat(
  () => portfolio.payload.value,
)

const portfolioOpen = ref(false)

function analyzePortfolio() {
  portfolioOpen.value = false
  send('Analise minha carteira.')
}

// Atalho para quem visita o portfólio: carrega o exemplo e já pergunta.
function trySample() {
  portfolio.loadSample()
  send('Analise minha carteira.')
}
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
        <button
          type="button"
          class="portfolio-btn"
          :aria-label="`Minha carteira, ${portfolio.assets.value.length} ativos`"
          aria-haspopup="dialog"
          @click="portfolioOpen = true"
        >
          <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M21 12a9 9 0 1 1-9-9v9h9z" />
            <path d="M15 3.5A9 9 0 0 1 20.5 9H15V3.5z" />
          </svg>
          <span class="portfolio-label">Minha carteira</span>
          <span v-if="portfolio.assets.value.length" class="badge">{{ portfolio.assets.value.length }}</span>
        </button>
        <button type="button" class="new-chat" :disabled="turns.length === 0" @click="reset">Nova conversa</button>
      </div>
    </header>

    <main class="layout">
      <ChatPanel
        :turns="turns"
        :selected-turn-id="selectedTurnId"
        :is-running="isRunning"
        :has-portfolio="portfolio.assets.value.length > 0"
        @send="send"
        @select="select"
        @stop="stop"
        @open-portfolio="portfolioOpen = true"
        @try-sample="trySample"
      />
      <TracePanel :turn="selectedTurn" />
    </main>

    <PortfolioDialog
      v-model:open="portfolioOpen"
      :assets="portfolio.assets.value"
      :persisted="portfolio.persisted.value"
      :can-analyze="!isRunning"
      @add="portfolio.add"
      @update="portfolio.update"
      @remove="portfolio.remove"
      @load-sample="portfolio.loadSample"
      @clear="portfolio.clear"
      @analyze="analyzePortfolio"
    />
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
  gap: 10px;
}

.portfolio-btn {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  height: 34px;
  padding: 0 12px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: transparent;
  color: var(--text-strong);
  font: inherit;
  font-size: 13px;
  cursor: pointer;
}

.portfolio-btn:hover { border-color: var(--accent); }

.badge {
  min-width: 20px;
  padding: 0 6px;
  border-radius: 10px;
  background: var(--accent-bg);
  color: var(--accent-text);
  font-size: 12px;
  font-weight: 600;
  line-height: 20px;
  text-align: center;
  font-variant-numeric: tabular-nums;
}

.totals {
  font-size: 12px;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.new-chat {
  height: 34px;
  padding: 0 12px;
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

/* Celular: o botão da carteira vira só ícone (o aria-label continua dizendo o que é). */
@media (max-width: 600px) {
  .topbar { padding: 10px 16px; }
  .portfolio-label { display: none; }
}
</style>
