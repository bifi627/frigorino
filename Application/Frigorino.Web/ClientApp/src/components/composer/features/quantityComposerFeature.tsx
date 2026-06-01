/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { Clear, ShoppingBag } from "@mui/icons-material";
import { Box, Chip, IconButton, MenuItem, TextField } from "@mui/material";
import type { TFunction } from "i18next";
import { useTranslation } from "react-i18next";
import type { QuantityDto, QuantityUnit } from "../../../lib/api";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";
import { QUANTITY_UNIT_VALUES, unitLabel } from "./quantityFormat";

// The composer keeps the in-progress quantity as a draft (free-text value + unit) rather than a
// committed QuantityDto, so a half-typed value ("1.", "") doesn't have to round-trip through a
// number. `draftToQuantity` converts on send; an empty/invalid value yields null (= "preserve").
// Shared by the Lists and Inventories item composers.
export interface QuantityDraft {
    value: string;
    unit: QuantityUnit;
}

const PIECE_UNIT: QuantityUnit = "Piece"; // the default for a bare count

export const EMPTY_QUANTITY_DRAFT: QuantityDraft = {
    value: "",
    unit: PIECE_UNIT,
};

export const quantityToDraft = (
    q: QuantityDto | null | undefined,
): QuantityDraft =>
    q ? { value: String(q.value), unit: q.unit } : EMPTY_QUANTITY_DRAFT;

export const draftToQuantity = (d: QuantityDraft): QuantityDto | null => {
    const numeric = Number(d.value.replace(",", "."));
    return Number.isFinite(numeric) && numeric > 0
        ? { value: numeric, unit: d.unit }
        : null;
};

// A draft is valid when the field is empty (= "clear the quantity") or holds a positive number.
// A non-empty, non-positive/unparseable value is invalid — the composer blocks send rather than
// silently treating it as a clear, which would wipe an existing quantity.
export const isDraftValid = (d: QuantityDraft): boolean => {
    if (d.value.trim() === "") {
        return true;
    }
    const numeric = Number(d.value.replace(",", "."));
    return Number.isFinite(numeric) && numeric > 0;
};

const draftLabel = (t: TFunction, d: QuantityDraft): string =>
    `${d.value.trim()} ${unitLabel(t, d.unit)}`;

const QuantityToggle = ({
    value,
    open,
    toggleOpen,
}: FeatureSlot<QuantityDraft>) => {
    const { t } = useTranslation();
    return (
        <IconButton
            onClick={toggleOpen}
            aria-label={t("common.quantity")}
            sx={{
                minWidth: 44,
                minHeight: 44,
                color: value.value.trim() || open ? "primary.main" : "inherit",
            }}
        >
            <ShoppingBag fontSize="small" />
        </IconButton>
    );
};

const QuantityChip = ({ value, toggleOpen }: FeatureSlot<QuantityDraft>) => {
    const { t } = useTranslation();
    return (
        <Chip
            clickable
            onClick={toggleOpen}
            aria-label={`${t("common.edit")} ${t("common.quantity")}`}
            size="small"
            icon={<ShoppingBag fontSize="small" />}
            label={draftLabel(t, value)}
            sx={{ minHeight: 32 }}
        />
    );
};

const QuantityPanel = ({
    value,
    setValue,
    disabled,
}: FeatureSlot<QuantityDraft>) => {
    const { t } = useTranslation();
    const invalid = !isDraftValid(value);
    return (
        <Box
            sx={{ display: "flex", gap: 1, alignItems: "flex-start", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                variant="outlined"
                type="text"
                placeholder={t("common.quantity")}
                value={value.value}
                onChange={(e) => setValue({ ...value, value: e.target.value })}
                disabled={disabled}
                error={invalid}
                helperText={invalid ? t("common.invalidQuantity") : undefined}
                size="small"
                slotProps={{
                    htmlInput: {
                        inputMode: "decimal",
                        "data-testid": "composer-quantity-value",
                    },
                }}
                sx={{ width: 110 }}
            />
            <TextField
                select
                size="small"
                value={value.unit}
                onChange={(e) =>
                    setValue({ ...value, unit: e.target.value as QuantityUnit })
                }
                disabled={disabled}
                sx={{ flex: 1, minWidth: 120 }}
            >
                {QUANTITY_UNIT_VALUES.map((u) => (
                    <MenuItem key={u} value={u}>
                        {unitLabel(t, u)}
                    </MenuItem>
                ))}
            </TextField>
            <IconButton
                onClick={() => setValue({ ...value, value: "" })}
                disabled={disabled || value.value.trim() === ""}
                title={t("common.clear")}
                aria-label={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const quantityComposerFeature = defineModifier({
    id: "quantity",
    initial: EMPTY_QUANTITY_DRAFT,
    isEmpty: (value) => value.value.trim() === "",
    isValid: isDraftValid,
    renderToggle: (slot) => <QuantityToggle {...slot} />,
    renderPanel: (slot) => <QuantityPanel {...slot} />,
    renderChip: (slot) => <QuantityChip {...slot} />,
});
