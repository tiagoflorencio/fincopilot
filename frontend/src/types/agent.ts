// Eventos emitidos pelo backend (espelham Agent/AgentEvents.cs).
export type AgentEvent =
  | { type: 'conversation'; conversationId: string }
  | { type: 'iteration'; iteration: number }
  | { type: 'thinking'; text: string }
  | { type: 'tool_call'; id: string; name: string; input: unknown }
  | { type: 'tool_result'; id: string; name: string; output: unknown; isError: boolean; durationMs: number }
  | { type: 'text'; text: string; isFinal: boolean }
  | { type: 'usage' } & Usage
  | { type: 'error'; message: string }
  | { type: 'done'; stopReason: string }

export interface Usage {
  inputTokens: number
  outputTokens: number
  cacheReadTokens: number
  cacheWriteTokens: number
  costUsd: number
  durationMs: number
  iterations: number
}

export type TraceStep =
  | { kind: 'iteration'; iteration: number }
  | { kind: 'thinking'; text: string }
  | { kind: 'note'; text: string }
  | {
      kind: 'tool'
      id: string
      name: string
      input: unknown
      status: 'running' | 'done'
      output?: unknown
      isError?: boolean
      durationMs?: number
    }

export interface Turn {
  id: string
  question: string
  answer: string
  status: 'running' | 'done' | 'error'
  error?: string
  steps: TraceStep[]
  usage?: Usage
}
