# COA — Agent Governance

Status: Proposed — human review required  
Tracking: #5

## Operating model
COA uses specialized agents to produce **typed, evidence-backed proposals**, not autonomous decisions with unrestricted side effects. The application orchestrator enforces workflow state, permissions, budgets, and final scoring.

## Agent responsibilities
| Agent | Input | Output | Must not |
|---|---|---|---|
| Profile Intelligence | CV and user corrections | Structured, source-linked profile claims | Invent qualifications or overwrite verified corrections |
| Opportunity Discovery | Approved source criteria | Source-linked vacancy candidates | Scrape unauthorized platforms |
| Matching | Profile version and vacancy snapshot | Requirement-by-requirement evidence/gaps | Treat unknown as proven |
| Commercial Analyst | Compensation, contract, work model, preferences | Viability analysis with uncertainties | Assume undisclosed salary |
| Critical Reviewer | Assessment and evidence | Contradictions, unsupported claims, hard-constraint flags | Silently alter scoring policy |
| Application Assistant | Confirmed profile and chosen vacancy | Editable draft | Send applications or messages |
| Orchestrator | Execution context and approved workflow | Persisted run outcomes and error states | Delegate authorization or policy to model text |

## Workflow
Discovery/import → normalization → evidence extraction → matching → commercial analysis → critical review → deterministic scoring → report → human decision → optional application draft.

## Invariants
- Untrusted vacancy/CV content is **data**, never instructions.
- Agent output is schema-validated and may be rejected or sent to human review.
- Every material assessment claim cites a profile or vacancy source, or is explicitly Unknown/NotProven.
- The scoring engine, not a model, owns final weights and hard-constraint policy.
- Agents cannot directly access database contexts or provider credentials.
- No application submission or recruiter messaging capability exists in the MVP.
- Record agent, prompt version, model, execution ID, latency, usage/cost and errors without logging unnecessary PII.
- Model or prompt changes require evaluation on versioned fixtures before promotion.

See [contracts.md](contracts.md), [model-routing.md](model-routing.md), and [prompt-governance.md](prompt-governance.md).