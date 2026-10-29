<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { formatBrl, formatNumber } from '../lib/format'
import { type AssetInput, MAX_ASSETS, assetKindLabels } from '../lib/portfolio'
import type { PortfolioAsset } from '../types/portfolio'
import AssetForm from './AssetForm.vue'

const props = defineProps<{ assets: PortfolioAsset[]; persisted: boolean; canAnalyze: boolean }>()
const open = defineModel<boolean>('open', { required: true })
const emit = defineEmits<{
  add: [asset: AssetInput]
  update: [id: string, asset: AssetInput]
  remove: [id: string]
  loadSample: []
  clear: []
  analyze: []
}>()

// <dialog> nativo com showModal(): foco preso no painel, Esc fecha e o resto da página fica inerte.
const dialog = ref<HTMLDialogElement | null>(null)
const editingId = ref<string | null>(null)

const editing = computed(() => props.assets.find((a) => a.id === editingId.value))
const others = computed(() => props.assets.filter((a) => a.id !== editingId.value))
const total = computed(() => props.assets.reduce((sum, a) => sum + ('ticker' in a ? a.quantity * a.averagePrice : a.amount), 0))

watch(open, (value) => {
  if (value && !dialog.value?.open) dialog.value?.showModal()
  if (!value && dialog.value?.open) dialog.value.close()
  if (!value) editingId.value = null
})

function onSave(asset: AssetInput) {
  if (editingId.value) emit('update', editingId.value, asset)
  else emit('add', asset)
  editingId.value = null
}

function onRemove(asset: PortfolioAsset) {
  if (asset.id === editingId.value) editingId.value = null
  emit('remove', asset.id)
}

function onLoadSample() {
  if (props.assets.length > 0 && !confirm('Substituir a sua carteira pela carteira de exemplo?')) return
  editingId.value = null
  emit('loadSample')
}

function onClear() {
  if (!confirm('Remover todos os ativos da carteira?')) return
  editingId.value = null
  emit('clear')
}

// Clique no fundo escurecido (fora do painel) fecha.
function onDialogClick(e: MouseEvent) {
  if (e.target === dialog.value) open.value = false
}
</script>

<template>
  <dialog ref="dialog" class="portfolio-dialog" aria-labelledby="portfolio-title" @close="open = false" @click="onDialogClick">
    <div class="panel">
      <header class="head">
        <div>
          <h2 id="portfolio-title">Minha carteira</h2>
          <p class="sub">Enviada ao agente junto de cada mensagem. Fica salva só neste navegador.</p>
        </div>
        <button type="button" class="icon-btn" aria-label="Fechar" @click="open = false">
          <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round">
            <path d="M6 6l12 12M18 6L6 18" />
          </svg>
        </button>
      </header>

      <div class="body">
        <section class="card" :aria-label="editing ? 'Editar ativo' : 'Adicionar ativo'">
          <h3>{{ editing ? `Editar ${'ticker' in editing ? editing.ticker : editing.name}` : 'Adicionar ativo' }}</h3>
          <AssetForm :asset="editing" :others="others" @save="onSave" @cancel="editingId = null" />
        </section>

        <p v-if="!persisted" class="warn" role="status">
          O navegador não permitiu salvar a carteira. Ela vale só enquanto esta aba estiver aberta.
        </p>

        <section aria-labelledby="positions-title">
          <div class="list-head">
            <h3 id="positions-title">Ativos <span class="count">{{ assets.length }}/{{ MAX_ASSETS }}</span></h3>
            <span v-if="assets.length" class="total">Custo total {{ formatBrl(total) }}</span>
          </div>

          <div v-if="assets.length === 0" class="empty">
            <p>Nenhum ativo ainda. Adicione acima ou use a carteira de exemplo para testar.</p>
            <button type="button" class="btn" @click="onLoadSample">Carregar exemplo</button>
          </div>

          <ul v-else class="assets">
            <li v-for="asset in assets" :key="asset.id" class="asset" :class="{ 'is-editing': asset.id === editingId }">
              <span class="swatch" :style="{ background: `var(--series-${asset.kind.replace('_', '-')})` }" aria-hidden="true"></span>
              <div class="asset-main">
                <strong>{{ 'ticker' in asset ? asset.ticker : asset.name }}</strong>
                <span class="asset-detail">
                  {{ assetKindLabels[asset.kind] }} ·
                  <template v-if="'ticker' in asset">{{ formatNumber(asset.quantity) }} × {{ formatBrl(asset.averagePrice) }}</template>
                  <template v-else>{{ formatBrl(asset.amount) }}</template>
                </span>
              </div>
              <button
                type="button"
                class="icon-btn"
                :aria-label="`Editar ${'ticker' in asset ? asset.ticker : asset.name}`"
                @click="editingId = asset.id"
              >
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M4 20h4L19 9l-4-4L4 16v4z" />
                </svg>
              </button>
              <button
                type="button"
                class="icon-btn icon-btn--danger"
                :aria-label="`Remover ${'ticker' in asset ? asset.ticker : asset.name}`"
                @click="onRemove(asset)"
              >
                <svg viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                  <path d="M5 7h14M10 7V5h4v2M7 7l1 12h8l1-12" />
                </svg>
              </button>
            </li>
          </ul>
        </section>
      </div>

      <footer class="foot">
        <div class="foot-secondary">
          <button v-if="assets.length" type="button" class="btn btn--ghost" @click="onLoadSample">Carregar exemplo</button>
          <button v-if="assets.length" type="button" class="btn btn--ghost btn--danger" @click="onClear">Limpar</button>
        </div>
        <button type="button" class="btn" :disabled="!assets.length || !canAnalyze" @click="emit('analyze')">
          Analisar no chat
        </button>
      </footer>
    </div>
  </dialog>
