import { describe, expect, it } from 'vitest'
import type { Turn } from '../types/agent'
import { applyEvent } from './turnReducer'

const newTurn = (): Turn => ({ id: '1', question: 'q', answer: '', status: 'running', steps: [] })

describe('applyEvent', () => {
  it('separa narração entre ferramentas da resposta final', () => {
    const turn = newTurn()
    applyEvent(turn, { type: 'text', text: 'Vou buscar a Selic.', isFinal: false })
    applyEvent(turn, { type: 'text', text: 'A Selic está em 13,75%.', isFinal: true })

    expect(turn.answer).toBe('A Selic está em 13,75%.')
    expect(turn.steps).toEqual([{ kind: 'note', text: 'Vou buscar a Selic.' }])
  })

  it('associa o resultado à chamada de ferramenta pelo id', () => {
    const turn = newTurn()
    applyEvent(turn, { type: 'tool_call', id: 'a', name: 'get_stock_quote', input: {} })
    applyEvent(turn, { type: 'tool_call', id: 'b', name: 'get_economic_indicator', input: {} })
    applyEvent(turn, { type: 'tool_result', id: 'b', name: 'get_economic_indicator', output: { ok: 1 }, isError: false, durationMs: 120 })

    const [a, b] = turn.steps
    expect(a).toMatchObject({ id: 'a', status: 'running' })
    expect(b).toMatchObject({ id: 'b', status: 'done', durationMs: 120, output: { ok: 1 } })
  })

  it('erro não é sobrescrito pelo done', () => {
    const turn = newTurn()
    applyEvent(turn, { type: 'error', message: 'falhou' })
    applyEvent(turn, { type: 'done', stopReason: 'error' })

    expect(turn.status).toBe('error')
    expect(turn.error).toBe('falhou')
  })
})
