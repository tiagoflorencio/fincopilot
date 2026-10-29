<script setup lang="ts">
import { computed } from 'vue'
import { formatBrl, formatNumber, formatPercent } from '../lib/format'
import { assetKindLabels } from '../lib/portfolio'
import type { PortfolioAnalysis } from '../types/portfolio'
import AllocationDonut from './AllocationDonut.vue'

const props = defineProps<{ analysis: PortfolioAnalysis }>()

const asOf = computed(() =>
  new Date(props.analysis.asOf).toLocaleString('pt-BR', { dateStyle: 'short', timeStyle: 'short' }),
)
const hasEstimated = computed(() => props.analysis.positions.some((p) => p.priceStatus === 'unavailable'))

const swatch = (kind: string) => ({ background: `var(--series-${kind.replace('_', '-')})` })
const gainClass = (value: number | null) => (value === null || value === 0 ? '' : value > 0 ? 'up' : 'down')
</script>

<template>
  <section class="insights" aria-label="Análise da carteira">
    <header class="head">
      <h3>Sua carteira</h3>
      <span class="as-of">Cotações de {{ asOf }}</span>
    </header>

    <dl class="stats">
      <div>
        <dt>Valor atual</dt>
        <dd>{{ formatBrl(analysis.totalValue) }}</dd>
      </div>
      <div>
        <dt>Investido</dt>
        <dd>{{ formatBrl(analysis.investedValue) }}</dd>
      </div>
      <div>
        <dt>Resultado</dt>
        <dd :class="gainClass(analysis.gainPercent)">{{ formatPercent(analysis.gainPercent, true) }}</dd>
      </div>
    </dl>

    <div class="allocation">
      <AllocationDonut :slices="analysis.allocation" :total="analysis.totalValue" />
      <ul class="legend" aria-label="Legenda da alocação">
        <li v-for="slice in analysis.allocation" :key="slice.kind">
          <span class="swatch" :style="swatch(slice.kind)" aria-hidden="true"></span>
          <span class="legend-label">{{ assetKindLabels[slice.kind] }}</span>
          <span class="legend-pct">{{ formatPercent(slice.weightPercent) }}</span>
          <span class="legend-value">{{ formatBrl(slice.value) }}</span>
        </li>
      </ul>
    </div>

    <!-- A tabela também é a versão em texto do gráfico. -->
    <div class="table-wrap" tabindex="0" aria-label="Posições da carteira">
      <table>
        <thead>
          <tr>
            <th scope="col">Ativo</th>
            <th scope="col" class="num">Qtde</th>
            <th scope="col" class="num">Preço médio</th>
            <th scope="col" class="num">Preço atual</th>
            <th scope="col" class="num">Valor</th>
            <th scope="col" class="num">Peso</th>
            <th scope="col" class="num">Resultado</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="p in analysis.positions" :key="p.label">
            <th scope="row">
              <span class="swatch" :style="swatch(p.kind)" aria-hidden="true"></span>
              <span class="asset">
                {{ p.label }}
                <small>{{ assetKindLabels[p.kind] }}</small>
              </span>
            </th>
            <td class="num">{{ p.quantity === null ? '—' : formatNumber(p.quantity) }}</td>
            <td class="num">{{ p.averagePrice === null ? '—' : formatBrl(p.averagePrice) }}</td>
            <td class="num">
              <template v-if="p.priceStatus === 'live' && p.currentPrice !== null">{{ formatBrl(p.currentPrice) }}</template>
              <span v-else-if="p.priceStatus === 'unavailable'" class="muted">indisponível*</span>
              <span v-else class="muted">informado</span>
            </td>
            <td class="num strong">{{ formatBrl(p.marketValue) }}</td>
            <td class="num">{{ formatPercent(p.weightPercent) }}</td>
            <td class="num" :class="gainClass(p.gainPercent)">{{ p.gainPercent === null ? '—' : formatPercent(p.gainPercent, true) }}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <p v-if="hasEstimated" class="footnote">* Sem cotação disponível (a brapi exige token para esses tickers): avaliado pelo preço médio.</p>

    <ul v-if="analysis.warnings.length" class="warnings">
      <li v-for="w in analysis.warnings" :key="w">
        <svg viewBox="0 0 24 24" width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true">
          <path d="M12 3l9 16H3l9-16zM12 10v4M12 17v.5" />
        </svg>
        {{ w }}
      </li>
    </ul>
  </section>