</template>

<style scoped>
.portfolio-dialog {
  /* Gaveta à direita em telas grandes. */
  width: min(480px, 100vw);
  max-width: 100vw;
  height: 100dvh;
  max-height: 100dvh;
  margin: 0 0 0 auto;
  padding: 0;
  border: 0;
  border-left: 1px solid var(--border);
  background: var(--surface);
  color: var(--text);
}

.portfolio-dialog::backdrop {
  background: rgb(5 10 18 / 0.45);
}

.portfolio-dialog[open] {
  animation: slide-in 0.18s ease-out;
}

@keyframes slide-in {
  from { transform: translateX(24px); opacity: 0; }
}

@media (prefers-reduced-motion: reduce) {
  .portfolio-dialog[open] { animation: none; }
}

.panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  padding: 18px 20px 14px;
  border-bottom: 1px solid var(--border);
}

h2 {
  margin: 0;
  font-size: 17px;
  color: var(--text-strong);
}

h3 {
  margin: 0 0 12px;
  font-size: 14px;
  color: var(--text-strong);
}

.sub {
  margin: 2px 0 0;
  font-size: 13px;
  color: var(--text-muted);
}

.body {
  flex: 1;
  overflow-y: auto;
  display: grid;
  align-content: start;
  gap: 20px;
  padding: 16px 20px;
}

.card {
  padding: 14px;
  border: 1px solid var(--border);
  border-radius: 12px;
  background: var(--surface-2);
}

.warn {
  margin: 0;
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--danger-bg);
  color: var(--danger);
  font-size: 13px;
}

.list-head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
}

.count,
.total {
  font-size: 12px;
  font-weight: 400;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.empty {
  display: grid;
  justify-items: start;
  gap: 10px;
  padding: 16px;
  border: 1px dashed var(--border);
  border-radius: 12px;
}

.empty p {
  margin: 0;
  font-size: 14px;
  color: var(--text-muted);
}

.assets {
  display: grid;
  gap: 2px;
  margin: 0;
  padding: 0;
  list-style: none;
}

.asset {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 6px 8px 10px;
  border-radius: 8px;
}

.asset.is-editing { background: var(--accent-bg); }

.swatch {
  flex: none;
  width: 10px;
  height: 10px;
  border-radius: 3px;
}

.asset-main {
  flex: 1;
  min-width: 0;
  display: grid;
}

.asset-main strong {
  font-size: 14px;
  color: var(--text-strong);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.asset-detail {
  font-size: 12px;
  color: var(--text-muted);
  font-variant-numeric: tabular-nums;
}

.icon-btn {
  flex: none;
  display: grid;
  place-items: center;
  /* Área de toque de 36px mesmo com ícone pequeno. */
  width: 36px;
  height: 36px;
  border: 0;
  border-radius: 8px;
  background: transparent;
  color: var(--text-muted);
  cursor: pointer;
}

.icon-btn:hover {
  background: var(--surface-2);
  color: var(--text-strong);
}

.icon-btn--danger:hover { color: var(--danger); }

.foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 12px 20px;
  padding-bottom: max(12px, env(safe-area-inset-bottom));
  border-top: 1px solid var(--border);
}

.foot-secondary {
  display: flex;
  gap: 8px;
}

.btn {
  height: 38px;
  padding: 0 14px;
  border: 0;
  border-radius: 8px;
  background: var(--accent);
  color: var(--on-accent);
  font: inherit;
  font-size: 14px;
  font-weight: 600;
  white-space: nowrap;
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
  font-weight: 500;
}

.btn--danger { color: var(--danger); }

/* Celular: tela cheia. */
@media (max-width: 600px) {
  .portfolio-dialog {
    width: 100vw;
    border-left: 0;
  }

  .head,
  .body,
  .foot { padding-left: 16px; padding-right: 16px; }

  /* Três botões não cabem numa linha de 360px: a ação principal desce e ocupa a largura toda. */
  .foot { flex-wrap: wrap; }
  .foot > .btn { flex: 1 0 100%; }
}
</style>
