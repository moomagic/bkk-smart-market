# PrimeNG replaces the "no heavy UI library" rule

Supersedes the Web row of the Stack table in `CLAUDE.md`, which said "SCSS design tokens (ไม่ใช้ UI library หนัก)".

The web app grew to three areas (vendor, portal, dashboard) with every control hand-styled per component: four copies of the button styles, two of the table styles, three tag/pill variants, and the portal stylesheet close to the 4 kB component-style budget. Each copy re-implements focus, disabled and form-control behaviour slightly differently. We want one component library to own that behaviour so screens stay consistent as the portal grows, and so the remaining CSS is about layout and domain meaning only.

So:

- **PrimeNG is the primary UI component library** in styled mode, with a preset derived from Aura. The visual identity stays: the existing tokens (`--ground`, `--ink`, `--accent`, `--warn`, `--series-*`, …) remain the single source of colour and are mapped into the preset. No shadows, radius 4/6/8, IBM Plex Sans Thai / Bai Jamjuree / IBM Plex Mono, no dark mode.
- **Version is `primeng@21`** (21.1.x, the line whose peers are Angular 21) with `@primeuix/themes@2` (3.x belongs to PrimeNG 22). PrimeNG 22 requires Angular ≥ 22.1; upgrading Angular and PrimeNG together is a separate, later step, so the migration never changes the framework and the library at once.
- PrimeNG renders inside a CSS layer so app styles win without `!important`.
- Components are used directly (`p-button`, `pInputText`, …), not wrapped in generic app components. Only domain-meaningful pieces get shared components (status → tag mapping that enforces Guardrail 4, the mock-data badge for Guardrail 5).
- The quota chart moves to `p-chart` (Chart.js + annotation plugin for the quota marker), loaded only in the dashboard route. The per-space table stays as the accessible equivalent of the canvas.
- Inline confirmations (forfeit, reject, demo reset) stay inline; only their controls change. To revisit after the pilot.

What does not change: business logic, API contracts, validation, permissions, guardrail wording, and the demo script.

Rollback: the pre-migration state is tagged `pre-primeng`; the work lands as one merge of `feat/primeng-migration`, so reverting is one `git revert -m 1`.

Plan and component mapping: `docs/migration/primeng-plan.md`.
