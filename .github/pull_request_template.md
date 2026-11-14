## O que muda

<!-- Resumo em 1–3 frases. -->

## Rastreabilidade (SDD)

- Regras/critérios afetados: `RN-xx`, `CA-xx`
- [ ] `docs/spec.md` atualizado (se o comportamento mudou)
- [ ] Teste novo/alterado citando o `CA-xx`

## Checklist

- [ ] `dotnet build backend/Pedidos.sln` sem warnings
- [ ] Testes unitários e de integração passando
- [ ] `npm run lint && npm run typecheck && npm test` no frontend (se aplicável)
- [ ] Sem segredos, sem SQL concatenado, sem preço vindo do cliente
- [ ] Revisado com o subagente `code-reviewer`
