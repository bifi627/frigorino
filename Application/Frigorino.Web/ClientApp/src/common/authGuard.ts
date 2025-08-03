import { redirect } from "@tanstack/react-router";
import { useAuthStore } from "./authProvider";

export const requireAuth = ({
    location,
}: {
    location: { pathname: string };
}) => {
    const { user, loading } = useAuthStore.getState();

    // If still loading auth state, wait for it
    if (loading) {
        return new Promise<void>((resolve, reject) => {
            const unsubscribe = useAuthStore.subscribe((state) => {
                if (!state.loading) {
                    unsubscribe();
                    if (!state.user) {
                        reject(
                            redirect({
                                to: "/auth/login",
                                search: {
                                    redirect: location.pathname,
                                },
                            }),
                        );
                    } else {
                        resolve();
                    }
                }
            });
        });
    }

    // Auth has loaded, check if user exists
    if (!user) {
        throw redirect({
            to: "/auth/login",
            search: {
                redirect: location.pathname,
            },
        });
    }
};
