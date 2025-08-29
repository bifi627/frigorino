# CLAUDE.md

## Development Commands

### Backend (.NET 8)

- `dotnet run --project Application/Frigorino.Web` - Start server (https://localhost:5001)
- `dotnet build Application/Frigorino.sln` - Build solution
- `dotnet test Application/Frigorino.Test` - Run tests
- `dotnet ef migrations add [Name] --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web`
- `dotnet ef database update --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web`

### Frontend (React + Vite)

Navigate to `Application/Frigorino.Web/ClientApp/`:

- `npm run dev` - Start dev server (Vite)
- `npm run build` - Build production bundle
- `npm run lint` - ESLint
- `npm run prettier` - Format code
- `npm run fix` - Lint + prettier
- `npm run api` - Regenerate API client from swagger.json

### Database

- `npm run sql` - pgAdmin4 Docker (localhost:8080, test@test.de/test)
- PostgreSQL + Entity Framework Core with migrations

## Architecture

### Backend (.NET Clean Architecture)

- **Domain**: Entities (User, Household, List, ListItem, Inventory, InventoryItem), DTOs, interfaces
- **Application**: Business logic, services, use cases
- **Infrastructure**: EF Core data access, Firebase auth, maintenance tasks
- **Web**: Controllers, middleware, SPA hosting

### Frontend (React + TypeScript)

- **Router**: TanStack Router (file-based routing)
- **State**: TanStack Query (server state) + Zustand (client state)
- **UI**: Material-UI with dark theme
- **Auth**: Firebase Auth + JWT Bearer tokens
- **API**: Auto-generated TypeScript client from OpenAPI

### Key Features

- Multi-tenant households with roles (Owner/Admin/Member)
- Firebase authentication with automatic user creation
- Drag-and-drop list reordering
- Session-based household context switching
- Soft deletes (IsActive flag)
- Background maintenance tasks
- React Query with 5min staleTime
- PostgreSQL + EF Core migrations

## API Client

Auto-generated TypeScript client from OpenAPI spec (`src/common/apiClient.ts`):

```typescript
export const ClientApi = new FrigorinoApiClient(getClientApiConfig());
```

### Features

- Auto-generated types and services
- Firebase JWT authentication (dynamic token retrieval)
- Session-based household context
- Type-safe API operations

### Services

- AuthService, CurrentHouseholdService, HouseholdService, MembersService
- ListsService, ListItemsService, InventoriesService, InventoryItemsService
- ItemsService, DemoService, WeatherForecastService

### Token Management

```typescript
const apiConfig: OpenAPIConfig = {
  ...OpenAPI,
  TOKEN: async () => (await getAuth().currentUser?.getIdToken()) ?? "",
};
```

Regenerate after backend changes: `npm run api`

## Backend Details

### Clean Architecture

- **Domain**: Entities, DTOs, interfaces
- **Application**: Services, mapping extensions, utilities (SortOrderCalculator)
- **Infrastructure**: EF Core context, Firebase auth, maintenance tasks
- **Web**: Controllers, middleware (InitialConnectionMiddleware), startup

### Entities

**User**: ExternalId (Firebase UID/PK), Name, Email, CreatedAt/LastLoginAt, IsActive

- Relations: UserHouseholds (M:M), CreatedHouseholds (1:M)

**Household**: Id (PK), Name/Description, CreatedByUserId (FK), CreatedAt/UpdatedAt, IsActive

- Relations: CreatedByUser, UserHouseholds (M:M), Lists (1:M), Inventories (1:M)

**List**: Id (PK), HouseholdId (FK), Name/Description, IsActive

- Relations: Household, Items (1:M)

**ListItem**: Id (PK), ListId (FK), Name, Status, SortOrder, CreatedAt/UpdatedAt, IsActive

**UserHousehold**: UserId (FK), HouseholdId (FK), Role (Owner/Admin/Member), JoinedAt

**Database**: PostgreSQL, auto-increment PKs (except User), soft deletes (IsActive), automatic timestamps

### Services

- ICurrentUserService, ICurrentHouseholdService
- IHouseholdService, IListService, IListItemService
- IInventoryService, IInventoryItemService
- Scoped per-request, ApplicationDbContext, session management

### Maintenance System

- **IMaintenanceTask**: Background service interface
- **MaintenanceHostedService**: Runs tasks on startup (5s delay)
- **Tasks**: DeleteInactiveItems (30+ days), RecalculateSortOrderTask, DemoMaintenanceTask
- Each task runs in own DI scope with error isolation

```csharp
public interface IMaintenanceTask
{
    Task Run(CancellationToken cancellationToken = default);
}
```

## Frontend Details

### Tech Stack

React 19.1.0 + TypeScript 5.8.3, Vite 7.0.4, TanStack Router 1.128.8, TanStack Query 5.83.0 + Zustand 5.0.6, Material-UI 7.2.0, Firebase 12.0.0, @dnd-kit/sortable 10.0.0, react-window 1.8.11

### Structure

```
src/
├── components/ (auth, common, dashboard, household, layout, list, inventory)
├── hooks/ (useAuth, useHouseholdQueries)
├── lib/api/ (models, services, core)
├── routes/ (__root.tsx, index.tsx, auth/, household/, lists/, inventories/)
└── common/ (auth setup, API config)
```

### Query Configuration

```typescript
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, // 5 minutes
      gcTime: 10 * 60 * 1000, // 10 minutes
    },
  },
});
```

**Stale Times**: Household (5min), List/Inventory (2min), Collaborative items (30s)
**Keys**: Hierarchical (`['households']`, `['lists', householdId]`, `['list-items', listId]`)

### Performance

- **React**: React.memo, useMemo (sorting), useCallback, useRef
- **Drag-Drop**: Proper sensors, memoized arrays, optimistic updates, touch support
- **Bundle**: Vite code splitting, tree shaking, virtual scrolling, lazy loading

### UI/UX

- **Theme**: Material-UI dark theme, responsive breakpoints, 8px grid spacing
- **Mobile-First**: The app is only designed for mobile, do not put effort for making it nice for desktop
- **States**: Loading skeletons, error messages, empty state illustrations

### State Management

**TanStack Query**: Server state, cache management, optimistic updates, auto retry/error handling
**Zustand**: Local UI state (modals, forms, selections), user preferences, session data
**Firebase Auth**: User auth, token management, auto refresh, route protection
**Forms**: Local useState, client validation, API integration

```typescript
export const useListItems = (listId: string) => {
  return useQuery({
    queryKey: ["list-items", listId],
    queryFn: () => ClientApi.listItemsService.getListItems(listId),
    staleTime: 1000 * 30, // 30 seconds
  });
};
```

### Components

**Patterns**: Container/Presentation, Compound Components, Render Props, Custom Hooks
**Types**: Layout (nav, headers), Feature (lists, inventories), Common (buttons, inputs), Auth

### Build & Testing

**Vite**: React plugin, Fast Refresh, TypeScript strict mode, absolute imports, HMR
**Testing**: Jest + React Testing Library, MSW (API mocking), Cypress/Playwright (E2E)

### Routes

```
/ - Dashboard
/auth/login - Auth
/household/create - Create household
/household/manage - Manage household
/lists/ - List management
/lists/$listId/view - Individual list
/inventories/ - Inventory management
/inventories/create - Create inventory
```

### IMPORTANT WORKFLOW

**Always run these commands in this order after you are finished to ensure the code standards are met**

## Frontend

1. npm run fix
2. npm run lint
3. npm run build
