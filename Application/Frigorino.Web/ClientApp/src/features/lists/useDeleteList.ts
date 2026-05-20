import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    deleteListMutation,
    getListQueryKey,
    getListsQueryKey,
} from "../../lib/api/@tanstack/react-query.gen";

export const useDeleteList = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...deleteListMutation(),
        onSuccess: (_data, variables) => {
            queryClient.removeQueries({
                queryKey: getListQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            });
            queryClient.invalidateQueries({
                queryKey: getListsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
            });
        },
    });
};
