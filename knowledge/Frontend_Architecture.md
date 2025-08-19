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

```
src/
├── components/          # Reusable UI components
│   ├── auth/           # Authentication components
│   ├── common/         # Shared components (HeroImage, sortable)
│   ├── dashboard/      # Dashboard-specific components
│   ├── household/      # Household management components
│   ├── layout/         # Navigation and layout components
│   ├── list/           # List and list item components
│   └── inventory/      # Inventory management components
├── hooks/              # Custom React hooks (useAuth, useHouseholdQueries)
├── lib/api/            # Auto-generated API client (OpenAPI codegen)
│   ├── models/         # TypeScript type definitions
│   ├── services/       # API service classes
│   └── core/           # Base HTTP client and error handling
├── routes/             # File-based routing (TanStack Router)
│   ├── __root.tsx     # Root layout with auth and navigation
│   ├── index.tsx      # Dashboard route
│   ├── auth/          # Authentication routes
│   ├── household/     # Household management routes
│   ├── lists/         # List management routes
│   └── inventories/   # Inventory management routes
└── common/             # Auth setup and API configuration
```

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
- **Hierarchical Keys**: `['households']`, `['lists', householdId]`, `['list-items', listId]`
- **Invalidation Strategy**: Hierarchical invalidation for related data updates
- **Background Refetch**: Automatic background updates when data becomes stale

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
```typescript
const darkTheme = createTheme({
    palette: {
        mode: "dark",
    },
});
```
- **Dark Theme**: Consistent dark mode throughout application
- **Material Design**: Material-UI component library for consistent UX
- **Typography**: Consistent font scales and weights
- **Color Palette**: Semantic color system for status and actions

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
```typescript
// Query hook pattern
export const useListItems = (listId: string) => {
    return useQuery({
        queryKey: ['list-items', listId],
        queryFn: () => ClientApi.listItemsService.getListItems(listId),
        staleTime: 1000 * 30, // 30 seconds for collaborative updates
    });
};
```
- **Cache Management**: Automatic background refetch and cache invalidation
- **Optimistic Updates**: Immediate UI updates with server sync
- **Error Handling**: Built-in retry and error boundary patterns
- **Loading States**: Automatic loading and error state management

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

#### Testing Strategy
- **Unit Tests**: Component testing with React Testing Library
- **Integration Tests**: Multi-component interaction testing
- **API Testing**: Mock API responses for frontend testing
- **E2E Testing**: Full user workflow testing

#### Testing Tools
- **Jest**: Test runner and assertion library
- **React Testing Library**: Component testing utilities
- **MSW**: API mocking for testing
- **Cypress/Playwright**: End-to-end testing framework

### Route Structure
```
/ - Dashboard (WelcomePage)
/auth/login - Authentication
/household/create - Create household
/household/manage - Household management
/lists/ - List management
/lists/$listId/view - Individual list view
/inventories/ - Inventory management
/inventories/create - Create inventory
```
