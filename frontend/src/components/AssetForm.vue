<script setup lang="ts">
import { computed, nextTick, ref, useId, watch } from 'vue'
import {
  ASSET_KINDS,
  type AssetField,
  type AssetInput,
  type FieldErrors,
  assetKindLabels,
  assetToDraft,
  isListed,
  validateDraft,
} from '../lib/portfolio'
import type { PortfolioAsset } from '../types/portfolio'

const props = defineProps<{
  /** Ativo em edição; ausente = adicionar novo. */
  asset?: PortfolioAsset
  /** Demais ativos da carteira (sem o editado), para checar repetição e limite. */
  others: PortfolioAsset[]
}>()
const emit = defineEmits<{ save: [asset: AssetInput]; cancel: [] }>()

const uid = useId()
const form = ref<HTMLFormElement | null>(null)
const draft = ref(assetToDraft(props.asset))
const errors = ref<FieldErrors>({})

watch(() => props.asset, (asset) => {
  draft.value = assetToDraft(asset)
  errors.value = {}
})

const listed = computed(() => isListed(draft.value.kind))

// Some com o erro do campo assim que o usuário volta a digitar nele.
function clearError(field: AssetField) {
  if (errors.value[field]) errors.value = { ...errors.value, [field]: undefined }
}

async function submit() {
  const result = validateDraft(draft.value, props.others)
  if (!result.ok) {
    errors.value = result.errors
    await nextTick()
    form.value?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus()
    return
  }
  emit('save', result.asset)
  if (!props.asset) {
    // Mantém o tipo escolhido para facilitar adicionar vários ativos seguidos.
    draft.value = { ...assetToDraft(), kind: draft.value.kind }
    errors.value = {}
  }
}

const fieldAttrs = (field: AssetField) => ({
  id: `${uid}-${field}`,
  'aria-invalid': errors.value[field] ? ('true' as const) : ('false' as const),
  'aria-describedby': errors.value[field] ? `${uid}-${field}-error` : undefined,
})
</script>

<template>
  <form ref="form" class="asset-form" novalidate @submit.prevent="submit">
    <fieldset class="kinds">
      <legend>Tipo de ativo</legend>
      <label v-for="kind in ASSET_KINDS" :key="kind" class="kind">
        <input v-model="draft.kind" type="radio" :name="`${uid}-kind`" :value="kind" />
        <span>{{ assetKindLabels[kind] }}</span>
      </label>
    </fieldset>

    <div v-if="listed" class="fields fields--listed">
      <div class="field">
        <label :for="`${uid}-ticker`">Ticker</label>
        <input
          v-model="draft.ticker"
          v-bind="fieldAttrs('ticker')"
          type="text"
          placeholder="PETR4"
          maxlength="7"
          autocapitalize="characters"
          autocomplete="off"
          spellcheck="false"
          class="ticker"
          @input="clearError('ticker')"
        />
        <p v-if="errors.ticker" :id="`${uid}-ticker-error`" class="error">{{ errors.ticker }}</p>
      </div>
      <div class="field">
        <label :for="`${uid}-quantity`">Quantidade</label>
        <input
          v-model="draft.quantity"
          v-bind="fieldAttrs('quantity')"
          type="text"
          inputmode="numeric"
          placeholder="100"
          autocomplete="off"
          @input="clearError('quantity')"
        />
        <p v-if="errors.quantity" :id="`${uid}-quantity-error`" class="error">{{ errors.quantity }}</p>
      </div>
      <div class="field">
        <label :for="`${uid}-averagePrice`">Preço médio (R$)</label>
        <input
          v-model="draft.averagePrice"
          v-bind="fieldAttrs('averagePrice')"
          type="text"
          inputmode="decimal"
          placeholder="35,50"
          autocomplete="off"
          @input="clearError('averagePrice')"
        />
        <p v-if="errors.averagePrice" :id="`${uid}-averagePrice-error`" class="error">{{ errors.averagePrice }}</p>
      </div>
    </div>

    <div v-else class="fields fields--fixed">
      <div class="field">
        <label :for="`${uid}-name`">Investimento</label>
        <input
          v-model="draft.name"
          v-bind="fieldAttrs('name')"
          type="text"
          placeholder="Tesouro Selic 2029"
          maxlength="60"
          autocomplete="off"
          @input="clearError('name')"
        />
        <p v-if="errors.name" :id="`${uid}-name-error`" class="error">{{ errors.name }}</p>
      </div>
      <div class="field">
        <label :for="`${uid}-amount`">Valor atual (R$)</label>
        <input
          v-model="draft.amount"
          v-bind="fieldAttrs('amount')"
          type="text"
          inputmode="decimal"
          placeholder="5.000,00"
          autocomplete="off"
          @input="clearError('amount')"
        />
        <p v-if="errors.amount" :id="`${uid}-amount-error`" class="error">{{ errors.amount }}</p>
      </div>
    </div>

    <p v-if="errors.form" class="error" role="alert">{{ errors.form }}</p>

    <div class="actions">
      <button v-if="asset" type="button" class="btn btn--ghost" @click="emit('cancel')">Cancelar</button>
      <button type="submit" class="btn">{{ asset ? 'Salvar alterações' : 'Adicionar ativo' }}</button>
    </div>
  </form>
