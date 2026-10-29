import { computed, ref, watch } from 'vue'
import {
  type AssetInput,
  type KeyValueStorage,
  createSamplePortfolio,
  getBrowserStorage,
  loadPortfolio,
  savePortfolio,
  toRequestPayload,
} from '../lib/portfolio'
import type { PortfolioAsset } from '../types/portfolio'

/** Estado da carteira, salvo automaticamente no localStorage a cada mudança. */
export function usePortfolio(storage: KeyValueStorage | null = getBrowserStorage()) {
  const assets = ref<PortfolioAsset[]>(loadPortfolio(storage))
  /** false quando o navegador não deixou salvar: a carteira vale só para esta aba. */
  const persisted = ref(storage !== null)

  watch(assets, (value) => (persisted.value = savePortfolio(storage, value)), { deep: true })

  const payload = computed(() => toRequestPayload(assets.value))

  function add(asset: AssetInput) {
    assets.value.push({ ...asset, id: crypto.randomUUID() } as PortfolioAsset)
  }

  function update(id: string, asset: AssetInput) {
    const index = assets.value.findIndex((a) => a.id === id)
    if (index >= 0) assets.value[index] = { ...asset, id } as PortfolioAsset
  }

  function remove(id: string) {
    assets.value = assets.value.filter((a) => a.id !== id)
  }

  function loadSample() {
    assets.value = createSamplePortfolio()
  }

  function clear() {
    assets.value = []
  }

  return { assets, persisted, payload, add, update, remove, loadSample, clear }
}
