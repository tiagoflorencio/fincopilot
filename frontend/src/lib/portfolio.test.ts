import { describe, expect, it } from 'vitest'
import type { Turn } from '../types/agent'
import type { PortfolioAsset } from '../types/portfolio'
import {
  type AssetDraft,
  type KeyValueStorage,
  MAX_ASSETS,
  STORAGE_KEY,
  assetToDraft,
  createSamplePortfolio,
  findPortfolioAnalysis,
  loadPortfolio,
  parseDecimal,
  parsePortfolio,
  savePortfolio,
  serializePortfolio,
  toRequestPayload,
  validateDraft,
} from './portfolio'

const petr: PortfolioAsset = { id: 'a', kind: 'stock', ticker: 'PETR4', quantity: 100, averagePrice: 35.5 }
const cdb: PortfolioAsset = { id: 'b', kind: 'fixed_income', name: 'CDB 110% CDI', amount: 5000 }

const draft = (patch: Partial<AssetDraft>): AssetDraft => ({
  kind: 'stock', ticker: 'VALE3', quantity: '10', averagePrice: '61,80', name: '', amount: '', ...patch,
})

const errorsOf = (d: AssetDraft, others: PortfolioAsset[] = []) => {
  const result = validateDraft(d, others)
  return result.ok ? {} : result.errors
}

describe('parseDecimal', () => {
  it.each([
    ['35,50', 35.5],
    ['1.234,56', 1234.56],
    ['R$ 1.500', 1500],
    ['35.5', 35.5],
    ['100', 100],
  ])('converte "%s"', (text, expected) => expect(parseDecimal(text)).toBe(expected))

  it.each(['', 'abc', '1,2,3', '-5', '1.23.4'])('rejeita "%s"', (text) => expect(parseDecimal(text)).toBeNaN())
})

describe('validateDraft', () => {
  it('aceita ativo válido e normaliza o ticker', () => {
    const result = validateDraft(draft({ ticker: ' vale3 ' }), [])
    expect(result).toEqual({ ok: true, asset: { kind: 'stock', ticker: 'VALE3', quantity: 10, averagePrice: 61.8 } })
  })

  it.each(['', 'PETR', 'PETR4; DROP', '^BVSP'])('rejeita ticker "%s"', (ticker) => {
    expect(errorsOf(draft({ ticker }))).toHaveProperty('ticker')
  })

  it.each(['0', '-1', '1,5', '', '10000001'])('rejeita quantidade "%s"', (quantity) => {
    expect(errorsOf(draft({ quantity }))).toHaveProperty('quantity')
  })

  it.each(['0', '0,001', '1.000.001', 'dez'])('rejeita preço médio "%s"', (averagePrice) => {
    expect(errorsOf(draft({ averagePrice }))).toHaveProperty('averagePrice')
  })

  it('valida os campos de renda fixa sem exigir ticker', () => {
    const fixed = draft({ kind: 'fixed_income', ticker: '', name: 'Tesouro Selic', amount: '8.000,00' })
    expect(validateDraft(fixed, [])).toEqual({ ok: true, asset: { kind: 'fixed_income', name: 'Tesouro Selic', amount: 8000 } })

    expect(errorsOf({ ...fixed, name: 'X', amount: '0' })).toEqual({
      name: expect.any(String),
      amount: expect.any(String),
    })
  })

  it('rejeita ticker repetido, mas não o próprio ativo em edição', () => {
    expect(errorsOf(draft({ ticker: 'PETR4' }), [petr]).ticker).toMatch(/já está/)
    // Na edição, o chamador passa a carteira sem o ativo editado.
    expect(validateDraft(draft({ ticker: 'PETR4' }), [cdb]).ok).toBe(true)
  })

  it('respeita o limite de ativos', () => {
    const full = Array.from({ length: MAX_ASSETS }, (_, i) => ({ ...cdb, id: String(i) }))
    expect(errorsOf(draft({}), full).form).toMatch(String(MAX_ASSETS))
  })

  it('assetToDraft devolve o ativo ao formulário com vírgula decimal', () => {
    expect(assetToDraft(petr)).toMatchObject({ kind: 'stock', ticker: 'PETR4', quantity: '100', averagePrice: '35,5' })
    expect(validateDraft(assetToDraft(cdb), []).ok).toBe(true)
  })
})

