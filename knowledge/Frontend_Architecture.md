# Frontend Architecture

## Tech Stack

- **Framework**: React 19.1.0 + TypeScript 5.8.3
- **Build Tool**: Vite 7.0.4
- **Router**: TanStack Router 1.128.8 (file-based routing)
- **State Management**: TanStack Query 5.83.0 + Zustand 5.0.6
- **UI Library**: Material-UI 7.2.0 (dark theme)
- **Authentication**: Firebase 12.0.0
- **Drag & Drop**: @dnd-kit/sortable 10.0.0
- **Virtualization**: react-window 1.8.11
- **Icons**: @mui/icons-material 7.2.0

## Project Structure

The SPA is mid-migration from a top-level `components/`+`hooks/` shape to a vertical-slice `features/<area>/` shape that mirrors the backend slice folders. **New work belongs in `features/`**; the legacy locations are being phased out alongside the corresponding backend slice migration.

```
src/
├── features/                       # Vertical slices (canonical going forward)
│   ├── households/                 # one-hook-per-file + pages/ + components/
│   │   ├── householdKeys.ts        # query-key factory
│   │   ├── householdRole.ts        # role tokens, color/label maps
│   │   ├── useUserHouseholds.ts    # one file per backend slice
│   │   ├── useCreateHousehold.ts
│   │   ├── useDeleteHousehold.ts
│   │   ├── pages/                  # CreateHouseholdPage, ManageHouseholdPage
│   │   ├── components/             # HouseholdSummaryCard, DeleteHouseholdDialog, …
│   │   └── members/                # nested sub-slice mirrors backend Members/
│   │       ├── useHouseholdMembers.ts / useAddMember.ts / …
│   │       └── components/
│   ├── lists/                      # same shape — see knowledge/Migrations/Lists.md
│   │   ├── listKeys.ts
│   │   ├── useHouseholdLists.ts / useList.ts / useCreate|Update|DeleteList.ts
│   │   ├── pages/                  # ListsPage, CreateListPage, ListViewPage, ListEditPage
│   │   ├── components/             # ListSummaryCard, ListActionsMenu, *Form, *Dialog
│   │   └── items/                  # nested sub-slice mirrors backend Lists/Items/
│   │       ├── listItemKeys.ts
│   │       ├── useListItems.ts / useCreateListItem.ts / useReorderListItem.ts / …
│   │       └── components/         # ListContainer, ListFooter, ListItemContent
│   └── me/activeHousehold/         # cross-aggregate session state
├── routes/                         # File-based routing (TanStack Router)
│   ├── __root.tsx                  # Root layout with auth + navigation
│   ├── _protected.tsx              # Auth gate for authenticated routes
│   ├── index.tsx                   # Dashboard
│   ├── auth/                       # Authentication routes
│   ├── household/                  # 7-line shells importing features/households/pages
│   ├── lists/                      # same — shells importing features/lists/pages
│   └── inventories/                # legacy fat routes (pending migration)
├── components/                     # Legacy + cross-cutting
│   ├── auth/                       # Authentication wrappers
│   ├── common/                     # HeroImage, etc.
│   ├── dashboard/                  # WelcomePage
│   ├── dialogs/                    # Shared ConfirmDialog primitive
│   ├── layout/                     # Navigation, headers
│   ├── shared/                     # PageHeadActionBar, etc.
│   ├── sortables/                  # Generic dnd-kit wrappers (SortableList, SortableListItem)
│   ├── list/                       # Shared input primitives (AddInput, QuantityPanel, DateInputPanel) — consumed by lists/items AND inventory; rename to components/inputs/ with Inventories migration
│   └── inventory/                  # Inventory UI — moves with backend Inventories migration
├── hooks/                          # Legacy bundled hooks (being phased out)
│   ├── useAuth.ts                  # stays — cross-cutting
│   ├── useLongPress.ts             # stays — cross-cutting
│   ├── useDebouncedInvalidation.ts # stays — cross-cutting TanStack Query helper
│   └── useInventoryQueries.ts      # moves with Inventories migration
├── lib/api/                        # Auto-generated client (`npm run api`)
│   ├── models/                     # TypeScript types from OpenAPI
│   ├── services/                   # ClientApi.households, ClientApi.lists, …
│   └── core/                       # Base HTTP client + error handling
├── theme.ts                        # MUI theme + pageContainerSx export
└── common/                         # apiClient.ts (ClientApi singleton), authGuard
```

