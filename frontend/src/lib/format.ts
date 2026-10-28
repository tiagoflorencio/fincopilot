const usd = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD', maximumFractionDigits: 4 })
const integer = new Intl.NumberFormat('pt-BR')

export const formatUsd = (value: number) => usd.format(value)

export const formatTokens = (value: number) =>
  value >= 10_000 ? `${(value / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 1 })}k` : integer.format(value)

export const formatDuration = (ms: number) =>
  ms < 1000 ? `${ms} ms` : `${(ms / 1000).toLocaleString('pt-BR', { maximumFractionDigits: 1 })} s`

export const formatJson = (value: unknown) => JSON.stringify(value, null, 2)

const toolLabels: Record<string, string> = {
  get_stock_quote: 'Cotação na B3',
  get_economic_indicator: 'Indicador do Banco Central',
  simulate_compound_interest: 'Simulação de juros compostos',
}

export const toolLabel = (name: string) => toolLabels[name] ?? name