</template>

<style scoped>
.insights {
  display: grid;
  gap: 16px;
  margin-bottom: 16px;
  padding: 16px;
  border: 1px solid var(--border);
  border-radius: 12px;
  background: var(--surface);
  /* Permite a rolagem horizontal da tabela sem alargar o chat. */
  min-width: 0;
}

.head {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 4px 12px;
}

h3 {
  margin: 0;
  font-size: 15px;
  color: var(--text-strong);
}

.as-of {
  font-size: 12px;
  color: var(--text-muted);
}

.stats {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
  margin: 0;
}

.stats div {
  padding: 10px 12px;
  border-radius: 10px;
  background: var(--surface-2);
}

.stats dt {
  font-size: 12px;
  color: var(--text-muted);
}

.stats dd {
  margin: 2px 0 0;
  font-size: 16px;
  font-weight: 650;
  color: var(--text-strong);
  font-variant-numeric: tabular-nums;
  overflow-wrap: anywhere;
}

.allocation {
  display: grid;
  grid-template-columns: minmax(160px, 220px) minmax(0, 1fr);
  align-items: center;
  gap: 20px;
}

.legend {
  display: grid;
  gap: 8px;
  margin: 0;
  padding: 0;
  list-style: none;
  font-size: 14px;
  font-variant-numeric: tabular-nums;
}

.legend li {
  display: grid;
  grid-template-columns: 12px 1fr auto;
  grid-template-areas: 'swatch label pct' '. value value';
  align-items: center;
  column-gap: 8px;
}

.legend .swatch { grid-area: swatch; }
.legend-label { grid-area: label; color: var(--text-strong); }
.legend-pct { grid-area: pct; font-weight: 600; color: var(--text-strong); }
.legend-value { grid-area: value; font-size: 12px; color: var(--text-muted); }

.swatch {
  display: inline-block;
  flex: none;
  width: 12px;
  height: 12px;
  border-radius: 3px;
}

.table-wrap {
  overflow-x: auto;
  margin: 0 -4px;
  padding: 0 4px;
}

.table-wrap:focus-visible { outline-offset: 0; }

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}

th,
td {
  padding: 7px 8px;
  border-bottom: 1px solid var(--border);
  text-align: left;
}

thead th {
  font-size: 12px;
  font-weight: 600;
  color: var(--text-muted);
}

tbody th {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
  color: var(--text-strong);
}

.asset {
  display: grid;
  line-height: 1.25;
}

.asset small {
  font-size: 11px;
  font-weight: 400;
  color: var(--text-muted);
}

.num { text-align: right; }
.strong { color: var(--text-strong); font-weight: 600; }
.muted { color: var(--text-muted); }

/* Ganho/perda: além da cor, o sinal (+/−) carrega a informação. */
.up { color: var(--accent-text); }
.down { color: var(--danger); }

.footnote {
  margin: -8px 0 0;
  font-size: 12px;
  color: var(--text-muted);
}

.warnings {
  display: grid;
  gap: 6px;
  margin: 0;
  padding: 0;
  list-style: none;
  font-size: 13px;
  color: var(--text);
}

.warnings li {
  display: flex;
  gap: 8px;
  align-items: baseline;
}

.warnings svg {
  flex: none;
  position: relative;
  top: 2px;
  color: var(--text-muted);
}

@media (max-width: 600px) {
  .insights { padding: 12px; }

  .stats { grid-template-columns: 1fr 1fr; }
  .stats div:last-child { grid-column: 1 / -1; }

  .allocation {
    grid-template-columns: 1fr;
    justify-items: center;
  }

  .legend { width: 100%; }
}
</style>