describe('serialização da carteira', () => {
  it('ida e volta sem perda', () => {
    const assets = createSamplePortfolio()
    expect(parsePortfolio(serializePortfolio(assets))).toEqual(assets)
  })

  it.each([
    ['nulo', null],
    ['JSON quebrado', '{"version":1,'],
    ['versão desconhecida', JSON.stringify({ version: 2, assets: [petr] })],
    ['formato antigo (array solto)', JSON.stringify([petr])],
  ])('%s vira carteira vazia', (_, raw) => {
    expect(parsePortfolio(raw)).toEqual([])
  })

  it('descarta itens inválidos e repetidos e mantém os válidos', () => {
    const raw = JSON.stringify({
      version: 1,
      assets: [
        petr,
        { ...petr, id: 'dup' },
        { id: 'c', kind: 'stock', ticker: 'X', quantity: 1, averagePrice: 1 },
        { id: 'd', kind: 'crypto', name: 'BTC', amount: 1 },
        { id: 'e', kind: 'fixed_income', name: 'CDB', amount: '5000' },
        cdb,
      ],
    })
    expect(parsePortfolio(raw)).toEqual([petr, cdb])
  })

  it('gera id para item salvo sem id', () => {
    const { id: _id, ...noId } = petr
    const [asset] = parsePortfolio(JSON.stringify({ version: 1, assets: [noId] }))
    expect(asset.id).toEqual(expect.any(String))
    expect(asset.id).not.toBe('')
  })

  it('toRequestPayload remove os ids e devolve null para carteira vazia', () => {
    expect(toRequestPayload([petr, cdb])).toEqual({
      assets: [
        { kind: 'stock', ticker: 'PETR4', quantity: 100, averagePrice: 35.5 },
        { kind: 'fixed_income', name: 'CDB 110% CDI', amount: 5000 },
      ],
    })
    expect(toRequestPayload([])).toBeNull()
  })
})

describe('localStorage', () => {
  const memoryStorage = () => {
    const data = new Map<string, string>()
    const storage: KeyValueStorage = {
      getItem: (k) => data.get(k) ?? null,
      setItem: (k, v) => void data.set(k, v),
      removeItem: (k) => void data.delete(k),
    }
    return { storage, data }
  }

  const throwing: KeyValueStorage = {
    getItem: () => { throw new DOMException('bloqueado', 'SecurityError') },
    setItem: () => { throw new DOMException('cheio', 'QuotaExceededError') },
    removeItem: () => { throw new DOMException('bloqueado', 'SecurityError') },
  }

  it('salva e carrega; carteira vazia remove a chave', () => {
    const { storage, data } = memoryStorage()
    expect(savePortfolio(storage, [petr])).toBe(true)
    expect(loadPortfolio(storage)).toEqual([petr])

    savePortfolio(storage, [])
    expect(data.has(STORAGE_KEY)).toBe(false)
  })

  it('storage que lança erro não derruba o app', () => {
    expect(loadPortfolio(throwing)).toEqual([])
    expect(savePortfolio(throwing, [petr])).toBe(false)
    expect(loadPortfolio(null)).toEqual([])
    expect(savePortfolio(null, [petr])).toBe(false)
  })
})

describe('findPortfolioAnalysis', () => {
  const analysis = {
    asOf: '2026-10-28T15:00:00Z', totalValue: 100, investedValue: 90, gainPercent: 11.11,
    positions: [{ kind: 'stock', label: 'PETR4', marketValue: 100, weightPercent: 100 }],
    allocation: [{ kind: 'stock', value: 100, weightPercent: 100 }],
    warnings: [], source: 'teste',
  }
  const toolStep = (output: unknown, extra: object = {}) =>
    ({ kind: 'tool', id: crypto.randomUUID(), name: 'analyze_portfolio', input: {}, status: 'done', output, isError: false, ...extra }) as const
  const turnWith = (...steps: Turn['steps']): Turn => ({ id: '1', question: 'q', answer: '', status: 'done', steps })

  it('pega o último resultado bem-sucedido', () => {
    const newer = { ...analysis, totalValue: 200 }
    expect(findPortfolioAnalysis(turnWith(toolStep(analysis), toolStep(newer)))?.totalValue).toBe(200)
  })

  it('ignora erro, resultado malformado, outra ferramenta e chamada em andamento', () => {
    expect(findPortfolioAnalysis(turnWith(
      toolStep({ error: 'sem carteira' }, { isError: true }),
      toolStep({ ...analysis, allocation: [{ kind: 'crypto', value: 1, weightPercent: 1 }] }),
      toolStep(analysis, { name: 'get_stock_quote' }),
      toolStep(undefined, { status: 'running' }),
    ))).toBeNull()
  })
})
