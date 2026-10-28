# FinCopilot

Agente de IA de educação financeira (projeto de portfólio). Backend em ASP.NET Core (.NET 9) com o SDK oficial da Anthropic para C#; frontend em Vue 3 + TypeScript + Vite. Visão geral, arquitetura e roadmap estão no [README.md](README.md).

## Comandos

```bash
# backend (porta 5080)
cd backend/src/FinCopilot.Api && dotnet run
cd backend && dotnet test

# frontend (porta 5173, proxy /api -> 5080)
cd frontend && npm run dev
cd frontend && npm test
cd frontend && npm run build    # inclui type-check (vue-tsc)
```

A chave da API fica em user-secrets (`Anthropic:ApiKey`) ou em `ANTHROPIC_API_KEY`, nunca no código ou no `appsettings.json`. O token opcional da brapi fica em `Brapi:Token`.

## Onde fica cada coisa

- `backend/src/FinCopilot.Api/Agent/AgentRunner.cs`: loop agêntico manual (chamada ao modelo, execução de ferramentas, eventos)
- `backend/src/FinCopilot.Api/Agent/AgentEvents.cs`: contrato dos eventos SSE, espelhado em `frontend/src/types/agent.ts`
- `backend/src/FinCopilot.Api/Tools/`: ferramentas do agente; cada uma herda `AgentTool<TInput>` e é registrada em `Program.cs`
- `frontend/src/lib/turnReducer.ts`: transforma eventos SSE no estado do turno exibido no trace
- `frontend/src/composables/useAgentChat.ts`: envio da mensagem e leitura do stream

## Regras do projeto

- **Contas ficam em ferramentas determinísticas e testadas**, nunca no LLM. Toda ferramenta nova precisa de testes em `backend/tests/FinCopilot.Tests` com `HttpMessageHandler` fake (sem rede nos testes).
- **O histórico da conversa é append-only.** Os blocos de thinking assinados voltam intactos para a API; ao falhar, descarta-se só a cauda da execução atual.
- **Falha de ferramenta vira `tool_result` com `is_error: true`** (`ToolException` para erros esperados), não exceção que derruba o turno.
- **Ao mudar um evento SSE, atualize os dois lados** (`AgentEvents.cs` e `types/agent.ts`) e o reducer.
- **O system prompt (`SystemPrompt.cs`) e a lista de ferramentas não podem ter conteúdo variável** (datas, dados do usuário), para não quebrar o prompt caching.
- Markdown vindo do modelo passa por `renderMarkdown` (DOMPurify) antes de ir para o `v-html`.
- O Node local é o 20.17: não subir para Vite 7+ nem TypeScript 6 sem atualizar o Node antes.
- Textos de UI, comentários e mensagens de erro em português do Brasil; identificadores de código em inglês.
- Commits e pushes só quando o usuário pedir.
