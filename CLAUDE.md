# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Development Commands

### Backend (.NET 8)
- `dotnet run --project Application/Frigorino.Web` - Start the ASP.NET Core server (runs on https://localhost:5001)
- `dotnet build Application/Frigorino.sln` - Build the entire solution
- `dotnet test Application/Frigorino.Test` - Run C# unit tests
- `dotnet ef migrations add [Name] --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web` - Add EF Core migration
- `dotnet ef database update --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web` - Apply migrations

### Frontend (React + Vite)
Navigate to `Application/Frigorino.Web/ClientApp/` first:
- `npm run dev` - Start development server (Vite)
- `npm run build` - Build production bundle (TypeScript + Vite)
- `npm run lint` - Run ESLint
- `npm run prettier` - Format code with Prettier  
- `npm run fix` - Run both lint --fix and prettier
- `npm run api` - Regenerate API client from swagger.json using openapi-typescript-codegen

### Database
- `npm run sql` - Start pgAdmin4 Docker container for PostgreSQL management (localhost:8080, test@test.de/test)
- Database is PostgreSQL, uses Entity Framework Core with migrations

## Architecture Overview

### Backend Architecture (.NET Clean Architecture)
- **Domain Layer** (`Frigorino.Domain`): Core business entities, DTOs, and interfaces
- **Application Layer** (`Frigorino.Application`): Business logic, services, and use cases
- **Infrastructure Layer** (`Frigorino.Infrastructure`): Data access (EF Core), Firebase auth, maintenance tasks
- **Web Layer** (`Frigorino.Web`): ASP.NET Core controllers, middleware, SPA hosting

### Key Domain Entities
- **User**: Firebase-authenticated users with ExternalId (Firebase UID)
- **Household**: Multi-tenant system - users belong to households with roles (Owner/Admin/Member)
- **List**: Shopping/task lists belonging to households with items
- **ListItem**: Individual items with drag-and-drop sorting via SortOrder
- **Inventory**: Household inventories for item management
- **InventoryItem**: Items within inventories

### Frontend Architecture (React + TypeScript)
- **Router**: TanStack Router with file-based routing (`src/routes/`)
- **State Management**: TanStack Query for server state, Zustand for client state
- **UI Framework**: Material-UI (MUI) with dark theme
- **Authentication**: Firebase Auth integration
- **API Client**: Auto-generated from OpenAPI spec using openapi-typescript-codegen

### Authentication & Authorization
- Firebase Authentication for user management
- JWT Bearer tokens for API authentication
- Household-based authorization - users must belong to households to access resources
- Role-based permissions (Owner > Admin > Member) for certain operations

### Performance Optimizations
- React Query with 5-minute staleTime and 10-minute garbage collection
- Drag-and-drop with @dnd-kit/sortable for list reordering
- Performance hooks for expensive operations
- React.memo and useMemo for component optimization

### Maintenance System
- Background hosted service (`MaintenanceHostedService`) runs scheduled tasks
- Tasks implement `IMaintainanceTask` interface 
- Examples: delete inactive items, recalculate sort orders
- Configurable intervals and dependency injection support

### Development Notes
- API client is auto-generated from Swagger - regenerate after backend changes with `npm run api`
- Entity Framework migrations are applied automatically on startup
- Soft deletes used throughout (IsActive flag)
- All entities have CreatedAt/UpdatedAt timestamps managed by DbContext
- Session-based household context switching
- Current user and household services injected via DI