---
name: feedback-git-and-readme
description: Preferências do usuário para commits (revisar lista antes) e para o visual do README (emojis e badges)
metadata:
  type: feedback
---

Antes de qualquer commit, mostrar a lista de arquivos no stage e a mensagem proposta, e só commitar depois do "sim".
**Why:** o usuário pediu isso explicitamente no commit inicial (2026-10-28); o repositório é vitrine de portfólio.
**How to apply:** `git add` + `git status --short`, apresentar, esperar confirmação. Mensagens de commit em pt-BR, estilo `feat: ...` com bullets.

O README usa emojis nos títulos/decisões técnicas e badges shields.io centralizados no topo; o usuário quer o README "bonito" visualmente.
**How to apply:** ao adicionar seções ou decisões novas no README, manter o padrão (emoji + título); atualizar badges se versões mudarem (ex.: Vite, TypeScript).

O repositório tem `.gitattributes` com `eol=lf` porque o git local usa `core.autocrlf=true` (Windows); avisos de LF/CRLF são esperados.
