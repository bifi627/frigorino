import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getHouseholdSettingsQueryKey,
    updateHouseholdSettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateHouseholdSettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateHouseholdSettingsMutation(),
        onSuccess: (data, variables) => {
            queryClient.setQueryData(
                getHouseholdSettingsQueryKey({
                    path: { householdId: variables.path.householdId },
                }),
                data,
            );
        },
    });
};
