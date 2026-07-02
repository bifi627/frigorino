# CLAUDE.md — ClientApp (React SPA)

Frontend conventions for the React 19 + Vite + TanStack Router SPA. The root `CLAUDE.md` (rules, commands, configuration) still applies. Deeper docs: `knowledge/Frontend_Architecture.md`, `knowledge/Frontend_Styling.md`, `knowledge/API_Integration.md`.

## Architecture

- TanStack Router uses **file-based routing**; `routeTree.gen.ts` is auto-generated (by the `@tanstack/router-plugin/vite` plugin in dev; `npm run routes:check` regenerates + diffs it in CI) — do not edit by hand.
- Auth gating is per-route: each protected route's `beforeLoad` calls `requireAuth` (`src/common/authGuard.ts`), which reads the Zustand auth store and redirects to `/auth/login` when there's no Firebase user. (There is no `_protected` layout route.)
- Server state goes through TanStack Query. Every endpoint has generated hooks-ready helpers in `src/lib/api/@tanstack/react-query.gen.ts` (from the hey-api `@tanstack/react-query` plugin). One-hook-per-file under `features/<area>/` spreads the generated options into `useQuery`/`useMutation` — never write `queryFn`/`mutationFn`/`queryKey` by hand. See "API hook conventions" below. Client state uses Zustand. Do not introduce a third state layer (Redux, Context-as-store) for new features.
- Route files under `src/routes/` are thin shells: `createFileRoute` + `requireAuth` + import the page component from `features/<area>/pages/`. See `routes/household/create.tsx` for the canonical shape.
- The fetch client is configured once at app boot in `src/common/apiClient.ts` (imported for its side effect from `main.tsx`). It injects the Firebase ID token via a request **interceptor** (`client.interceptors.request.use`), not the generated `auth` resolver (ASP.NET emits no security schemes, so `auth` never fires). There is no `ClientApi` singleton — generated SDK functions import the configured `client` internally.
- First-run onboarding: `routes/index.tsx` redirects a signed-in user with no households to `/onboarding` (a household-create form with a Skip option) unless they've skipped, persisted via `features/households/onboardingSkip.ts`.
- i18n is wired via `i18next` + `i18next-http-backend` — translation files live under `public/locales/{en,de}/translation.json`. **Tests never assert on translated text** — assert on testids or `data-*` attributes.
- Push + PWA: a **push-only** service worker (`src/sw.ts`, deliberately no Workbox precaching — don't reintroduce it without an explicit offline requirement) plus `src/common/pushNotifications.ts` (FCM enable/disable/register), surfaced in `features/settings`; PWA install wiring in `src/common/pwa.ts`. Frontend RUM/observability via Grafana Faro (`src/common/observability.ts`). See `knowledge/Push_Notifications.md`.
- Env vars are `VITE_*`, read via `import.meta.env` — the Dockerfile `ARG`+`ENV` requirement is in the root `CLAUDE.md` Configuration section.

## API hook conventions

Every TanStack Query hook in `features/<area>/use*.ts` follows one exact shape (no exceptions). Mirror the canonical files rather than copying snippets here: `features/lists/useList.ts` (query hook) and `features/lists/useDeleteList.ts` (mutation hook).

- **Query hook** — takes the IDs needed to build the URL, spreads `getXOptions({ path: {...} })` into `useQuery`, guards `enabled` on each path id being `> 0`, sets a `staleTime`.
- **Mutation hook** — arg-less; the caller passes the full `{ path, body }` to `mutate`/`mutateAsync`. Spread `xMutation()`; invalidation reads `variables.path.*` in `onSuccess`/`onSettled` and builds keys via `getXQueryKey({ path: {...} })`.

**Rules:**

- Never write `queryFn`, `mutationFn`, or manual `queryKey` arrays. Spread `getXOptions` / `xMutation` / `getXQueryKey()`.
- Never reintroduce `*Keys.ts` files — auto-generated keys carry `tags` for both point and tag-based invalidation. Tag-predicate invalidation isn't used anywhere today, but the keys support it if you ever need broad cross-domain invalidation: `queryClient.invalidateQueries({ predicate: q => (q.queryKey[0] as { tags?: string[] })?.tags?.includes('Households') })`.
- Mutation hooks are arg-less; callers pass `{ path: {...}, body: ... }` to `mutate` / `mutateAsync`.
- Optimistic update hooks (toggle/reorder/create/update items) keep their `onMutate`/`onError`/`onSettled` callbacks — those are the substance the codegen doesn't replace. Read/write queryKeys via `getXQueryKey({ path: {...} })`, not literal arrays.
- The error from a mutation may be widened to `unknown` when the OpenAPI error response is `unknown` (e.g. 404 bodies). When rendering in JSX, type the local as `unknown` and use `error instanceof Error ? error.message : t("common.errorOccurred")`.
- Hey-api throws the parsed error response body on non-2xx (because the generated mutationFn passes `throwOnError: true`). `instanceof ApiError` is no longer a thing — to read field-level validation errors, narrow on the body shape: `(error as { errors?: { email?: string[] } } | null)?.errors?.email`.

## Styling

Authoritative shape: `knowledge/Frontend_Styling.md`. Trust it over the inline sx patterns still present in the legacy Lists/Inventories routes.

Key rules: the theme at `src/theme.ts` owns `shape.borderRadius`, responsive typography, and button overrides — don't reintroduce `borderRadius: 2`, manual `boxShadow`, or `fontSize: { xs, sm }` inline. Use MUI size props (`size="small"`, `fontSize="small"`) and `<Card elevation={N}>` / `<Paper variant="outlined">` instead of hand-rolling surfaces with `<Box>`. Page Containers import `pageContainerSx` from `theme.ts`.