Canonical references for the vertical-slice shape: `features/households/pages/CreateHouseholdPage.tsx`, `features/lists/useCreateList.ts`, `features/lists/items/useCreateListItem.ts` (optimistic-update template), `routes/household/create.tsx` (the 7-line route shell).

## Feature-folder pattern (canonical)

The frontend mirrors the backend slice shape one-to-one. Each backend slice gets one frontend hook file; each backend feature folder maps to a `src/features/<area>/` folder; nested backend resources (`Households/Members/`, `Lists/Items/`) map to nested sub-folders (`features/households/members/`, `features/lists/items/`).

Rules for new work:

1. **One hook file per backend slice.** Name follows the verb: `useCreateList.ts` for `POST /lists`, `useListItems.ts` for `GET /lists/{id}/items`, etc. No bundled `useXxxQueries.ts` hook files — those are legacy.
2. **Colocate the query-key factory** at the feature root as `<area>Keys.ts`. One `as const` typed object with named factories — see the patterns under "Query Key Patterns" below.
3. **Pages live in `pages/`, components in `components/`.** Routes (`src/routes/`) are 7-line shells that import the page component and apply `requireAuth`. Don't put page-level state, fetching, or layout logic in route files.
4. **Type imports go through `lib/api` directly.** `import type { ListItemResponse } from "../../../lib/api"` — not via a hook re-export. The codegen owns the type names; re-exporting creates a second source-of-truth.
5. **Nest sub-slices when the backend nests.** `features/lists/items/` exists because the backend has `Lists/Items/` slices and the URL nests as `/lists/{id}/items`. Reach for a sub-folder when (a) the URL is parent-scoped, (b) the lifecycle is parent-bound, AND (c) the sub-area has its own hook surface. Don't nest just to group files.
6. **Shared primitives stay in `src/components/` until they have one owner.** When a component (e.g. `AddInput`) is consumed by two unrelated features (lists/items AND inventory), it belongs in a shared location — and the rename/move is bundled with the migration that frees the second consumer. Don't move a shared file into one feature's folder.
7. **Style cleanup is bundled with the move.** When pulling a component into `features/`, apply the anti-pattern checklist from `knowledge/Frontend_Styling.md` in the same change (drop inline `borderRadius`, hand-rolled `boxShadow`, redundant `<Box>` surfaces). Don't move first, clean up later.

### Nested sub-slice precedents

- `features/households/members/` — see `knowledge/Migrations/Members.md`. Hooks at the sub-folder root, components below. Query keys live on the parent (`householdKeys.members(householdId)`) because Members is parent-bound.
- `features/lists/items/` — see `knowledge/Migrations/ListItems.md`. Same shape. Query keys live in the sub-folder (`listItemKeys.byList(householdId, listId)`) because list-items have their own cache lifecycle independent of the parent list.

The two precedents differ in *where the keys live* — both choices are valid; follow the existing feature's choice when extending it, otherwise default to colocating keys in the sub-folder.

## Key Features

### TanStack Query Configuration

#### Global Configuration
```typescript
const queryClient = new QueryClient({
    defaultOptions: {
        queries: {
            staleTime: 5 * 60 * 1000, // 5 minutes
            gcTime: 10 * 60 * 1000,   // 10 minutes (garbage collection)
        },
    },
});
```

