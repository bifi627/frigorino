import { useQuery } from "@tanstack/react-query";
import { getExpiryCalendarOptions } from "../../../lib/api/@tanstack/react-query.gen";

export const useExpiryCalendar = (householdId: number, enabled = true) =>
    useQuery({
        ...getExpiryCalendarOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 30,
    });
