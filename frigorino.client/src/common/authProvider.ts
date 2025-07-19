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

        // Set up new subscription
        unsubscribe = onAuthStateChanged(getAuth(), (user) => {
            console.log("Auth state changed:", user);
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
