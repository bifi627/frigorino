import { useMutation, useQueryClient } from "@tanstack/react-query";
import { computeAppendSortOrder } from "../../../common/sortOrder";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getItemsQueryKey,
    toggleItemStatusMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type { ListItemResponse } from "../../../lib/api/types.gen";
import { usePromotableStore } from "../promote/promotableStore";

export const useToggleListItemStatus = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        ...toggleItemStatusMutation(),
        onMutate: async (variables) => {
            const queryKey = getItemsQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });

            await queryClient.cancelQueries({ queryKey });

            const previousItems =
                queryClient.getQueryData<ListItemResponse[]>(queryKey);

            // Optimistic mirror of server math; see common/sortOrder.ts.
            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const movedItem = old.find(
                    (i) => i.id === variables.path.itemId,
                );
                if (!movedItem) return old;

                const newStatus = !movedItem.status;
                const targetSection = old.filter((i) => i.status === newStatus);
                const newSortOrder = computeAppendSortOrder(
                    targetSection,
                    newStatus,
                );

                return old.map((i) =>
                    i.id === movedItem.id
                        ? { ...i, status: newStatus, sortOrder: newSortOrder }
                        : i,
                );
            });

            return { previousItems };
        },
        onError: (_data, variables, context) => {
            if (context?.previousItems) {
                queryClient.setQueryData(
                    getItemsQueryKey({
                        path: {
                            householdId: variables.path.householdId,
                            listId: variables.path.listId,
                        },
                    }),
                    context.previousItems,
                );
            }
        },
        onSuccess: (data) => {
            // Store-only side effect — NOT a query invalidate (see the onSettled note below).
            // The server attaches `promote` only when the item was checked DONE and its product
            // is a perishable; un-check / non-perishable / unclassified come back without it,
            // which retracts any pending entry for this item.
            const store = usePromotableStore.getState();
            if (data.promote) {
                store.add({
                    itemId: data.id,
                    listId: data.listId,
                    name: data.text,
                    quantity: data.quantity ?? null,
                    expiryHandling: data.promote.expiryHandling,
                    suggestedExpiry: data.promote.suggestedExpiry ?? null,
                });
            } else {
                store.remove(data.id);
            }
        },
        onSettled: (_data, _error, variables) => {
            // Deliberate: no `onSuccess` invalidate. The optimistic update is the only UI
            // signal we want — `onSettled` covers both success and rollback paths with a
            // single debounced refetch. Don't add an `onSuccess` invalidate "for consistency".
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