#### Specific Query Configurations
- **Household Data**: 5-minute stale time for household and member queries
- **List Data**: 2-minute stale time for lists and list items
- **Inventory Data**: 2-minute stale time for inventories and items
- **Real-time Updates**: 30-second stale time for collaborative list items

#### Query Key Patterns

Each migrated feature owns a key factory colocated with its hooks. Examples:

```typescript
// features/households/householdKeys.ts
export const householdKeys = {
    all: ["households"] as const,
    lists: () => [...householdKeys.all, "list"] as const,
    current: () => ["currentHousehold"] as const,
    members: (householdId: number) =>
        [...householdKeys.all, "members", householdId] as const,
};

// features/lists/listKeys.ts
export const listKeys = {
    all: ["lists"] as const,
    byHousehold: (householdId: number) =>
        [...listKeys.all, "household", householdId] as const,
    detail: (listId: number) => [...listKeys.all, "detail", listId] as const,
};

// features/lists/items/listItemKeys.ts — nested sub-slice
export const listItemKeys = {
    all: ["listItems"] as const,
    byList: (householdId: number, listId: number) =>
        [...listItemKeys.all, "household", householdId, "list", listId] as const,
    detail: (itemId: number) => [...listItemKeys.all, "detail", itemId] as const,
};
```

- **Hierarchical invalidation**: mutations call `queryClient.invalidateQueries({ queryKey: listKeys.byHousehold(id) })` to cascade refetches.
- **Background refetch**: TanStack Query handles staleness + revalidation automatically.
- **Each factory owns its own `all` root.** `listKeys.all = ["lists"]` and `listItemKeys.all = ["listItems"]` — distinct roots prevent cross-feature cache collisions even when sibling keys share suffix shapes (e.g. both end in `["...", "detail", id]`).

#### Optimistic-update template

Mutations that need optimistic UI (create, update, delete, toggle, reorder) follow this shape — canonical reference is `features/lists/items/useCreateListItem.ts`:

```typescript
return useMutation({
    mutationFn: (...) => ClientApi.x.mutate(...),
    onMutate: async (variables) => {
        await queryClient.cancelQueries({ queryKey: keys.byList(...) });
        const previousItems = queryClient.getQueryData<T[]>(keys.byList(...));
        queryClient.setQueryData<T[]>(keys.byList(...), (old) => /* optimistic patch */);
        return { previousItems };
    },
    onError: (_, variables, context) => {
        if (context?.previousItems) {
            queryClient.setQueryData(keys.byList(...), context.previousItems);
        }
    },
    onSuccess: (_, variables) => {
        debouncedInvalidate(keys.byList(...));
    },
});
```

- **Debounced invalidation** lives in `src/hooks/useDebouncedInvalidation.ts` — use it to coalesce server reconciliation when a user fires several mutations back-to-back (e.g. checking off multiple items).
- **Optimistic patches must mirror the server's invariants.** If the backend's aggregate method computes a derived value (sort order, status transition), the optimistic patch should compute the same value with the same formula. Where mirroring is hard or low-value, document the gap in `TECH_DEBT.md` rather than letting the UI drift silently from server truth.

### Performance Optimizations

#### When to memoize (and when not to)

