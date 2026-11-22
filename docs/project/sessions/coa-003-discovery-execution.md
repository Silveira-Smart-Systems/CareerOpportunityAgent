# coa-003 — Discovery Execution

Data de início: 22 Nov 2026
Estado: arquitetura Phase 4C.2 aprovada; documentação autorizada; implementação pendente de autorização separada
ChatGPT URL: por adicionar

## Objetivo
Planear a evolução provider-neutral de Planner, Orchestrator, Gate e Provenance após a Phase 4C.1, sem implementação nesta sessão.

## Referências canónicas
- [Issue #67 — Phase 4C.2: plano e tarefas](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67)
- [Issue #62 — decisão Opção B](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62)
- [Issue #63 — Phase 4C.1 concluída](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/63)
- [PR #64 — contratos geográficos](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/64)
- [PR #66 — índice de continuidade](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/66)
- [ADR-0013](../../architecture/adr/0013-multi-source-opportunity-discovery.md)
- [ADR-0014](../../architecture/adr/0014-discovery-geographic-semantics.md)
- [Issue #47 — fora do âmbito](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/47)

## Checkpoint verificado
Em 2026-11-22, a `main` estava em `b2ea35da49e55e7e0b1299aa9b0fc07970ce4b9b` (merge do PR #66). O PR #64 e o PR #66 estavam integrados. A próxima sessão sequencial é `coa-003`, sem reutilização de ID.

## Decisão do Product Owner
Aprovada a arquitetura proposta para a Phase 4C.2. Autorizado **somente o registo das issues e da documentação no GitHub**. **Implementação não autorizada**; requer checkpoint humano explícito separado. O desenho e as tarefas têm como fonte canónica a [issue #67](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67).

## Limites
- Jobicy permanece desativado; não declarar novas capabilities reais nem executar HTTP público.
- Não antecipar #47: sem ingestão, persistência, scheduler ou deduplicação global.
- Preservar comportamento legacy, sem equivalências implícitas entre publicação, geografia anunciada e elegibilidade remota.
- Sem alterações de código, pipelines, configuração, providers, UI ou matching; sem merge automático.

## Handoff
Retomar pela issue #67 e por este registo. Confirmar estado atual da main antes de qualquer proposta. Solicitar autorização explícita para a implementação da Phase 4C.2; não a inferir da aprovação arquitetural. GitHub é a fonte única de verdade.
