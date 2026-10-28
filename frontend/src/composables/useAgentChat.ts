import { computed, ref } from 'vue'
import { createSseParser } from '../lib/sse'
import { applyEvent } from '../lib/turnReducer'
import type { AgentEvent, Turn } from '../types/agent'

export function useAgentChat() {
  const turns = ref<Turn[]>([])
  const selectedTurnId = ref<string | null>(null)
  const conversationId = ref<string | null>(null)
  let controller: AbortController | null = null

  const isRunning = computed(() => turns.value.some((t) => t.status === 'running'))
  const selectedTurn = computed(() => turns.value.find((t) => t.id === selectedTurnId.value) ?? null)

  const totals = computed(() =>
    turns.value.reduce(
      (acc, t) => ({
        tokens: acc.tokens + (t.usage ? t.usage.inputTokens + t.usage.outputTokens + t.usage.cacheReadTokens + t.usage.cacheWriteTokens : 0),
        costUsd: acc.costUsd + (t.usage?.costUsd ?? 0),
      }),
      { tokens: 0, costUsd: 0 },
    ),
  )

  async function send(message: string) {
    if (isRunning.value || !message.trim()) return

    turns.value.push({ id: crypto.randomUUID(), question: message.trim(), answer: '', status: 'running', steps: [] })
    // Pega o objeto já reativo para que as mutações do reducer atualizem a tela.
    const turn = turns.value[turns.value.length - 1]
    selectedTurnId.value = turn.id
    controller = new AbortController()

    try {
      const response = await fetch('/api/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ message: turn.question, conversationId: conversationId.value }),
        signal: controller.signal,
      })

      if (!response.ok || !response.body) {
        const body = await response.json().catch(() => null)
        throw new Error(body?.error ?? `Erro HTTP ${response.status}`)
      }

      const feed = createSseParser((data) => {
        const event = JSON.parse(data) as AgentEvent
        if (event.type === 'conversation') conversationId.value = event.conversationId
        applyEvent(turn, event)
      })

      const reader = response.body.pipeThrough(new TextDecoderStream()).getReader()
      for (;;) {
        const { value, done } = await reader.read()
        if (done) break
        feed(value)
      }
    } catch (err) {
      if (controller.signal.aborted) {
        turn.status = 'error'
        turn.error = 'Interrompido.'
      } else {
        turn.status = 'error'
        turn.error = err instanceof Error ? err.message : 'Falha de conexão com o servidor.'
      }
    } finally {
      if (turn.status === 'running') turn.status = 'done'
      controller = null
    }
  }

  function stop() {
    controller?.abort()
  }

  function reset() {
    stop()
    turns.value = []
    selectedTurnId.value = null
    conversationId.value = null
  }

  function select(id: string) {
    selectedTurnId.value = id
  }

  return { turns, selectedTurn, selectedTurnId, isRunning, totals, send, stop, reset, select }
}
