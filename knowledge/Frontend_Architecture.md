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
│   ├── lists/                      # same shape — see knowledge/Migrations/Lists.md
│   │   ├── listKeys.ts
│   │   ├── useHouseholdLists.ts / useList.ts / useCreate|Update|DeleteList.ts
│   │   ├── pages/                  # ListsPage, CreateListPage, ListViewPage, ListEditPage
│   │   └── components/             # ListSummaryCard, ListActionsMenu, *Form, *Dialog
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
│   ├── list/                       # ListItems UI (AddInput, ListContainer, ListFooter) — moves with backend ListItems migration
│   └── inventory/                  # Inventory UI — moves with backend Inventories migration
├── hooks/                          # Legacy bundled hooks (being phased out)
│   ├── useAuth.ts                  # stays — cross-cutting
│   ├── useLongPress.ts             # stays — cross-cutting
│   ├── useListItemQueries.ts       # moves with ListItems migration
│   └── useInventoryQueries.ts      # moves with Inventories migration
├── lib/api/                        # Auto-generated client (`npm run api`)
│   ├── models/                     # TypeScript types from OpenAPI
│   ├── services/                   # ClientApi.households, ClientApi.lists, …
│   └── core/                       # Base HTTP client + error handling
├── theme.ts                        # MUI theme + pageContainerSx export
└── common/                         # apiClient.ts (ClientApi singleton), authGuard
```

Canonical references for the vertical-slice shape: `features/households/CreateHouseholdPage.tsx`, `features/lists/useCreateList.ts`, `routes/household/create.tsx` (the 3-line route shell).

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
```

- **Hierarchical invalidation**: mutations call `queryClient.invalidateQueries({ queryKey: listKeys.byHousehold(id) })` to cascade refetches.
- **Background refetch**: TanStack Query handles staleness + revalidation automatically.

### Performance Optimizations

#### React Optimization Patterns
- **React.memo**: Component memoization for `SortableListItem` and other list components
- **useMemo**: Expensive operations like sorting (`uncheckedItems`, `checkedItems`)
- **useCallback**: Event handlers and callback functions to prevent re-renders
- **useRef**: DOM references and mutable values that don't trigger re-renders

#### Drag-and-Drop Optimization
- **Proper Sensors**: Configured drag sensors at component top level (not in useMemo)
- **Memoized Item Arrays**: Separate unchecked/checked item arrays with useMemo
- **Optimistic Updates**: Immediate UI updates followed by server sync
- **Touch Support**: Touch-friendly drag interactions for mobile devices

#### Bundle and Loading Optimization
- **Code Splitting**: Vite automatic code splitting for routes
- **Tree Shaking**: ES modules for optimal bundle size
- **Virtual Scrolling**: react-window for large lists
- **Lazy Loading**: Dynamic imports for route components

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

One hook file per backend slice, colocated under `features/<area>/`. Canonical shape:

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

- **Cache Management**: automatic background refetch and cache invalidation via key factories.
- **Optimistic Updates**: a few hot paths (e.g. list item toggle) update cache pre-mutation; most rely on invalidation.
- **Error Handling**: built-in retry; component-level error rendering.
- **Loading States**: `isLoading` / `isPending` flags drive UI gating.

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
