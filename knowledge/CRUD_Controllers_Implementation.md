# Household CRUD Controllers Implementation

## 🎯 Controllers Overview

We've implemented a complete set of CRUD controllers for the household multi-tenancy system with proper authorization and role-based access control.

## 📁 Controllers Implemented

### **1. HouseholdController** (`/api/household`)

**Purpose**: Main CRUD operations for household management

**Endpoints**:

- `GET /api/household` - Get all households for current user
- `GET /api/household/{id}` - Get specific household details
- `POST /api/household` - Create new household
- `PUT /api/household/{id}` - Update household (Admin/Owner only)
- `DELETE /api/household/{id}` - Delete household (Owner only)

**Features**:

- ✅ **Role-based Authorization** - Different permissions for Member/Admin/Owner
- ✅ **Automatic Ownership** - Creator becomes Owner automatically
- ✅ **Soft Delete** - Households are deactivated, not permanently removed
- ✅ **Rich DTOs** - Includes member count, roles, and relationships
- ✅ **Input Validation** - Required fields and business rules enforced

### **2. MembersController** (`/api/household/{householdId}/members`)

**Purpose**: Manage household membership and member roles

**Endpoints**:

- `GET /api/household/{id}/members` - List all household members
- `POST /api/household/{id}/members` - Add member by email (Admin/Owner only)
- `PUT /api/household/{id}/members/{userId}` - Update member role (Admin/Owner only)
- `DELETE /api/household/{id}/members/{userId}` - Remove member (Admin/Owner only)
- `POST /api/household/{id}/members/leave` - Leave household (self-removal)

**Features**:

- ✅ **Email-based Invitations** - Add users by email address
- ✅ **Role Management** - Change member roles with proper permissions
- ✅ **Self-removal** - Users can leave households themselves
- ✅ **Owner Protection** - Prevents removing last owner
- ✅ **Permission Hierarchy** - Owners > Admins > Members

### **3. CurrentHouseholdController** (`/api/currenthousehold`)

**Purpose**: Manage user's active household context

**Endpoints**:

- `GET /api/currenthousehold` - Get current active household
- `POST /api/currenthousehold/{id}` - Set active household

**Features**:

- ✅ **Session-based Context** - Remembers user's active household
- ✅ **Automatic Fallback** - Selects user's first household if none set
- ✅ **Access Validation** - Ensures user has access to selected household

## 🔧 Services Implemented

### **CurrentHouseholdService**

**Purpose**: Manage current household context and permissions

**Methods**:

- `GetCurrentHouseholdIdAsync()` - Get active household with validation
- `SetCurrentHouseholdAsync(id)` - Set active household with access check
- `GetCurrentHouseholdRoleAsync()` - Get user's role in active household
- `HasHouseholdAccessAsync(id)` - Check if user can access household
- `HasMinimumRoleAsync(role)` - Check if user has required permissions
- `GetDefaultHouseholdIdAsync()` - Get user's first available household

**Features**:

- ✅ **Session Storage** - Uses HTTP sessions for context persistence
- ✅ **Automatic Validation** - Verifies access before setting context
- ✅ **Smart Fallbacks** - Handles missing or invalid contexts gracefully

## 🛡️ Security Features

### **Authorization Model**

- **Authentication Required** - All endpoints require valid JWT token
- **Role-based Permissions** - Different actions available per role
- **Ownership Protection** - Special rules for household owners
- **Access Validation** - Users can only access their households

### **Permission Matrix**

| Action             | Member | Admin             | Owner |
| ------------------ | ------ | ----------------- | ----- |
| View Household     | ✅     | ✅                | ✅    |
| View Members       | ✅     | ✅                | ✅    |
| Add Members        | ❌     | ✅                | ✅    |
| Update Household   | ❌     | ✅                | ✅    |
| Remove Members     | ❌     | ✅ (Members only) | ✅    |
| Change Roles       | ❌     | ✅ (Members only) | ✅    |
| Delete Household   | ❌     | ❌                | ✅    |
| Transfer Ownership | ❌     | ❌                | ✅    |

## 📦 DTOs and Models

### **HouseholdDto**

```csharp
{
    Id: int
    Name: string
    Description: string?
    CreatedAt: DateTime
    UpdatedAt: DateTime
    CreatedByUser: UserDto
    CurrentUserRole: HouseholdRole
    MemberCount: int
    Members: HouseholdMemberDto[]
}
```

### **HouseholdMemberDto**

```csharp
{
    User: UserDto
    Role: HouseholdRole
    JoinedAt: DateTime
}
```

### **Request Models**

- `CreateHouseholdRequest` - Name and description
- `UpdateHouseholdRequest` - Name and description
- `AddMemberRequest` - Email and optional role
- `UpdateMemberRoleRequest` - New role

## 🔄 Business Logic

### **Household Creation Flow**

1. Validate input (name required)
2. Create household record
3. Add creator as Owner automatically
4. Return complete household information

### **Member Addition Flow**

1. Check current user has Admin/Owner permissions
2. Find target user by email
3. Verify user exists and is active
4. Check if already a member (reactivate if needed)
5. Create or update membership record

### **Role Change Flow**

1. Validate current user permissions
2. Find target member
3. Apply role hierarchy rules (can't demote higher roles)
4. Prevent non-owners from creating owners
5. Update role and return updated info

### **Household Deletion Flow**

1. Verify Owner permissions
2. Soft delete household (IsActive = false)
3. Deactivate all memberships
4. Maintain data integrity

## 🚀 Ready for Frontend Integration

The controllers are now ready for frontend integration with the following features:

- **Complete REST API** - Standard HTTP methods and status codes
- **Rich Error Handling** - Detailed error messages and appropriate status codes
- **Consistent Response Format** - Standardized DTOs across all endpoints
- **Session Management** - Automatic context handling for user experience

## 🧪 Testing Ready

The controllers include:

- **Input Validation** - Bad request responses for invalid data
- **Permission Checks** - Forbidden responses for unauthorized actions
- **Not Found Handling** - Proper responses for missing resources
- **Error Recovery** - Graceful handling of edge cases

## ⏭️ Next Steps

1. **Apply Migration** - Run `dotnet ef database update` to create tables
2. **Frontend Integration** - Create React hooks and components
3. **API Testing** - Use Swagger or Postman to test endpoints
4. **Add Validation** - Client-side validation for better UX
