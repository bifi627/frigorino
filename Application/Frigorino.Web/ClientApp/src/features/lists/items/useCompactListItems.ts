import { useMutation } from "@tanstack/react-query";
import { ClientApi } from "../../../common/apiClient";
import { useDebouncedInvalidation } from "../../../hooks/useDebouncedInvalidation";
import { listItemKeys } from "./listItemKeys";

export const useCompactListItems = () => {
    const debouncedInvalidate = useDebouncedInvalidation();

    return useMutation({
        mutationFn: ({
            householdId,
            listId,
        }: {
            householdId: number;
            listId: number;
        }) => ClientApi.listItems.compactItems(householdId, listId),
        onSuccess: (_, variables) => {
            debouncedInvalidate(
                listItemKeys.byList(variables.householdId, variables.listId),
            );
        },
    });
};