Memoization is **load-bearing only when a memoized child consumes the value**. `memo()` short-circuits a re-render only when the parent re-renders for reasons orthogonal to the props this component reads. `useMemo` / `useCallback` produce stable references that only help if the downstream consumer does `===` comparison (i.e. it's wrapped in `memo`).

Default: **don't memoize**. Reach for it when:

- A parent re-renders frequently for reasons unrelated to this component's props (e.g. unrelated sibling state churn). The parent of `features/lists/items/components/ListContainer.tsx` doesn't qualify — `ListViewPage`'s state changes (`editingItem`, `showDragHandles`) are all props this component reads, so `memo()` was theater and got removed.
- A `memo()`-wrapped child reads the value as a prop. `AddInput` in `src/components/list/AddInput.tsx` is wrapped in `memo()`, so `ListFooter`'s `useMemo`s for `rightControls` / `topPanels` / `mappedExistingItems` are load-bearing and stay.

Rule of thumb when adding/removing memoization: grep the downstream consumer for `memo(`. No memo → no benefit → don't wrap.

#### Drag-and-Drop performance
- **Sensors at component top level**: configured directly (not in `useMemo`).
- **Per-section sorted arrays**: `uncheckedItems` / `checkedItems` derived with `useMemo` inside `SortableList` because they feed dnd-kit's `<SortableContext>` which compares item-id arrays by reference.
- **Optimistic updates** with debounced invalidation: see "Optimistic-update template" above.
- **Touch support**: dnd-kit's `PointerSensor` with `activationConstraint: { distance: 8 }` so taps don't trigger drags.

#### Bundle and loading
- **Code splitting**: Vite handles per-route splitting automatically.
- **Tree shaking**: ES modules — import named exports from `@mui/material`, not the barrel default.
- **Virtual scrolling**: `react-window` is available; not currently used (lists are short enough today).
- **Lazy loading**: dynamic imports per route.

### UI/UX Design System

#### Material-UI Theme Configuration

The theme lives at `src/theme.ts` and is the source of truth — see `knowledge/Frontend_Styling.md` for the rules that apply when authoring components (no inline `borderRadius`, `boxShadow`, or `fontSize` overrides; use `elevation`, `variant`, `size` props instead).

```typescript
export const appTheme = responsiveFontSizes(
    createTheme({
        palette: { mode: "dark" },
        shape: { borderRadius: 8 },
        components: {
            MuiButton: { styleOverrides: { root: { textTransform: "none" } } },
        },
    }),
);

export const pageContainerSx = {
    py: { xs: 2, sm: 3 },
    px: { xs: 1, sm: 2 },
};
```

- **Dark Theme**: single-mode dark UI.
- **Responsive Typography**: `responsiveFontSizes` scales variants across breakpoints — don't override `fontSize` inline.
- **Global radius**: `shape.borderRadius: 8` applies to Buttons, Cards, Papers, Alerts, OutlinedInputs, Menus, Chips. Inline `borderRadius: 2` is an anti-pattern.
- **Button overrides**: `textTransform: "none"` is global — don't re-declare per Button.

#### Responsive Design
- **Mobile-First**: Progressive enhancement from mobile to desktop
- **Breakpoint System**: Material-UI responsive breakpoints (xs, sm, md, lg, xl)
- **Touch Interactions**: Touch-friendly drag-and-drop with proper hit targets
- **Accessibility**: ARIA labels, keyboard navigation, screen reader support

#### Component Design Patterns
- **Consistent Spacing**: Material-UI spacing system (8px grid)
- **Loading States**: Skeleton loaders and progress indicators
- **Error States**: User-friendly error messages and retry actions
- **Empty States**: Helpful empty state illustrations and calls-to-action

### State Management Architecture

#### Server State (TanStack Query)

One hook file per backend slice, colocated under `features/<area>/`. Two shapes — pick by the hook's job:

**Simple mutation** (no UI work between optimistic and server response):

```typescript
// features/lists/useDeleteList.ts
export const useDeleteList = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: ({ householdId, listId }) =>
            ClientApi.lists.deleteList(householdId, listId),
        onSuccess: (_, variables) => {
            queryClient.removeQueries({
                queryKey: listKeys.detail(variables.listId),
            });
            queryClient.invalidateQueries({
                queryKey: listKeys.byHousehold(variables.householdId),
            });
        },
    });
};
```

**Optimistic mutation** (frequent user-driven mutation on a hot path — add/toggle/reorder list items):

See the "Optimistic-update template" under TanStack Query Configuration above. Canonical reference: `features/lists/items/useCreateListItem.ts`. Always pair `onMutate` with the matching `onError` rollback.

**Query** (cached read):

```typescript
// features/lists/useHouseholdLists.ts
export const useHouseholdLists = (householdId: number, enabled = true) => {
    return useQuery({
        queryKey: listKeys.byHousehold(householdId),
        queryFn: () => ClientApi.lists.getLists(householdId),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 2,
    });
};
```

- **`staleTime` cheatsheet**: 5 min for slow-moving data (households), 2 min for normal feature reads (lists, list detail, inventories), 30 s for hot collaborative reads (the list-items collection inside an open list).
- **Cache management**: automatic background refetch driven by key factories.
- **Error handling**: built-in retry; component-level error rendering.
- **Loading states**: `isLoading` / `isPending` flags drive UI gating.

#### Client State (Zustand)
- **Local UI State**: Modal open/close, form states, temporary selections
- **User Preferences**: Theme settings, view preferences
- **Session Data**: Current household context, navigation state

#### Authentication State
- **Firebase Auth**: User authentication and token management
- **Session Persistence**: HTTP sessions for household context
- **Automatic Refresh**: Firebase SDK handles token refresh
- **Route Protection**: Authentication guards for protected routes

#### Form State Management
- **Local React State**: Simple forms with useState
- **Validation**: Client-side validation with immediate feedback
- **Submission Handling**: API integration with loading/error states

### Component Architecture

#### Component Composition Patterns
- **Container/Presentation**: Separation of data fetching and UI rendering
- **Compound Components**: Complex components with multiple sub-components
- **Render Props**: Flexible component composition for shared logic
- **Custom Hooks**: Reusable stateful logic extraction

#### Component Categories
- **Layout Components**: Navigation, headers, page layouts
- **Feature Components**: Business logic components (lists, inventories)
- **Common Components**: Reusable UI elements (buttons, inputs, modals)
- **Auth Components**: Authentication and authorization wrappers

### Build Configuration

#### Vite Configuration
- **Plugin Setup**: React plugin with Fast Refresh
- **TypeScript**: Strict type checking with incremental compilation
- **Path Resolution**: Absolute imports from src directory
- **Development Server**: Hot module replacement and error overlay

#### TypeScript Configuration
- **Strict Mode**: Full type safety with strict compiler options
- **Module Resolution**: ES2022 modules with bundler resolution
- **Type Generation**: Auto-generated API types from OpenAPI spec
- **JSX**: React 19 JSX transform configuration

### Testing Architecture

The SPA has **no frontend-only test runner** today — no Jest, no React Testing Library, no MSW. Component-level unit tests were considered but never wired in. Earlier versions of this doc described an aspirational stack; that's been removed to avoid confusion.

UI behavior is covered end-to-end from the backend side: Reqnroll/Playwright scenarios under `Application/Frigorino.IntegrationTests/Slices/<Area>/<Feature>.feature` drive a real Chromium against the Vite dev server + a Postgres testcontainer.

- **Assertions on `data-testid` and `data-*` attributes only** — never translated UI text (i18n keys move). See `Frontend_Styling.md` and `feedback_test_assertions_no_translated_text` in user memory.
- **Adding a feature** → add a `*.feature` scenario + steps in the matching `Slices/` folder. There's no expectation of unit-testing React components separately.
- TypeScript + ESLint + a successful `vite build` are the static guardrails; the Playwright suite is the runtime guardrail.

### Route Structure
```
/                              Dashboard (WelcomePage)
/auth/login                    Firebase authentication
/household/create              Create household
/household/manage              Household management (members, delete)
/lists/                        Lists overview
/lists/create                  Create list
/lists/$listId/view            View list items
/lists/$listId/edit            Edit list metadata
/inventories/                  Inventory management
/inventories/create            Create inventory
/inventories/$inventoryId/view Inventory items
/inventories/$inventoryId/edit Edit inventory metadata
```

Route files under `src/routes/` are thin shells (`createFileRoute` + `requireAuth` + page import from `features/<area>/pages/`). `routeTree.gen.ts` is autogenerated by `@tanstack/router-plugin/vite` — do not edit by hand.
