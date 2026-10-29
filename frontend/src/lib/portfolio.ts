import type { Turn } from '../types/agent'
import type {
  AssetKind,
  FixedIncomeAsset,
  ListedAsset,
  PortfolioAnalysis,
  PortfolioAsset,
  PortfolioPayload,
} from '../types/portfolio'

// Mesmos limites de Portfolio.Validate() no backend, que revalida tudo.
export const MAX_ASSETS = 20
export const STORAGE_KEY = 'fincopilot:portfolio:v1'
const STORAGE_VERSION = 1

/** Ordem fixa: define a cor de cada tipo no gráfico e a ordem da legenda. */
export const ASSET_KINDS: readonly AssetKind[] = ['stock', 'fii', 'etf', 'fixed_income']

export const assetKindLabels: Record<AssetKind, string> = {
  stock: 'Ações',
  fii: 'FIIs',
  etf: 'ETFs',
  fixed_income: 'Renda fixa',
}

// Subconjunto da regex do backend: lá ela também aceita índices (^BVSP), que não são ativos de carteira.
const TICKER_PATTERN = /^[A-Z0-9]{4,6}[0-9]{1,2}F?$/

export type AssetInput = Omit<ListedAsset, 'id'> | Omit<FixedIncomeAsset, 'id'>

/** Valores do formulário, como o usuário digitou (texto, aceitando vírgula decimal). */
export interface AssetDraft {
  kind: AssetKind
  ticker: string
  quantity: string
  averagePrice: string
  name: string
  amount: string
}

export type AssetField = 'ticker' | 'quantity' | 'averagePrice' | 'name' | 'amount'
export type FieldErrors = Partial<Record<AssetField | 'form', string>>

export type DraftResult = { ok: true; asset: AssetInput } | { ok: false; errors: FieldErrors }

export const isListed = (kind: AssetKind): kind is ListedAsset['kind'] => kind !== 'fixed_income'

const newId = () => crypto.randomUUID()

/**
 * Converte número digitado em pt-BR. Com vírgula, ela é o decimal e os pontos são milhar
 * ("1.234,56"). Sem vírgula, ponto seguido de grupos de 3 dígitos é milhar ("1.500") e,
 * nos demais casos, é decimal ("35.5"). Devolve NaN se o texto não for um número.
 */
export function parseDecimal(text: string): number {
  const t = text.trim().replace(/^R\$\s*/, '')
  if (/^\d{1,3}(\.\d{3})*(,\d+)?$/.test(t) || /^\d+(,\d+)?$/.test(t))
    return Number(t.replace(/\./g, '').replace(',', '.'))
  if (/^\d+\.\d+$/.test(t)) return Number(t)
  return Number.NaN
}

/** Erros por campo de um ativo já convertido. Usado pelo formulário e ao ler o localStorage. */
export function assetErrors(asset: AssetInput): FieldErrors {
  const errors: FieldErrors = {}

  if (isListed(asset.kind)) {
    const a = asset as Omit<ListedAsset, 'id'>
    if (!a.ticker) errors.ticker = 'Informe o ticker.'
    else if (!TICKER_PATTERN.test(a.ticker)) errors.ticker = 'Ticker inválido. Use o código da B3, ex.: PETR4.'

    if (Number.isNaN(a.quantity)) errors.quantity = 'Informe a quantidade.'
    else if (!Number.isInteger(a.quantity)) errors.quantity = 'Use um número inteiro de cotas.'
    else if (a.quantity < 1 || a.quantity > 10_000_000) errors.quantity = 'A quantidade deve ficar entre 1 e 10.000.000.'

    if (Number.isNaN(a.averagePrice)) errors.averagePrice = 'Informe o preço médio.'
    else if (a.averagePrice < 0.01 || a.averagePrice > 1_000_000)
      errors.averagePrice = 'O preço médio deve ficar entre R$ 0,01 e R$ 1.000.000,00.'
  } else {
    const a = asset as Omit<FixedIncomeAsset, 'id'>
    if (!a.name) errors.name = 'Informe o nome do investimento.'
    else if (a.name.length < 2 || a.name.length > 60) errors.name = 'Use de 2 a 60 caracteres.'

    if (Number.isNaN(a.amount)) errors.amount = 'Informe o valor atual.'
    else if (a.amount < 0.01 || a.amount > 100_000_000) errors.amount = 'O valor deve ficar entre R$ 0,01 e R$ 100.000.000,00.'
  }

  return errors
}

