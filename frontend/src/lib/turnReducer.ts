import type { AgentEvent, Turn } from '../types/agent'

/** Aplica um evento do agente ao turno atual. Mutável de propósito: o turno é reativo no Vue. */
export function applyEvent(turn: Turn, event: AgentEvent): void {
  switch (event.type) {
    case 'iteration':
      turn.steps.push({ kind: 'iteration', iteration: event.iteration })
      break

    case 'thinking':
      turn.steps.push({ kind: 'thinking', text: event.text })
      break

    case 'text':
      // Texto entre chamadas de ferramenta é narração do processo: vai para o trace, não para a resposta.
      if (event.isFinal) turn.answer += event.text
      else turn.steps.push({ kind: 'note', text: event.text })
      break

    case 'tool_call':
      turn.steps.push({ kind: 'tool', id: event.id, name: event.name, input: event.input, status: 'running' })
      break

    case 'tool_result': {
      const step = turn.steps.find((s) => s.kind === 'tool' && s.id === event.id)
      if (step?.kind === 'tool') {
        step.status = 'done'
        step.output = event.output
        step.isError = event.isError
        step.durationMs = event.durationMs
      }
      break
    }

    case 'usage': {
      const { type: _type, ...usage } = event
      turn.usage = usage
      break
    }

    case 'error':
      turn.status = 'error'
      turn.error = event.message
      break

    case 'done':
      if (turn.status === 'running') turn.status = 'done'
      break

    case 'conversation':
      break
  }
}
