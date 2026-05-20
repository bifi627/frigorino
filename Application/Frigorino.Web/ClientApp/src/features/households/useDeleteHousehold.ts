import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import {
    deleteHouseholdMutation,
    getActiveHouseholdQueryKey,
    getUserHouseholdsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteHousehold = () => {
    const queryClient = useQueryClient();
    const navigate = useNavigate();

    return useMutation({
        ...deleteHouseholdMutation(),
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: getActiveHouseholdQueryKey(),
            });
            queryClient.invalidateQueries({
                queryKey: getUserHouseholdsQueryKey(),
            });
            navigate({ to: "/" });
        },
    });
};
