# Configuration and Secrets

Status: Proposed  
Tracking: #6

## Local development
- Use `.NET User Secrets` or environment variables for provider credentials; never commit `secrets.json`.
- Commit safe `appsettings.json` defaults only; use `appsettings.Development.json` for non-sensitive overrides.
- Use a local SQL Server container only when implementation requires it.
- Model provider credentials and quotas must be independently configured and verified.

## CI
- CI jobs for untrusted pull requests must not access production credentials.
- Avoid calling paid model APIs in default PR checks.
- Keep evaluation fixtures synthetic/redacted.
- Use least-privilege `GITHUB_TOKEN` permissions.

## Runtime
- Store secrets in managed secret stores or secured environment injection.
- Restrict CV access to authorized user scope; configure retention/deletion before beta.
- Sanitize logs, redact PII, and record model usage/cost without full private prompts.
- Validate provider terms, processing agreements, and data residency before multi-user beta.

## Required configuration categories (names to be finalized during implementation)
- SQL Server connection
- CV/document storage
- Model provider endpoint/key and per-task model IDs
- Approved opportunity source API credentials
- Job schedule timezone (`Europe/Lisbon`)
- Limits: request size, concurrency, timeouts, model budgets

Never commit real values in examples.
