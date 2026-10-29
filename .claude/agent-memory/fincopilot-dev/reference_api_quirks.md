---
name: reference-api-quirks
description: Peculiaridades verificadas do SDK C# da Anthropic (ApiEnum), da API SGS do BCB, da brapi e do ambiente ASP.NET local
metadata:
  type: reference
---

Verificado em 2026-10-28 com chamadas reais.

**SDK C# Anthropic (13.3.0)**
- `ApiEnum<string, T>.ToString()` devolve o JSON com aspas (`"tool_use"`), não o valor. Use `.Raw()` para a string ou `.Value()` para a enum. Esse bug fazia o loop nunca reconhecer `tool_use`/`refusal`.
- XML docs ficam em `~/.nuget/packages/anthropic/13.3.0/lib/net9.0/Anthropic.xml`: grep ali é o jeito mais rápido de confirmar nomes.

**BCB SGS**
- Série 432 (meta Selic) tem datas FUTURAS: `ultimos/N` devolve dias até a próxima reunião do Copom (ex.: em 28/10 devolveu 04/11). Isso confundiu o modelo sobre "que dia é hoje". Filtrar por `dataFinal` ou descartar datas > hoje.
- Série 4389 (CDI) em `ultimos/N` veio em ordem DECRESCENTE; 432, 433 e 13522 vieram crescentes. Ordenar por data no código.
- CDI publica com ~1 dia útil de atraso; IPCA (433/13522) com ~1 mês e datas sempre no dia 01.
- Filtro por período: `/dados?formato=json&dataInicial=dd/MM/yyyy&dataFinal=dd/MM/yyyy`. Confirmado: `dataFinal` corta as datas futuras da Selic e a resposta vem crescente (até o CDI). A ferramenta já usa isso.

**brapi**
- Sem token, PETR4/VALE3/ITUB4/MGLU3 funcionam; HGLG11, BOVA11 etc. voltam 401. `regularMarketTime` vem em UTC (14:58Z = 11:58 Brasília).
- Sem token, rajadas de ~6 requisições paralelas recebem 429 de forma intermitente (visto em 2026-10-28). O BrapiClient faz retry com backoff; não remover.

**Prompt caching (doc da skill claude-api, prompt-caching.md)**
- Cache automático (top-level) combina com breakpoint explícito: o arranjo recomendado para loops é marcador explícito no fim do system + automático na cauda. TTLs mais longos precisam vir antes.

**Ambiente local**
- Edge headless (`msedge --headless=new --screenshot`) serve para capturar telas sem extensão, mas a viewport mínima é 492px (`--window-size=390` só recorta a imagem). Tema escuro: `--force-dark-mode --blink-settings=preferredColorScheme=0`.
- `npm audit` acusa vulnerabilidades no Vitest 3 (tinypool, @vitest/mocker); a correção exige Vitest 4, que ainda não foi checado no Node 20.17.
- `dotnet run --no-launch-profile` sobe em Production e NÃO carrega user-secrets (erro 401 "x-api-key header is required"). Use `dotnet run` (perfil com ASPNETCORE_ENVIRONMENT=Development).
- Com o backend rodando, `dotnet test` pode falhar por DLL travada no Windows: pare o servidor antes.