</template>

<style scoped>
.asset-form {
  display: grid;
  gap: 14px;
}

.kinds {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 4px;
  margin: 0;
  padding: 4px;
  border: 1px solid var(--border);
  border-radius: 10px;
  background: var(--surface-2);
}

.kinds legend {
  /* Visível para leitores de tela, escondida na tela: os botões já dizem o que são. */
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip-path: inset(50%);
  white-space: nowrap;
}

.kind {
  position: relative;
}

.kind input {
  position: absolute;
  opacity: 0;
  inset: 0;
  margin: 0;
  cursor: pointer;
}

.kind span {
  display: block;
  padding: 7px 4px;
  border-radius: 7px;
  font-size: 13px;
  text-align: center;
  color: var(--text-muted);
  white-space: nowrap;
}

.kind input:checked + span {
  background: var(--surface);
  color: var(--text-strong);
  font-weight: 600;
  box-shadow: 0 1px 2px rgb(0 0 0 / 0.12);
}

.kind input:focus-visible + span {
  outline: 2px solid var(--accent);
  outline-offset: 1px;
}

.fields {
  display: grid;
  gap: 12px;
}

.fields--listed { grid-template-columns: 1fr 1fr 1.2fr; }
.fields--fixed { grid-template-columns: 1.6fr 1fr; }

.field {
  display: grid;
  gap: 4px;
  align-content: start;
  min-width: 0;
}

.field label {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-muted);
}

.field input {
  width: 100%;
  height: 38px;
  padding: 0 10px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: var(--surface);
  color: var(--text-strong);
  /* 16px evita o zoom automático do iOS ao focar o campo. */
  font: inherit;
  font-size: 16px;
  font-variant-numeric: tabular-nums;
}

.field input:focus {
  border-color: var(--accent);
  outline: none;
}

.field input[aria-invalid='true'] { border-color: var(--danger); }

.ticker { text-transform: uppercase; }
.ticker::placeholder { text-transform: none; }

.error {
  margin: 0;
  font-size: 12px;
  line-height: 1.4;
  color: var(--danger);
}

.actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}

.btn {
  height: 38px;
  padding: 0 16px;
  border: 0;
  border-radius: 8px;
  background: var(--accent);
  color: var(--on-accent);
  font: inherit;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
}

.btn--ghost {
  border: 1px solid var(--border);
  background: transparent;
  color: var(--text-strong);
}

@media (max-width: 600px) {
  .fields--listed,
  .fields--fixed { grid-template-columns: 1fr 1fr; }

  .fields--listed .field:first-child,
  .fields--fixed .field:first-child { grid-column: 1 / -1; }
}
</style>
