# Firebase Authentication Setup

## Current Configuration

Firebase authentication is integrated with both frontend and backend:

### Frontend Integration
- **Location**: `src/common/auth.ts` and `src/common/authProvider.ts`
- **Provider**: Google authentication enabled
- **State Management**: Zustand store for auth state
- **Route Protection**: `ProtectedRoute` component guards authenticated routes

### Backend Integration
- **JWT Validation**: ASP.NET Core JWT Bearer authentication
- **Configuration**: `Frigorino.Infrastructure.Auth.FirebaseAuth`
- **User Creation**: Automatic user creation on first login
- **Token Handling**: Bearer tokens automatically validated

## Setup Steps

### Firebase Console Setup
1. Enable Google authentication in Firebase Console
2. Configure OAuth consent screen
3. Add authorized domains:
   - `localhost` (development)
   - Your production domain

### Development URLs
- `http://localhost:5173` (Vite dev server)
- `https://localhost:5001` (ASP.NET Core API)

### Environment Configuration
- Firebase config in `src/common/auth.ts`
- Backend Firebase settings in `appsettings.json`

## User Flow
1. User clicks "Continue with Google"
2. Firebase handles Google OAuth flow
3. JWT token sent to backend APIs
4. Backend validates token and creates/updates user
5. User redirected to protected dashboard

## Authentication Features
- Google OAuth integration
- Automatic user creation
- JWT token validation
- Session persistence
- Route protection
- Logout functionality
