import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getItemsQueryKey,
    restoreItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreListItem = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...restoreItemMutation(),
        onSuccess: (_data, variables) => {
            queryClient.invalidateQueries({
                queryKey: getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            });
        },
    });
};