/**
 * Valida o formulário. `others` são os demais ativos da carteira (sem o que está sendo
 * editado), para checar ticker repetido e o limite de ativos.
 */
export function validateDraft(draft: AssetDraft, others: readonly PortfolioAsset[]): DraftResult {
  const asset: AssetInput = isListed(draft.kind)
    ? {
        kind: draft.kind,
        ticker: draft.ticker.trim().toUpperCase(),
        quantity: parseDecimal(draft.quantity),
        averagePrice: parseDecimal(draft.averagePrice),
      }
    : { kind: 'fixed_income', name: draft.name.trim(), amount: parseDecimal(draft.amount) }

  const errors = assetErrors(asset)

  if ('ticker' in asset && !errors.ticker && others.some((o) => 'ticker' in o && o.ticker === asset.ticker))
    errors.ticker = 'Este ticker já está na carteira.'
  if (others.length >= MAX_ASSETS) errors.form = `A carteira aceita no máximo ${MAX_ASSETS} ativos.`

  return Object.keys(errors).length > 0 ? { ok: false, errors } : { ok: true, asset }
}

/** Preenche o formulário para edição (números com vírgula decimal, sem separador de milhar). */
export function assetToDraft(asset?: PortfolioAsset): AssetDraft {
  const text = (n: number) => String(n).replace('.', ',')
  const empty: AssetDraft = { kind: 'stock', ticker: '', quantity: '', averagePrice: '', name: '', amount: '' }
  if (!asset) return empty
  return 'ticker' in asset
    ? { ...empty, kind: asset.kind, ticker: asset.ticker, quantity: text(asset.quantity), averagePrice: text(asset.averagePrice) }
    : { ...empty, kind: 'fixed_income', name: asset.name, amount: text(asset.amount) }
}

// ---------- Persistência (localStorage) ----------

export type KeyValueStorage = Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>

const isRecord = (v: unknown): v is Record<string, unknown> => typeof v === 'object' && v !== null && !Array.isArray(v)

/** Converte um item salvo em ativo válido, ou null. Não confia no formato: o storage é editável. */
function toAsset(item: unknown): AssetInput | null {
  if (!isRecord(item) || !ASSET_KINDS.includes(item.kind as AssetKind)) return null
  const kind = item.kind as AssetKind

  const asset: AssetInput | null = isListed(kind)
    ? typeof item.ticker === 'string' && typeof item.quantity === 'number' && typeof item.averagePrice === 'number'
      ? { kind, ticker: item.ticker, quantity: item.quantity, averagePrice: item.averagePrice }
      : null
    : typeof item.name === 'string' && typeof item.amount === 'number'
      ? { kind: 'fixed_income', name: item.name, amount: item.amount }
      : null

  return asset && Object.keys(assetErrors(asset)).length === 0 ? asset : null
}

export function serializePortfolio(assets: readonly PortfolioAsset[]): string {
  return JSON.stringify({ version: STORAGE_VERSION, assets })
}

/**
 * Lê a carteira salva de forma defensiva: JSON inválido ou versão desconhecida viram carteira
 * vazia; itens inválidos ou repetidos são descartados e os válidos são mantidos.
 */
export function parsePortfolio(raw: string | null): PortfolioAsset[] {
  if (!raw) return []

  let data: unknown
  try {
    data = JSON.parse(raw)
  } catch {
    return []
  }
  if (!isRecord(data) || data.version !== STORAGE_VERSION || !Array.isArray(data.assets)) return []

  const assets: PortfolioAsset[] = []
  for (const item of data.assets) {
    const asset = toAsset(item)
    if (!asset) continue
    if ('ticker' in asset && assets.some((a) => 'ticker' in a && a.ticker === asset.ticker)) continue
    if (assets.length >= MAX_ASSETS) break

    const id = isRecord(item) && typeof item.id === 'string' && item.id ? item.id : newId()
    assets.push({ ...asset, id } as PortfolioAsset)
  }
  return assets
}

