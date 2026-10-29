<script setup lang="ts">
import { computed, nextTick, ref, watch } from 'vue'
import type { Turn } from '../types/agent'
import ChatMessage from './ChatMessage.vue'

const props = defineProps<{ turns: Turn[]; selectedTurnId: string | null; isRunning: boolean; hasPortfolio: boolean }>()
const emit = defineEmits<{ send: [message: string]; select: [id: string]; stop: []; openPortfolio: []; trySample: [] }>()

const draft = ref('')
const scroller = ref<HTMLElement | null>(null)

const generalSuggestions = [
  'Qual a Selic hoje e quanto rende R$ 10 mil em um ano num CDB a 100% do CDI?',
  'Se eu investir R$ 500 por mês a 11% ao ano por 20 anos, quanto terei? Considere 4% de inflação.',
  'Compare o desempenho de hoje de PETR4, VALE3 e ITUB4.',
  'O que é o IPCA e como a inflação atual afeta meus investimentos?',
]

// Com carteira cadastrada, as sugestões usam a analyze_portfolio (e combinam com outras ferramentas).
const portfolioSuggestions = [
  'Analise a diversificação da minha carteira.',
  'Quanto minha carteira rendeu em relação ao que investi?',
  'Qual posição pesa mais na minha carteira e que risco isso traz?',
  'Se eu aportar R$ 500 por mês nessa carteira a 10% ao ano, quanto terei em 10 anos?',
]

const suggestions = computed(() => (props.hasPortfolio ? portfolioSuggestions : generalSuggestions))

function submit(text = draft.value) {
  if (!text.trim() || props.isRunning) return
  emit('send', text)
  draft.value = ''
}

function onKeydown(e: KeyboardEvent) {
  if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) {
    e.preventDefault()
    submit()
  }
}

// Acompanha a resposta enquanto ela chega.
watch(
  () => props.turns.map((t) => t.answer.length + t.steps.length).join(),
  async () => {
    await nextTick()
    scroller.value?.scrollTo({ top: scroller.value.scrollHeight, behavior: 'smooth' })
  },
)
</script>

<template>
  <section class="chat" aria-label="Conversa">
    <div ref="scroller" class="messages">
      <div v-if="turns.length === 0" class="empty">
        <h1>Pergunte sobre seus investimentos</h1>
        <p>
          O agente consulta dados reais da B3 e do Banco Central e faz as contas com ferramentas determinísticas.
          Veja cada passo no painel de trace.
        </p>
        <ul class="suggestions">
          <li v-for="s in suggestions" :key="s">
            <button type="button" @click="submit(s)">{{ s }}</button>
          </li>
        </ul>
        <div v-if="!hasPortfolio" class="portfolio-cta">
          <span>Tem uma carteira? O agente analisa alocação, pesos e resultado.</span>
          <div class="cta-actions">
            <button type="button" class="link-btn" @click="emit('openPortfolio')">Montar minha carteira</button>
            <button type="button" class="link-btn" @click="emit('trySample')">Testar com a carteira de exemplo</button>
          </div>
        </div>
      </div>

      <ChatMessage
        v-for="turn in turns"
        :key="turn.id"
        :turn="turn"
        :selected="turn.id === selectedTurnId"
        @select="emit('select', turn.id)"
      />
    </div>

    <form class="composer" @submit.prevent="submit()">
      <textarea
        v-model="draft"
        rows="1"
        placeholder="Ex.: Quanto rende R$ 1.000 no Tesouro Selic em 2 anos?"
        aria-label="Mensagem"
        maxlength="4000"
        @keydown="onKeydown"
      ></textarea>
      <button v-if="isRunning" type="button" class="btn btn--ghost" @click="emit('stop')">Parar</button>
      <button v-else type="submit" class="btn" :disabled="!draft.trim()">Enviar</button>
    </form>
    <p class="disclaimer">Conteúdo educativo. Não é recomendação de investimento.</p>
  </section>
</template>

<style scoped>
.chat {
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.messages {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: 28px;
  padding: 28px max(16px, calc((100% - 760px) / 2));
}

.empty {
  margin: auto 0;
  padding: 24px 0;
}

.empty h1 {
  margin: 0 0 8px;
  font-size: 26px;
  font-weight: 650;
  letter-spacing: -0.01em;
  color: var(--text-strong);
}

.empty p {
  margin: 0 0 20px;
  max-width: 60ch;
  color: var(--text-muted);
}

.suggestions {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 10px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.suggestions button {
  width: 100%;
  height: 100%;
  padding: 12px 14px;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface);
  color: var(--text);
  font: inherit;
  font-size: 14px;
  text-align: left;
  cursor: pointer;
  transition: border-color 0.15s, background 0.15s;
}

.suggestions button:hover {
  border-color: var(--accent);
  background: var(--accent-bg);
}

.portfolio-cta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 8px 16px;
  margin-top: 14px;
  padding: 12px 14px;
  border: 1px dashed var(--border);
  border-radius: 10px;
  font-size: 14px;
  color: var(--text-muted);
}

.cta-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 16px;
}

.link-btn {
  padding: 4px 0;
  border: 0;
  background: none;
  color: var(--accent-text);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

.link-btn:hover { text-decoration: underline; }

.composer {
  display: flex;
  gap: 8px;
  align-items: flex-end;
  margin: 0 max(16px, calc((100% - 760px) / 2));
  padding: 8px;
  border: 1px solid var(--border);
  border-radius: 12px;
  background: var(--surface);
}

.composer:focus-within { border-color: var(--accent); }

textarea {
  flex: 1;
  min-height: 40px;
  max-height: 160px;
  padding: 9px 8px;
  border: 0;
  outline: none;
  resize: none;
  field-sizing: content;
  background: transparent;
  color: var(--text-strong);
  font: inherit;
}

.btn {
  height: 40px;
  padding: 0 18px;
  border: 0;
  border-radius: 8px;
  background: var(--accent);
  color: var(--on-accent);
  font: inherit;
  font-weight: 600;
  cursor: pointer;
}

.btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.btn--ghost {
  border: 1px solid var(--border);
  background: transparent;
  color: var(--text-strong);
}

.disclaimer {
  margin: 8px 16px 14px;
  font-size: 12px;
  text-align: center;
  color: var(--text-muted);
}
</style>
