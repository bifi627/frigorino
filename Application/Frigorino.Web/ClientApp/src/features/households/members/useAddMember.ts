import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    addMemberMutation,
    getMembersQueryKey,
    getUserHouseholdsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useAddMember = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...addMemberMutation(),
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
