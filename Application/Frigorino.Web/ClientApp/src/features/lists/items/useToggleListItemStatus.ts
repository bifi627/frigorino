import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import {
    getItemsQueryKey,
    getListQueryKey,
    toggleItemStatusMutation,
} from "../../../lib/api/@tanstack/react-query.gen";
import type {
    ListItemResponse,
    ListResponse,
} from "../../../lib/api/types.gen";

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

            // The server mints the authoritative rank; optimistically we flip the status and move
            // the item to mirror the server's placement — checked → top of checked section,
            // unchecked → bottom of unchecked section. The real rank arrives on refetch.
            queryClient.setQueryData<ListItemResponse[]>(queryKey, (old) => {
                if (!old) return old;
                const moved = old.find((i) => i.id === variables.path.itemId);
                if (!moved) return old;

                const newStatus = !moved.status;
                const updated = { ...moved, status: newStatus };
                const others = old.filter((i) => i.id !== moved.id);
                if (newStatus) {
                    // Checked: prepend above the first checked item.
                    const firstChecked = others.findIndex((i) => i.status);
                    others.splice(
                        firstChecked === -1 ? others.length : firstChecked,
                        0,
                        updated,
                    );
                } else {
                    // Unchecked: append after the last unchecked item.
                    let lastUnchecked = -1;
                    others.forEach((i, idx) => {
                        if (!i.status) lastUnchecked = idx;
                    });
                    others.splice(lastUnchecked + 1, 0, updated);
                }
                return others;
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
        onSuccess: async (data, variables) => {
            // Bump the PromoteBar count the instant a perishable is checked off, so it doesn't
            // lag the debounced getList refetch in onSettled. `promote` is non-null only when
            // the server stamped the item as a pending candidate, so this is an accurate +1
            // (uncheck/decrement is left to the onSettled reconcile — predicting it client-side
            // would risk a wrong-direction flicker).
            const becamePromotable = data.status && data.promote != null;
            if (!becamePromotable) {
                return;
            }
            const listQueryKey = getListQueryKey({
                path: {
                    householdId: variables.path.householdId,
                    listId: variables.path.listId,
                },
            });
            // Cancel any in-flight list refetch so it can't clobber the optimistic count;
            // onSettled re-invalidates for the authoritative value.
            await queryClient.cancelQueries({ queryKey: listQueryKey });
            queryClient.setQueryData<ListResponse>(listQueryKey, (old) =>
                old
                    ? {
                          ...old,
                          pendingPromotionCount: old.pendingPromotionCount + 1,
                      }
                    : old,
            );
        },
        onSettled: (_data, _error, variables) => {
            // onSuccess optimistically patches the count; this is the single debounced
            // reconcile that covers both success (authoritative count) and rollback paths.
            debouncedInvalidate(
                getItemsQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
            // Reconcile the PromoteBar count (pendingPromotionCount lives on the list response).
            debouncedInvalidate(
                getListQueryKey({
                    path: {
                        householdId: variables.path.householdId,
                        listId: variables.path.listId,
                    },
                }),
            );
        },
    });
};
