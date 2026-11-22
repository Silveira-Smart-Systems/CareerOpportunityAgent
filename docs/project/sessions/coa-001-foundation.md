# coa-001 — Fundação de Discovery

Intervalo conhecido: 19--21 Nov 2026
Estado: histórico
ChatGPT URL: por adicionar

## Objetivo

Estabelecer a visão de Discovery e a fundação provider-neutral, sem transformar
conversas em fonte de verdade.

## Referências canónicas

- [#45 — visão de produto](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/45)
- [#46 — validação e seleção da primeira fonte](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/46)
- [#47 — catálogo, persistência, deduplicação e scheduler](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/47)
- [#53 — Search Profiles MVP](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/53)
- [#60 — adapter experimental Jobicy](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/60)
- [PR #61 — adapter Jobicy](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/61)
- [#62 — semântica geográfica](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62)
- [ADR-0013 — multi-source opportunity discovery](../../architecture/adr/0013-multi-source-opportunity-discovery.md)

## Decisões e entregas verificáveis

- A visão em [#45](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/45)
  define descoberta autorizada, catálogo partilhado e matching individual; não afirma
  que esses elementos estão todos implementados.
- A seleção de fonte e as restrições de autorização/licença/quota são rastreadas em
  [#46](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/46).
- A arquitetura capability-based provider-neutral foi versionada em
  [ADR-0013](../../architecture/adr/0013-multi-source-opportunity-discovery.md).
- O adapter Jobicy foi integrado pelo [PR #61](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/61)
  e permanece experimental e desativado por omissão, conforme
  [#60](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/60).

## Riscos, limites e handoff

- [#47](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/47)
  mantém a responsabilidade por catálogo, ingestão, persistência, deduplicação e
  scheduler; não a antecipar.
- Não inferir cobertura PT/BR ou elegibilidade remota a partir de `jobGeo`.
- Prosseguir no registo [coa-002](coa-002-discovery-geography.md) para a decisão de
  geografia e o checkpoint posterior.
