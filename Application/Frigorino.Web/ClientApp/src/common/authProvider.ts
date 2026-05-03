import { getAuth, onAuthStateChanged, type User } from "firebase/auth";
import { create } from "zustand";

interface AuthState {
    user: User | null | undefined;
    loading: boolean;
    initialize: () => void;
}

let unsubscribe: (() => void) | undefined;

export const useAuthStore = create<AuthState>((set) => ({
    user: undefined,
    loading: true,
    initialize: () => {
        // Clean up previous subscription if it exists
        if (unsubscribe) {
            unsubscribe();
        }

        // Playwright integration test bypass: skip Firebase if a test user is injected
        const testUser = (window as unknown as Record<string, unknown>).__PLAYWRIGHT_TEST_USER__;
        if (testUser) {
            set({ user: testUser as User, loading: false });
            return;
        }

        // Set up new subscription
        unsubscribe = onAuthStateChanged(getAuth(), (user) => {
            window.console.log("Auth state changed:", user);
            set({ user, loading: false });
        });
    },
}));

// Initialize auth state monitoring
useAuthStore.getState().initialize();

// Clean up subscription when the module is hot reloaded or the app is unmounted
if (import.meta.hot) {
    import.meta.hot.dispose(() => {
        if (unsubscribe) {
            unsubscribe();
        }
    });
}
