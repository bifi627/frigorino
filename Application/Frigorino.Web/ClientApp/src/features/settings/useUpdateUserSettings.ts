import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
    getUserSettingsQueryKey,
    updateUserSettingsMutation,
} from "../../lib/api/@tanstack/react-query.gen";

export const useUpdateUserSettings = () => {
    const queryClient = useQueryClient();

    return useMutation({
        ...updateUserSettingsMutation(),
        onSuccess: (data) => {
            queryClient.setQueryData(getUserSettingsQueryKey(), data);
        },
    });
};
