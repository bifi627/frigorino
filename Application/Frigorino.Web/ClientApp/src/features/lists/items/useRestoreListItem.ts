import { useMutation } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getItemsQueryKey,
    restoreItemMutation,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useRestoreListItem = () => {
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...restoreItemMutation(),
        onSettled: (_data, _error, variables) => {
            debouncedInvalidate(
                getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
        },
    });
};
