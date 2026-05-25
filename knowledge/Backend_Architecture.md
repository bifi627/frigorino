# Backend Architecture

## Clean Architecture Layers

### Domain Layer (`Frigorino.Domain`)
- **Entities**: Core business objects (User, Household, List, ListItem, Inventory, InventoryItem)
- **DTOs**: Data transfer objects for API communication
- **Interfaces**: Service contracts and repository interfaces

### Application Layer (`Frigorino.Application`)
- **Services**: Business logic implementation
- **Extensions**: Mapping extensions for DTO conversions
- **Utilities**: Helper classes like SortOrderCalculator

### Infrastructure Layer (`Frigorino.Infrastructure`)
- **EntityFramework**: Database context and configurations
- **Auth**: Firebase authentication setup
- **Services**: Current user/household context services
- **Hangfire**: DI wiring (`HangfireDependencyInjection.cs`), `ILogger`→Hangfire.Console bridge
- **Jobs**: Background jobs (scoped classes with `ExecuteAsync`)

### Web Layer (`Frigorino.Web`)
- **Controllers**: REST API endpoints (being phased out — new endpoints go in `Frigorino.Features` as vertical slices)
- **Middleware**: Static-file and pre-compressed-asset middlewares. Lazy `Users` sync lives in `Frigorino.Infrastructure/Auth/UserSync.cs` and is called from `JwtBearerEvents.OnTokenValidated`, not from a Web-layer middleware.
- **Program.cs**: Application startup and configuration

## Entity Relationships

### Core Entities and Properties

#### User Entity
- **ExternalId**: Firebase UID (Primary Key)
- **Name**: User display name
- **Email**: User email address
- **CreatedAt/LastLoginAt**: Timestamp tracking
- **IsActive**: Soft delete flag
- **Navigation Properties**:
  - `UserHouseholds`: Many-to-many relationship with households
  - `CreatedHouseholds`: One-to-many for households created by user

#### Household Entity
- **Id**: Auto-incrementing integer primary key
- **Name/Description**: Household metadata
- **CreatedByUserId**: Foreign key to User.ExternalId
- **CreatedAt/UpdatedAt**: Automatic timestamp management
- **IsActive**: Soft delete flag
- **Navigation Properties**:
  - `CreatedByUser`: Owner relationship
  - `UserHouseholds`: Many-to-many with users (includes roles)
  - `Lists`: One-to-many for household lists
  - `Inventories`: One-to-many for household inventories

#### List Entity
- **Id**: Auto-incrementing primary key
- **HouseholdId**: Foreign key to Household
- **Name/Description**: List metadata
- **IsActive**: Soft delete flag
- **Navigation Properties**:
  - `Household`: Parent household
  - `Items`: One-to-many for list items with sort ordering

#### ListItem Entity
- **Id**: Auto-incrementing primary key
- **ListId**: Foreign key to List
- **Name**: Item name
- **Status**: Boolean completion status
- **SortOrder**: Integer for drag-and-drop ordering
- **CreatedAt/UpdatedAt**: Timestamp tracking
- **IsActive**: Soft delete flag

#### UserHousehold Junction Entity
- **UserId**: Foreign key to User.ExternalId
- **HouseholdId**: Foreign key to Household.Id
- **Role**: Enum (Owner/Admin/Member)
- **JoinedAt**: Timestamp when user joined household

### Database Configuration
- **Primary Keys**: Auto-incrementing integers except User (string ExternalId)
- **Foreign Keys**: Properly configured with cascade behaviors
- **Indexes**: Optimized for common query patterns
- **Soft Deletes**: IsActive flag pattern throughout
- **Timestamps**: Automatic CreatedAt/UpdatedAt management via DbContext

## Dependency Injection Patterns

### Service Registration
```csharp
// Core application services
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Background jobs (Hangfire)
builder.Services.AddHangfireServices(builder.Configuration); // Frigorino.Infrastructure/Hangfire/HangfireDependencyInjection.cs
```

### Service Interfaces
- **ICurrentUserService**: Gets current authenticated user from Firebase JWT
- **ICurrentHouseholdService**: Manages household context switching
- **IHouseholdService**: CRUD operations for households
- **IListService/IListItemService**: List management operations
- **IInventoryService/IInventoryItemService**: Inventory management

### Scoped Services Pattern
- **Per-Request Scope**: User and household context services
- **Database Context**: ApplicationDbContext scoped per request
- **Authentication**: Firebase JWT validation per request
- **Session Management**: HTTP sessions for household context persistence

## Key Features

### Multi-Tenant System
- Household-based data isolation
- Role-based permissions (Owner/Admin/Member)
- Session-based household context switching

### Authentication
- Firebase JWT Bearer authentication
- Automatic user creation on first login
- Session management for household context

### Database
- **PostgreSQL**: Primary database with Entity Framework Core
- **Automatic Migrations**: Applied on application startup
- **Soft Deletes**: IsActive flag pattern for data retention
- **Timestamp Management**: CreatedAt/UpdatedAt automatically managed by DbContext
- **Connection String**: Configured via appsettings.json/environment variables
- **Entity Configurations**: Fluent API configurations for relationships and constraints

### Background Jobs (Hangfire)

The former bespoke `IMaintenanceTask` / `MaintenanceHostedService` system has been **removed** and replaced by Hangfire (Hangfire.AspNetCore + Hangfire.PostgreSql). See `CLAUDE.md` → "Background jobs (Hangfire)" for the authoritative description.

#### Current jobs
- **`CleanupInactiveEntitiesJob`** (`Cron.Daily()`, `MisfireHandlingMode.Relaxed`) — removes soft-deleted entities and completed items older than 30 days. This is the sole recurring job today.

#### Adding a new job
1. Implement `Frigorino.Infrastructure.Jobs.<JobName>` as a scoped class with `ExecuteAsync(CancellationToken)`. Log via `ILogger<T>` only.
2. Register it as scoped in `AddHangfireServices` (`Frigorino.Infrastructure/Hangfire/HangfireDependencyInjection.cs`).
3. For recurring jobs, add `RecurringJob.AddOrUpdate<TJob>(...)` with `MisfireHandlingMode.Relaxed` in `ConfigureHangfireJobs()` (`Frigorino.Web/Program.cs`).
4. For on-demand jobs, inject `IBackgroundJobClient` and call `Enqueue<TJob>(j => j.ExecuteAsync(...))` from the producer.
