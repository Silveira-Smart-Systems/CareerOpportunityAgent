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

Último checkpoint concluído: [coa-002 — Discovery geography](sessions/coa-002-discovery-geography.md).
Sessão em curso: [coa-003 — Discovery Execution](sessions/coa-003-discovery-execution.md).
A arquitetura Phase 4C.2 foi aprovada e a [issue #67](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67) criada;
**apenas documentação e issues foram autorizadas, não a implementação**.
[#47](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/47) continua
fora do âmbito e Jobicy permanece desativado.

## Registos

| ID | Intervalo conhecido | Estado | Referências canónicas |
| --- | --- | --- | --- |
| [coa-001](sessions/coa-001-foundation.md) | 19--21 Nov 2026 | histórico | [#45](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/45), [#46](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/46), [ADR-0013](../architecture/adr/0013-multi-source-opportunity-discovery.md) |
| [coa-002](sessions/coa-002-discovery-geography.md) | 21--22 Nov 2026 | checkpoint | [#62](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62), [#63](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/63), [PR #64](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/pull/64), [ADR-0014](../architecture/adr/0014-discovery-geographic-semantics.md) |
| [coa-003](sessions/coa-003-discovery-execution.md) | 22 Nov 2026-- | em curso; arquitetura aprovada, implementação bloqueada | [#67](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/67), [#62](https://github.com/Silveira-Smart-Systems/CareerOpportunityAgent/issues/62), [ADR-0013](../architecture/adr/0013-multi-source-opportunity-discovery.md), [ADR-0014](../architecture/adr/0014-discovery-geographic-semantics.md) |

## Como retomar

1. Ler este índice e o último registo de sessão.
2. Abrir as issues, PRs e ADRs ligados antes de propor trabalho.
3. Confirmar a autorização da próxima fase na issue aplicável; não inferir autorização
   de um handoff.

## Handoff para nova conversa

Copiar para uma nova conversa: "Começar por
`docs/project/conversation-index.md` e pelo último checkpoint `coa-002` e pela sessão em curso `coa-003`; consultar as
issues, PRs e ADRs canónicos ligados e confirmar o estado atual no GitHub antes de
propor trabalho. Não assumir acesso ao histórico integral desta conversa."
