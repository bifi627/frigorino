# API Client Integration

## Current Implementation

The frontend uses an auto-generated TypeScript API client from the OpenAPI specification.

### API Client Setup

```typescript
// Located in: src/common/apiClient.ts
export const ClientApi = new FrigorinoApiClient(getClientApiConfig());
```

### Key Features

- **Auto-generated Types**: All request/response models generated from OpenAPI spec
- **Firebase Authentication**: JWT tokens automatically included in requests
- **Session Management**: Household context maintained via HTTP sessions
- **Type Safety**: Full TypeScript intellisense for all API operations

### Regenerating API Client

When backend API changes:
```bash
cd Application/Frigorino.Web/ClientApp
npm run api
```

### Main API Services Used

- **AuthService**: User authentication and profile management
- **CurrentHouseholdService**: Household context switching and current household management
- **HouseholdService**: CRUD operations for households
- **MembersService**: Household member management and role operations
- **ListsService**: List management within households
- **ListItemsService**: Individual list item operations with drag-and-drop support
- **InventoriesService**: Inventory management
- **InventoryItemsService**: Individual inventory item operations
- **ItemsService**: General item operations
- **DemoService**: Demo/development utilities
- **WeatherForecastService**: Sample weather data (demo purposes)

### Authentication Flow

#### JWT Token Management
```typescript
// Token retrieval from Firebase
const apiConfig: OpenAPIConfig = {
    ...OpenAPI,
    TOKEN: async () => (await getAuth().currentUser?.getIdToken()) ?? "",
};
```

- **Dynamic Token Retrieval**: Tokens fetched asynchronously from Firebase Auth
- **Automatic Refresh**: Firebase SDK handles token refresh automatically
- **Session Management**: Household context maintained via HTTP sessions
- **Bearer Authentication**: JWT tokens included in Authorization header

### Error Handling

#### API Error Structure
- **ApiError**: Typed error responses from OpenAPI generation
- **Status Codes**: Standard HTTP status codes with meaningful error messages
- **Error Catching**: Consistent try-catch patterns throughout application

#### Common Error Patterns
```typescript
try {
    const result = await ClientApi.listsService.createList(request);
    // Handle success
} catch (error) {
    // Handle API errors, network issues, authentication failures
    console.error('API call failed:', error);
}
```

### Configuration

#### Client Configuration
```typescript
// Located in: src/common/apiClient.ts
export const getClientApiConfig = () => {
    const apiConfig: OpenAPIConfig = {
        ...OpenAPI,
        TOKEN: async () => (await getAuth().currentUser?.getIdToken()) ?? "",
    };
    return apiConfig;
};

export const ClientApi = new FrigorinoApiClient(getClientApiConfig());
```

- **Base URL**: Configured automatically from OpenAPI spec
- **Authentication**: Dynamic Firebase token retrieval
- **Type Safety**: Full TypeScript intellisense for all operations
- **Request Interceptors**: Automatic token injection and error handling
