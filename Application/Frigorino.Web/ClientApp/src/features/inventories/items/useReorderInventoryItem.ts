import { useMutation, useQueryClient } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import type {
    InventoryItemResponse,
    ReorderItemRequest,
} from "../../../lib/api";
import { inventoryItemKeys } from "./inventoryItemKeys";

export const useReorderInventoryItem = () => {
    const queryClient = useQueryClient();
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            inventoryId,
            itemId,
            data,
        }: {
            householdId: number;
            inventoryId: number;
            itemId: number;
            data: ReorderItemRequest;
        }) =>
            ClientApi.inventoryItems.reorderInventoryItem(
                householdId,
                inventoryId,
                itemId,
                data,
            ),
        onMutate: async (variables) => {
            await queryClient.cancelQueries({
                queryKey: inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            });

            const previousItems = queryClient.getQueryData<
                InventoryItemResponse[]
            >(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            );

            // Mirror the server's midpoint math (Inventory.ReorderItem). Single
            // section — no checked/unchecked split. UNCHECKED_MIN matches
            // SortOrderCalculator.UncheckedMinRange (1_000_000).
            queryClient.setQueryData<InventoryItemResponse[]>(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
                (old) => {
                    if (!old) return old;
                    const movedItem = old.find((i) => i.id === variables.itemId);
                    if (!movedItem) return old;

                    const section = old
                        .filter((i) => i.id !== movedItem.id)
                        .sort(
                            (a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0),
                        );

                    const DEFAULT_GAP = 10_000;
                    const UNCHECKED_MIN = 1_000_000;

                    const afterItem =
                        variables.data.afterId && variables.data.afterId !== 0
                            ? section.find(
                                  (i) => i.id === variables.data.afterId,
                              )
                            : undefined;
                    const beforeItem = afterItem
                        ? section.find(
                              (i) =>
                                  (i.sortOrder ?? 0) >
                                  (afterItem.sortOrder ?? 0),
                          )
                        : undefined;

                    let newSortOrder: number;
                    if (!afterItem) {
                        newSortOrder = section.length
                            ? (section[0].sortOrder ?? 0) - DEFAULT_GAP
                            : UNCHECKED_MIN + DEFAULT_GAP;
                    } else if (!beforeItem) {
                        newSortOrder = (afterItem.sortOrder ?? 0) + DEFAULT_GAP;
                    } else {
                        newSortOrder = Math.floor(
                            (afterItem.sortOrder ?? 0) +
                                ((beforeItem.sortOrder ?? 0) -
                                    (afterItem.sortOrder ?? 0)) /
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
                    inventoryItemKeys.byInventory(
                        variables.householdId,
                        variables.inventoryId,
                    ),
                    context.previousItems,
                );
            }
        },
        onSettled: (_, __, variables) => {
            debouncedInvalidate(
                inventoryItemKeys.byInventory(
                    variables.householdId,
                    variables.inventoryId,
                ),
            );
        },
    });
};
