import { useQuery } from "@tanstack/react-query";
import { getHouseholdSettingsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useHouseholdSettings = (householdId: number, enabled = true) =>
    useQuery({
        ...getHouseholdSettingsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 5,
    });
