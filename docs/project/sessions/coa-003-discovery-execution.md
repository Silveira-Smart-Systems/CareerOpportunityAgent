# coa-003 — Discovery Execution

Data de início: 22 Nov 2026
Último checkpoint: 22 Nov 2026
Estado: checkpoint concluído; handoff preparado para próxima sessão
ChatGPT URL: por adicionar

## Objetivo e evolução

Esta sessão começou com o desenho da Phase 4C.2. Após aprovações explícitas subsequentes do responsável humano, foram executadas alterações técnicas delimitadas na Phase 4C.2, sem autorização de merge automático. A decisão inicial, que autorizava apenas documentação, não é retroativamente reinterpretada como autorização de implementação.

## Referências canónicas

- [Issue #67 — Phase 4C.2](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67)
- [Issue #72 — Phase 4C.2-D scoped provenance](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/72)
- [Draft PR #73 — implementação e diagnósticos de proveniência](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/73)
- [Issue #74 — Phase 4C.2-E dependências](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/74)
- [Draft PR #75 — trusted evidence / fail-closed design](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/75)
- [Issue #47 — fora do âmbito](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/47)
- [ADR-0013](../../architecture/adr/0013-multi-source-opportunity-discovery.md)
- [ADR-0014](../../architecture/adr/0014-discovery-geographic-semantics.md)

## Checkpoint verificado

- O PR #73 adicionou validação de proveniência, observação geográfica independente do âmbito pedido, avaliação de conflito e diagnóstico `ObservedGeographyConflict`, incluindo testes PT solicitado / ES observado e âmbito unconstrained.
- CI do commit `f0a2cce44c05b5b050416beabe93e07502da388c` do PR #73: **success**, confirmado em 22 Nov 2026. Sem revisões submetidas nem threads de revisão no momento da verificação.
- A issue #74 organiza quatro dependências: evidência/autorização (P0), trace por campo (P1), identidade/deduplicação (P1), gate distribuído (P2).
- O PR #75 foi criado como Draft, a partir de `main`, com desenho P0 de evidência confiável e autorização fail-closed, commit `e2f195642236b1d56d6fd7e7747eaa83ec3764cc`. **CI a verificar**; não declarar aprovação sem consulta.
- PRs #73 e #75 permanecem sem merge na última observação.

## Limites e autorizações

A autorização humana permitiu trabalho técnico não destrutivo, documentação, commits e testes. **Não representa aprovação de revisão humana, merge ou deploy**. Não ativar Jobicy ou outros providers; sem ingestão, persistência, scheduler, produção ou implementação de autorização distribuída. Não tratar claims de providers como evidência verificada.

## Próxima sessão

Atribuir `coa-004` **apenas quando a nova sessão efetivamente começar**, criando então o ficheiro correspondente. Começar pela leitura deste checkpoint e do índice, confirmar os estados reais de #73, #74, #75 e CI no GitHub, e rever as decisões arquiteturais P0 abertas no Draft PR #75. Dividir implementação futura em PRs pequenos e manter a aprovação humana explícita para merge.

## Handoff copiável

> Retomar COA a partir de `docs/project/conversation-index.md` e `docs/project/sessions/coa-003-discovery-execution.md`. Consultar issues #67, #72, #74 e Draft PRs #73 e #75. Verificar CI/estado atual, sem assumir merge ou aprovação de revisão. A próxima sessão deverá usar o ID sequencial `coa-004` quando começar. Prioridade: revisão do desenho P0 de evidência confiável e autorização fail-closed. GitHub é a fonte única de verdade; não ativar providers nem efetuar merge/deploy sem aprovação específica.
