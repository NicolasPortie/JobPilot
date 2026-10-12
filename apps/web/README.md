# JobPilot Web

React and TypeScript interface for the manual JobPilot workspace.

## Responsibilities

- present the focused workspace for searches, opportunities, profile and resume tailoring;
- call the API through domain clients under `src/api`;
- keep reusable application defaults, models, and utilities under `src/app`;
- keep business policy in the API; no browser agent or extension is required by the web flow.

New screens and complex workflows should be created as feature modules instead
of being added directly to `App.tsx`.

## Commands

```powershell
npm ci
npm run dev
npm run test
npm run check
```

`npm run check` is the frontend quality gate used by CI.
