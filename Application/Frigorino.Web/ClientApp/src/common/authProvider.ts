import { getAuth, onAuthStateChanged, type User } from "firebase/auth";
import { create } from "zustand";
import { identifyUser, resetUser } from "./observability";

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
            const u = testUser as User;
            identifyUser({ id: u.uid, email: u.email });
            set({ user: u, loading: false });
            return;
        }

        // Dev mode bypass: paired with the backend DevAuthHandler so `npm run dev` reaches
        // protected routes without a real Firebase tenant. Identity must mirror the
        // backend DevAuth principal (see Frigorino.Infrastructure/Auth/DevAuthHandler.cs).
        if (import.meta.env.VITE_DEV_AUTH === "true") {
            const devUser = {
                uid: "dev-user",
                email: "dev@frigorino.local",
                displayName: "Dev User",
                photoURL: null,
            } as unknown as User;
            identifyUser({ id: devUser.uid, email: devUser.email });
            set({ user: devUser, loading: false });
            return;
        }

        // Set up new subscription
        unsubscribe = onAuthStateChanged(getAuth(), (user) => {
            window.console.log("Auth state changed:", user);
            if (user) {
                identifyUser({ id: user.uid, email: user.email });
            } else {
                resetUser();
            }
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
