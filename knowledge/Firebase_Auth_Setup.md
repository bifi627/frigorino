# Firebase Authentication Setup

Firebase Auth is used as the identity provider — sign-in and JWT issuance only. No Firestore, Functions, Storage, or Messaging. Household membership and authorization live in the application database; Firebase only owns credentials.

## Frontend Integration

- **SDK init**: `src/common/auth.ts` calls `initializeApp(firebaseConfig)` with the project's public config (`apiKey`, `authDomain`, `projectId`, etc.). These values are non-secret and intentionally shipped to the browser.
- **Auth state**: `src/common/authProvider.ts` exposes a Zustand store (`useAuthStore`) that subscribes to `onAuthStateChanged` and exposes `user` + `loading`.
- **Sign-in methods** (`src/hooks/useAuth.ts`):
  - Email/password via `signInWithEmailAndPassword` and `createUserWithEmailAndPassword`.
  - Google OAuth via `signInWithPopup(GoogleAuthProvider)`.
  - Logout via `signOut`.
- **Route protection**: the `_protected.tsx` TanStack Router layout redirects unauthenticated users.
- **Token attachment**: `src/common/apiClient.ts` resolves `getAuth().currentUser?.getIdToken()` on every API call and sends it as `Authorization: Bearer <token>`.

## Backend Integration

- **JWT validation**: ASP.NET Core `JwtBearer` middleware wired in `Frigorino.Infrastructure/Auth/FirebaseAuth.cs`. Validates issuer, audience, lifetime, and signing key against Firebase's public keys (fetched via `Authority`).
- **Claims consumed**: `CurrentUserService` reads `sub` (as `ClaimTypes.NameIdentifier`), `email`, and `name`. All three are standard OIDC claims, not Firebase-specific — keeps the integration thin and the provider replaceable.
- **Lazy user creation**: `InitialConnectionMiddleware` (runs after the auth middlewares in `Program.cs`) checks the JWT's `sub` against the `Users` table and creates a row on first authenticated request. The Firebase Admin SDK singleton is registered but not used for user management.

## Setup Steps

### Firebase Console

1. Enable the sign-in methods to support: **Email/Password** and **Google**.
2. Configure the OAuth consent screen (Google sign-in only).
3. Add authorized domains: `localhost` (development) and the production domain.

### Development URLs

- Vite dev server: `https://localhost:44375` (HTTPS, dev cert managed by `dotnet dev-certs` — see `vite.config.ts`).
- ASP.NET Core API: `https://localhost:5001`.
- Vite proxies `/api`, `/openapi`, and `/scalar` to the API server.

### Configuration

- **Frontend**: project config is hardcoded in `src/common/auth.ts`. Non-secret.
- **Backend**: `FirebaseSettings:ValidIssuer`, `FirebaseSettings:ValidAudience`, and `FirebaseSettings:AccessJson` are empty placeholders in `appsettings.json`. Supply via user-secrets (dev), environment variables (Railway), or `appsettings.Development.json`. The app will not boot without these set.

## User Flow

1. SPA loads; Firebase SDK initializes; `onAuthStateChanged` fires once with `null`.
2. User picks a sign-in path:
   - Email/password (existing account or new registration) → Firebase validates / creates the credential.
   - "Continue with Google" → Firebase pops the Google OAuth flow.
3. On success, Firebase issues an ID token; the Zustand store updates; protected routes become accessible.
4. First API call carries the ID token as a Bearer header.
5. Backend validates the token, `InitialConnectionMiddleware` creates the `User` row if absent, the request proceeds.

## Playwright Test Bypass

Integration tests **do not** go through Firebase. `Frigorino.IntegrationTests/Hooks/ScenarioHooks.cs` short-circuits auth at both ends:

- Before any page JS runs, Playwright's `AddInitScriptAsync` sets `window.__PLAYWRIGHT_TEST_USER__`. `authProvider.ts` checks for this global on init and skips the Firebase `onAuthStateChanged` subscription, populating the store from the injected object directly.
- Every `/api/**` request gets `X-Test-User`, `X-Test-Email`, `X-Test-Name` headers injected via `browserContext.RouteAsync`. The test `WebApplicationFactory` registers a fake authentication handler that materializes a `ClaimsPrincipal` from these headers in place of Firebase JWT validation.

When changing auth wiring, verify both the production path and this test bypass still work.
