import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    createHouseholdMutation,
    getUserHouseholdsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useCreateHousehold = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...createHouseholdMutation(),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: getUserHouseholdsQueryKey(),
            });
        },
    });
};
