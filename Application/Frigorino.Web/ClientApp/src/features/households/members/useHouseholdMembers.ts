import { useQuery } from "@tanstack/react-query";
import { getMembersOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useHouseholdMembers = (householdId: number, enabled = true) =>
    useQuery({
        ...getMembersOptions({ path: { householdId } }),
        enabled: enabled && !!householdId,
    });
