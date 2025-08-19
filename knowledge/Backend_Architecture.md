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
- **Tasks**: Background maintenance tasks

### Web Layer (`Frigorino.Web`)
- **Controllers**: REST API endpoints
- **Middleware**: Custom middleware (InitialConnectionMiddleware)
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
builder.Services.AddScoped<InitialConnectionMiddleware>();

// Maintenance system
builder.Services.AddMaintenanceServices(); // Extension method
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

### Maintenance System

#### Architecture
- **IMaintenanceTask Interface**: Contract for all maintenance operations
- **MaintenanceHostedService**: Background service that runs all maintenance tasks on startup
- **MaintenanceDependencyInjection**: Service registration for maintenance system

#### Task Implementation
```csharp
public interface IMaintenanceTask
{
    Task Run(CancellationToken cancellationToken = default);
}
```

#### Current Maintenance Tasks
- **DeleteInactiveItems**: Removes soft-deleted entities and completed items older than 30 days
- **RecalculateSortOrderTask**: Recalculates sort orders for list items
- **DemoMaintenanceTask**: Demo/development maintenance operations

#### Task Execution Flow
1. **Startup Delay**: 5-second delay after application start
2. **Service Scope**: Each task runs in its own dependency injection scope
3. **Error Isolation**: Individual task failures don't crash the application
4. **Logging**: Comprehensive logging for maintenance operations

#### Adding New Tasks
```csharp
// 1. Implement IMaintenanceTask
public class CustomMaintenanceTask : IMaintenanceTask
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}

// 2. Register in MaintenanceDependencyInjection
services.AddScoped<IMaintenanceTask, CustomMaintenanceTask>();
```
