---
name: ci-failure-triage
description: Diagnose a failing COA GitHub Actions build or test run; use when a CI run fails or a user provides a failing run/PR.
---

1. Identify the exact PR, commit SHA, failed job and failed step; do not assume the latest run is the relevant one.
2. Read only failing step logs; extract first actionable error with file/line. Avoid dumping full logs into context.
3. Inspect affected source file and immediate imports/dependencies; propose smallest fix, without unrelated refactoring.
4. Apply fix to the existing PR branch when authorized. Do not open duplicate PRs for the same fix.
5. Re-run targeted validation if possible, then inspect CI result. Never claim success before checks complete.
6. Report: root cause, changed file(s), validation evidence, remaining uncertainty. Never merge.
