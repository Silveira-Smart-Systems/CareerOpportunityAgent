# Índice de continuidade das conversas COA

Estado: ativo
Rastreio: [#65](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/65)

Este índice é um mapa de handoff. As decisões, tarefas e entregas têm como fonte
canónica as issues, pull requests (PRs), ADRs e documentos versionados ligados abaixo.
Não é uma transcrição de conversas.

## Convenções

- IDs de sessão são sequenciais, monotónicos e nunca reutilizados: `coa-###`.
  Atribuir o próximo ID apenas quando uma sessão começa; não criar ficheiros vazios
  para fases futuras.
- Este índice principal tem o nome fixo `conversation-index.md` e não recebe numeração.
- Cada registo liga apenas evidência verificável; informação não confirmada é marcada
  `a verificar`.
- `ChatGPT URL: por adicionar` significa que não foi fornecido um URL de conversa.
- Atualizar este índice ao encerrar uma fase ou após cerca de 2--3 PRs. Uma fase pode
  ter várias conversas e uma sessão pode abranger mais de um PR.
- Não registar dados pessoais, credenciais, tokens, payloads, logs extensos ou conteúdo
  privado sensível.

## Último checkpoint e próxima sessão

Último checkpoint concluído: [coa-003 — Discovery Execution](sessions/coa-003-discovery-execution.md).
Sessão em curso: nenhuma; próxima sessão sequencial a iniciar: `coa-004`.
Os Draft PRs [#73](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/73) e
[#75](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/75)
não têm merge autorizado. A [issue #74](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/74)
regista as dependências da Phase 4C.2-E. Confirmar o CI mais recente no GitHub.

## Registos

| ID | Intervalo conhecido | Estado | Referências canónicas |
| --- | --- | --- | --- |
| [coa-001](sessions/coa-001-foundation.md) | 19--21 Nov 2026 | histórico | [#45](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/45), [#46](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/46), [ADR-0013](../architecture/adr/0013-multi-source-opportunity-discovery.md) |
| [coa-002](sessions/coa-002-discovery-geography.md) | 21--22 Nov 2026 | checkpoint | [#62](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62), [#63](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/63), [PR #64](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/64), [ADR-0014](../architecture/adr/0014-discovery-geographic-semantics.md) |
| [coa-003](sessions/coa-003-discovery-execution.md) | 22 Nov 2026 | checkpoint concluído; handoff para coa-004 | [#67](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67), [#62](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62), [ADR-0013](../architecture/adr/0013-multi-source-opportunity-discovery.md), [ADR-0014](../architecture/adr/0014-discovery-geographic-semantics.md) |

## Como retomar

1. Ler este índice e o último registo de sessão.
2. Abrir as issues, PRs e ADRs ligados antes de propor trabalho.
3. Confirmar a autorização da próxima fase na issue aplicável; não inferir autorização
   de um handoff.

## Handoff para nova conversa

Copiar para uma nova conversa: "Retomar COA a partir de
`docs/project/conversation-index.md` e do checkpoint
`docs/project/sessions/coa-003-discovery-execution.md`. Consultar issues #67,
#72, #74 e Draft PRs #73 e #75; verificar estado atual e CI no GitHub.
Ao iniciar a nova sessão, atribuir o próximo ID sequencial `coa-004` e criar
o respetivo registo. Prioridade: revisão P0 de evidência confiável e autorização
fail-closed no PR #75. Não presumir aprovação de merge/deploy; GitHub é a fonte
única de verdade."
