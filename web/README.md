# OppsView web

React 19 + TypeScript + Vite. Setup steps are in the [root README](../README.md#running-it-locally).

```powershell
npm install
npm run dev     # http://localhost:5173 (expects the API on http://localhost:5226)
npm test        # Vitest + Testing Library
npm run lint    # oxlint
npm run build   # type-check and production build
```

To point at a different API, copy `.env.example` to `.env.local` and set `VITE_API_URL`. Only `VITE_*` variables reach the browser, so never put secrets there.

## Structure

```
src/
  api/          types.ts mirrors the API DTOs. client.ts is the single fetch wrapper.
  auth/         Session storage, AuthProvider/useAuth, RequireAuth route guard
  pages/        AuthPage (login + register), ApplicationsPage
  components/   ApplicationForm, ApplicationList, StatusSelect, StatusHistory, StatusBadge
  lib/          Date helpers and form validation (pure functions, unit tested)
```

## Design notes

- **The server is the source of truth.** After every create, edit or status change, the page swaps in the application the API returned rather than guessing. The status dropdown only offers the API's `allowedNextStatuses`.
- **Validation runs twice, on purpose.** The form checks fields instantly for a good UX. The API checks again and is the authority, and its per-field errors (`ApiError.fieldErrors`) appear under the same inputs.
- **Dates.** `appliedOn` is a calendar date with no time zone, so the form uses the *local* date (`localToday()`). `new Date().toISOString()` would give the UTC date, which is yesterday for someone in South Africa just after midnight.
- **Token storage.** The JWT is kept in `localStorage` so a refresh doesn't sign you out. `src/auth/session.ts` explains the XSS trade-off and the httpOnly-cookie alternative. Expired sessions are discarded on load, the app signs out when the token expires, and any 401 also signs you out.
