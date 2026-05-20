import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getActiveHouseholdQueryKey,
    getUserHouseholdsQueryKey,
    setActiveHouseholdMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useSetCurrentHousehold = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...setActiveHouseholdMutation(),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: getActiveHouseholdQueryKey(),
            });
            queryClient.invalidateQueries({
                queryKey: getUserHouseholdsQueryKey(),
            });
        },
    });
};
