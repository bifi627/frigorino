import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { ListItemResponse } from "../../../lib/api";
import { listItemKeys } from "./listItemKeys";

export const useToggleListItemStatus = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
            itemId,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
        }) =>
            ClientApi.listItems.toggleItemStatus(householdId, listId, itemId),
        onMutate: async (variables) => {
            await queryClient.cancelQueries({
                queryKey: listItemKeys.byList(
                    variables.householdId,
                    variables.listId,
                ),
            });

            const previousItems = queryClient.getQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
            );

            // Only flips `status` — does NOT recompute `sortOrder`. See TECH_DEBT.md:
            // "useToggleListItemStatus optimistic update doesn't recompute sortOrder".
            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    return old.map((item) =>
                        item.id === variables.itemId
                            ? { ...item, status: !item.status }
                            : item,
                    );
                },
            );

            return { previousItems };
        },
        onError: (_, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    listItemKeys.byList(
                        variables.householdId,
                        variables.listId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSettled: (_, __, variables) => {
            // Deliberate: no `onSuccess` invalidate. The optimistic flip is the only UI signal
            // we want — `onSettled` covers both success and rollback paths with a single
            // debounced refetch to reconcile sortOrder (see TECH_DEBT.md note on missing
            // sortOrder recompute). Don't add an `onSuccess` invalidate "for consistency".
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};
