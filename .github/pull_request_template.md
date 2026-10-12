## Summary

- What changed:
- Why:

## Architecture checklist

- [ ] Public HTTP changes use focused controllers and explicit request/response contracts.
- [ ] Business logic stays outside controllers and UI components.
- [ ] New backend services and frontend screens respect the architecture line budgets.
- [ ] Frontend HTTP calls stay in `apps/web/src/api`.
- [ ] No secrets, logs, generated files, or temporary artifacts were added.
- [ ] Relevant documentation or ADRs were updated.

## Verification

- [ ] `scripts/quality.ps1`
- [ ] Critical user flow manually verified when applicable.
