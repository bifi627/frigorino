import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getListsQueryKey,
    updateListMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateListMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getListsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
