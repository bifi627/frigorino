import type { TFunction } from "i18next";
import type { QuantityDto, QuantityUnit } from "../../../lib/api";

// QuantityUnit is emitted as a string union by the generated client. These mirror the backend
// enum members (Gram .. Bag). The label text comes from i18n (quantityUnits.<Name>).
// Update this list (and the quantityUnits i18n keys) after `npm run api` if units change.
export const QUANTITY_UNIT_VALUES = [
    "Gram",
    "Kilogram",
    "Milliliter",
    "Liter",
    "Piece",
    "Pack",
    "Can",
    "Bottle",
    "Bag",
] as const satisfies readonly QuantityUnit[];

export const unitLabel = (t: TFunction, unit: QuantityUnit): string =>
    t(`quantityUnits.${unit}`);

// Render a structured quantity like "1.5 l" / "2 pc". Number() coerces the decimal
// (which the client may emit as number | string) and trims trailing zeros.
export const formatQuantity = (t: TFunction, q: QuantityDto): string => {
    const value = Number(q.value);
    return `${value.toString()} ${unitLabel(t, q.unit)}`;
};

// Multiply a structured quantity by a scale factor for DISPLAY only. Rounds to 3 decimals to match
// the numeric(12,3) DB column, so a scaled value shown here equals what a future promote-to-list
// would persist. Unit is unchanged (no g<->kg conversion). Caller skips this when multiplier === 1.
export const scaleQuantity = (
    q: QuantityDto,
    multiplier: number,
): QuantityDto => {
    const scaled = Number(q.value) * multiplier;
    const rounded = Math.round(scaled * 1000) / 1000;
    return { ...q, value: rounded };
};
