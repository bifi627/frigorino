import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type { ListItemResponse, ReorderItemRequest } from "../../../lib/api";
import { listItemKeys } from "./listItemKeys";

export const useReorderListItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
            itemId,
            data,
        }: {
            householdId: number;
            listId: number;
            itemId: number;
            data: ReorderItemRequest;
        }) =>
            ClientApi.listItems.reorderItem(householdId, listId, itemId, data),
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

            // Mirror the server's midpoint math (List.ReorderItem):
            //   afterId == 0  → first.sortOrder - DefaultGap (top of section)
            //   no `before`   → after.sortOrder + DefaultGap (last of section)
            //   otherwise     → midpoint between after and before
            // We don't shift sibling sortOrders — the server doesn't either.
            queryClient.setQueryData<ListItemResponse[]>(
                listItemKeys.byList(variables.householdId, variables.listId),
                (old) => {
                    if (!old) return old;
                    const movedItem = old.find((i) => i.id === variables.itemId);
                    if (!movedItem) return old;

                    const section = old
                        .filter(
                            (i) =>
                                i.status === movedItem.status &&
                                i.id !== movedItem.id,
                        )
                        .sort((a, b) => a.sortOrder - b.sortOrder);

                    const DEFAULT_GAP = 10_000;
                    const UNCHECKED_MIN = 1_000_000;
                    const CHECKED_MIN = 10_000_000;

                    const afterItem =
                        variables.data.afterId && variables.data.afterId !== 0
                            ? section.find(
                                  (i) => i.id === variables.data.afterId,
                              )
                            : undefined;
                    const beforeItem = afterItem
                        ? section.find((i) => i.sortOrder > afterItem.sortOrder)
                        : undefined;

                    let newSortOrder: number;
                    if (!afterItem) {
                        newSortOrder = section.length
                            ? section[0].sortOrder - DEFAULT_GAP
                            : (movedItem.status ? CHECKED_MIN : UNCHECKED_MIN) +
                              DEFAULT_GAP;
                    } else if (!beforeItem) {
                        newSortOrder = afterItem.sortOrder + DEFAULT_GAP;
                    } else {
                        newSortOrder = Math.floor(
                            afterItem.sortOrder +
                                (beforeItem.sortOrder - afterItem.sortOrder) /
                                    2,
                        );
                    }

                    return old.map((i) =>
                        i.id === movedItem.id
                            ? { ...i, sortOrder: newSortOrder }
                            : i,
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
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};
