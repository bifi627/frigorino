import { useMutation } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    compactItemsMutation,
    getItemsQueryKey,
} from "../../../lib/api/@tanstack/react-query.gen";

export const useCompactListItems = () => {
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...compactItemsMutation(),
        onSuccess: (_data, variables) => {
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
