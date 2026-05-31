import type { TFunction } from "i18next";
import type { QuantityDto } from "../../../lib/api";

// QuantityUnit is emitted as `number` by the generated client. These mirror the backend enum
// order (Gram=0 .. Bag=8). The label text comes from i18n (quantityUnits.<n>).
export const QUANTITY_UNIT_VALUES = [0, 1, 2, 3, 4, 5, 6, 7, 8] as const;

export const unitLabel = (t: TFunction, unit: number): string =>
    t(`quantityUnits.${unit}`);

// Render a structured quantity like "1.5 l" / "2 pc". Number() coerces the decimal
// (which the client may emit as number | string) and trims trailing zeros.
export const formatQuantity = (t: TFunction, q: QuantityDto): string => {
    const value = Number(q.value);
    return `${value.toString()} ${unitLabel(t, q.unit)}`;
};
