# coa-002 — Discovery geography

Intervalo conhecido: 21--22 Nov 2026
Estado: checkpoint; Phase 4C.2 não autorizada
ChatGPT URL: por adicionar

## Objetivo

Separar mercado de publicação, geografia anunciada e elegibilidade remota sem alterar
silenciosamente a semântica de contratos existentes.

## Referências canónicas

- [#62 — decisão arquitetural e opções](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62)
- [#63 — Phase 4C.1](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/63)
- [PR #64 — geographic intent contracts](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/64)
- [ADR-0014 — semântica geográfica de Discovery](../../architecture/adr/0014-discovery-geographic-semantics.md)
- [ADR-0013 — arquitetura base](../../architecture/adr/0013-multi-source-opportunity-discovery.md)
- [#60 — limite do adapter Jobicy](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/60)
- [#47 — fora do âmbito](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/47)

## Decisão e entrega verificáveis

- [#62](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62)
  registou a arquitetura Opção B para tratar os três eixos geográficos separadamente.
- A Phase 4C.1 foi integrada na `main` pelo squash merge
  [`6cfbfe6ff4357bac834704f1d297d7458c84a2e7`](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/commit/6cfbfe6ff4357bac834704f1d297d7458c84a2e7),
  conforme o encerramento de [#63](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/63).
- [ADR-0014](../../architecture/adr/0014-discovery-geographic-semantics.md) e
  [PR #64](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/64)
  registam contratos provider-neutral e compatibilidade legacy.

## Estado, riscos e próximo passo

- Jobicy continua desativado. Planner, Orchestrator e Gate não foram evoluídos nesta
  fase; `jobGeo` não prova `RemoteFromCountry` nem cobertura PT/BR.
- [#47](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/47)
  permanece fora do âmbito.
- Próximo checkpoint: criar uma issue e obter autorização explícita antes de iniciar
  a Phase 4C.2. Ler primeiro [#62](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62),
  [#63](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/63)
  e ADR-0014.
