import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getMembersQueryKey,
    updateMemberRoleMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useUpdateMemberRole = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateMemberRoleMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getMembersQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
