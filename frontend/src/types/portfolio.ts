// Carteira do painel "Minha carteira" (espelha Tools/Portfolio.cs no backend).
export type ListedKind = 'stock' | 'fii' | 'etf'
export type AssetKind = ListedKind | 'fixed_income'

export interface ListedAsset {
  id: string
  kind: ListedKind
  ticker: string
  quantity: number
  averagePrice: number
}

/** Renda fixa não tem cotação: o usuário informa o valor atual. */
export interface FixedIncomeAsset {
  id: string
  kind: 'fixed_income'
  name: string
  amount: number
}

export type PortfolioAsset = ListedAsset | FixedIncomeAsset

/** O que vai no corpo de POST /api/chat (sem os ids, que só existem no frontend). */
export interface PortfolioPayload {
  assets: Array<Omit<ListedAsset, 'id'> | Omit<FixedIncomeAsset, 'id'>>
}

// Resultado da ferramenta analyze_portfolio (espelha Tools/AnalyzePortfolioTool.cs).
export interface PortfolioPosition {
  kind: AssetKind
  label: string
  quantity: number | null
  averagePrice: number | null
  currentPrice: number | null
  investedValue: number
  marketValue: number
  weightPercent: number
  gainPercent: number | null
  /** live = cotação da brapi; unavailable = avaliado pelo preço médio; manual = valor informado. */
  priceStatus: 'live' | 'unavailable' | 'manual'
}

export interface AllocationSlice {
  kind: AssetKind
  value: number
  weightPercent: number
}

export interface PortfolioAnalysis {
  asOf: string
  totalValue: number
  investedValue: number
  gainPercent: number
  positions: PortfolioPosition[]
  allocation: AllocationSlice[]
  warnings: string[]
  source: string
}
