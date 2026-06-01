import { useQuery } from "@tanstack/react-query";
import { getUserSettingsOptions } from "../../lib/api/@tanstack/react-query.gen";
import { useAuthStore } from "../../common/authProvider";

export const useUserSettings = () => {
    const { user } = useAuthStore();
    return useQuery({
        ...getUserSettingsOptions(),
        enabled: !!user,
        staleTime: 1000 * 60 * 5,
    });
};
