import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getMembersQueryKey,
    getUserHouseholdsQueryKey,
    removeMemberMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRemoveMember = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...removeMemberMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getMembersQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getUserHouseholdsQueryKey(),
            });
        },
    });
};
