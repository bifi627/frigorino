# Firebase Google Authentication Setup Guide

## Step 1: Enable Google Authentication in Firebase Console

1. Go to the [Firebase Console](https://console.firebase.google.com/)
2. Select your project: `frigorino-2acd1`
3. Navigate to **Authentication** → **Sign-in method**
4. Click on **Google** from the list of providers
5. Toggle **Enable** to turn on Google authentication
6. Add your support email (required)
7. Click **Save**

## Step 2: Configure OAuth Consent Screen (if prompted)

1. If prompted to configure OAuth consent screen, click the link to Google Cloud Console
2. Choose **External** user type (unless you have a Google Workspace account)
3. Fill in the required information:
   - **App name**: Frigorino
   - **User support email**: Your email
   - **Developer contact information**: Your email
4. Add authorized domains:
   - `localhost` (for development)
   - Your production domain (when deployed)
5. Save and continue through the steps

## Step 3: Add Authorized Domains

In Firebase Console → Authentication → Settings → Authorized domains:

- `localhost` (for development)
- Your production domain

## Step 4: Update Environment Configuration

The current Firebase configuration in your app should work with Google authentication enabled.

## Step 5: Test the Integration

1. Start your development server
2. Navigate to the login page
3. Click "Continue with Google"
4. Sign in with a Google account
5. Verify the user is created in Firebase Authentication

## Troubleshooting

### Common Issues:

1. **"This app isn't verified" warning**:

   - This is normal during development
   - Click "Advanced" → "Go to Frigorino (unsafe)" to continue

2. **Redirect URI mismatch**:

   - Ensure `localhost:5173` (or your dev server port) is in authorized domains
   - Check OAuth settings in Google Cloud Console

3. **Authentication errors**:
   - Check browser console for detailed error messages
   - Verify Firebase project configuration

### Development URLs to Add:

- `http://localhost:5173` (Vite dev server)
- `https://localhost:5173`
- `http://localhost:3000` (if using different port)
- `https://localhost:44375` (ASP.NET Core proxy)

## Production Setup

For production deployment:

1. Add your production domain to Firebase authorized domains
2. Update OAuth settings in Google Cloud Console
3. Ensure HTTPS is enabled on your production domain
4. Update Firebase configuration if needed

## Testing Checklist

- [ ] Google authentication is enabled in Firebase Console
- [ ] OAuth consent screen is configured
- [ ] Authorized domains include localhost
- [ ] Google login button appears in the UI
- [ ] Clicking Google login opens popup/redirect
- [ ] User can successfully authenticate
- [ ] User data is stored in Firebase Authentication
- [ ] Backend receives valid JWT tokens
- [ ] User is redirected to protected routes after login
