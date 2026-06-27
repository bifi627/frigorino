import { useQuery } from "@tanstack/react-query";
import { getProductsOptions } from "../../lib/api/@tanstack/react-query.gen";

export const useProducts = (householdId: number, enabled = true) =>
    useQuery({
        ...getProductsOptions({ path: { householdId } }),
        enabled: enabled && householdId > 0,
        staleTime: 1000 * 60 * 5,
    });