/** Acessar window.localStorage já pode lançar (cookies bloqueados, alguns modos anônimos). */
export function getBrowserStorage(): KeyValueStorage | null {
  try {
    return globalThis.localStorage ?? null
  } catch {
    return null
  }
}

export function loadPortfolio(storage: KeyValueStorage | null): PortfolioAsset[] {
  try {
    return parsePortfolio(storage?.getItem(STORAGE_KEY) ?? null)
  } catch {
    return []
  }
}

/** Devolve false se não conseguiu salvar (cota cheia, storage bloqueado). A carteira segue em memória. */
export function savePortfolio(storage: KeyValueStorage | null, assets: readonly PortfolioAsset[]): boolean {
  if (!storage) return false
  try {
    if (assets.length === 0) storage.removeItem(STORAGE_KEY)
    else storage.setItem(STORAGE_KEY, serializePortfolio(assets))
    return true
  } catch {
    return false
  }
}

/** Corpo enviado ao backend junto de cada mensagem; null quando a carteira está vazia. */
export function toRequestPayload(assets: readonly PortfolioAsset[]): PortfolioPayload | null {
  if (assets.length === 0) return null
  return { assets: assets.map(({ id: _id, ...rest }) => rest) }
}

/**
 * Carteira de demonstração. Só PETR4, VALE3, ITUB4 e MGLU3 têm cotação sem token da brapi;
 * HGLG11 e BOVA11 entram de propósito para mostrar o agente lidando com cotação indisponível
 * (avaliados pelo preço médio) e para a alocação ter os quatro tipos.
 */
export function createSamplePortfolio(): PortfolioAsset[] {
  const sample: AssetInput[] = [
    { kind: 'stock', ticker: 'PETR4', quantity: 100, averagePrice: 34.2 },
    { kind: 'stock', ticker: 'VALE3', quantity: 40, averagePrice: 61.8 },
    { kind: 'stock', ticker: 'ITUB4', quantity: 80, averagePrice: 31.5 },
    { kind: 'stock', ticker: 'MGLU3', quantity: 300, averagePrice: 8.9 },
    { kind: 'fii', ticker: 'HGLG11', quantity: 15, averagePrice: 158 },
    { kind: 'etf', ticker: 'BOVA11', quantity: 20, averagePrice: 118.5 },
    { kind: 'fixed_income', name: 'Tesouro Selic 2029', amount: 8000 },
    { kind: 'fixed_income', name: 'CDB 110% do CDI', amount: 5000 },
  ]
  return sample.map((a) => ({ ...a, id: newId() }) as PortfolioAsset)
}

// ---------- Resultado da ferramenta analyze_portfolio ----------

const isNumber = (v: unknown): v is number => typeof v === 'number' && Number.isFinite(v)

/** Type guard: o output da ferramenta chega como unknown pelo SSE. */
export function isPortfolioAnalysis(value: unknown): value is PortfolioAnalysis {
  if (!isRecord(value)) return false
  const { totalValue, positions, allocation, warnings } = value
  return (
    isNumber(totalValue) &&
    Array.isArray(warnings) &&
    Array.isArray(positions) &&
    positions.every((p) => isRecord(p) && typeof p.label === 'string' && isNumber(p.marketValue) && isNumber(p.weightPercent)) &&
    Array.isArray(allocation) &&
    allocation.every((s) => isRecord(s) && ASSET_KINDS.includes(s.kind as AssetKind) && isNumber(s.value) && isNumber(s.weightPercent))
  )
}

/** Último analyze_portfolio bem-sucedido do turno; é ele que alimenta o gráfico. */
export function findPortfolioAnalysis(turn: Turn): PortfolioAnalysis | null {
  for (let i = turn.steps.length - 1; i >= 0; i--) {
    const step = turn.steps[i]
    if (step.kind === 'tool' && step.name === 'analyze_portfolio' && step.status === 'done' && !step.isError && isPortfolioAnalysis(step.output))
      return step.output
  }
  return null
}
