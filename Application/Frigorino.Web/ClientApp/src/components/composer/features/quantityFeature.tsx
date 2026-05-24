/* eslint-disable react-refresh/only-export-components -- this module exports a feature
   descriptor (non-component) alongside local presentational components. */
import { Add, Clear, Remove, ShoppingBag } from "@mui/icons-material";
import { Box, Button, ButtonGroup, Chip, IconButton, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import { defineModifier } from "../defineFeature";
import type { FeatureSlot } from "../types";

const QuantityToggle = ({ value, open, toggleOpen }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <IconButton
            onClick={toggleOpen}
            aria-label={t("common.quantity")}
            sx={{
                minWidth: 44,
                minHeight: 44,
                color: value || open ? "primary.main" : "inherit",
            }}
        >
            <ShoppingBag fontSize="small" />
        </IconButton>
    );
};

const QuantityChip = ({ value, toggleOpen }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    return (
        <Chip
            clickable
            onClick={toggleOpen}
            aria-label={`${t("common.edit")} ${t("common.quantity")}`}
            size="small"
            icon={<ShoppingBag fontSize="small" />}
            label={value}
            sx={{ minHeight: 32 }}
        />
    );
};

const QuantityPanel = ({ value, setValue, disabled }: FeatureSlot<string>) => {
    const { t } = useTranslation();
    const trimmed = value.trim();
    // Steppers only apply to a plain integer; free-text like "2 kg" stays untouched.
    const numeric =
        trimmed === ""
            ? 0
            : /^\d+$/.test(trimmed)
              ? parseInt(trimmed, 10)
              : null;
    return (
        <Box
            sx={{ display: "flex", gap: 0.75, alignItems: "center", p: 1 }}
            onClick={(e) => e.stopPropagation()}
        >
            <TextField
                fullWidth
                variant="outlined"
                placeholder={t("common.quantity")}
                value={value}
                onChange={(e) => setValue(e.target.value)}
                disabled={disabled}
                size="small"
            />
            <ButtonGroup variant="outlined" size="small">
                {[1, 2, 5].map((num) => (
                    <Button
                        key={num}
                        onClick={() => setValue(num.toString())}
                        variant={value === num.toString() ? "contained" : "outlined"}
                        size="small"
                        sx={{ minWidth: 44, minHeight: 44 }}
                    >
                        {num}
                    </Button>
                ))}
            </ButtonGroup>
            <IconButton
                onClick={() => {
                    if (numeric !== null && numeric > 0) {
                        setValue((numeric - 1).toString());
                    }
                }}
                disabled={numeric === null || numeric <= 0}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Remove fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => {
                    if (numeric !== null) {
                        setValue((numeric + 1).toString());
                    }
                }}
                disabled={numeric === null}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Add fontSize="small" />
            </IconButton>
            <IconButton
                onClick={() => setValue("")}
                disabled={!value}
                title={t("common.clear")}
                sx={{ minWidth: 44, minHeight: 44 }}
            >
                <Clear fontSize="small" />
            </IconButton>
        </Box>
    );
};

export const quantityFeature = defineModifier({
    id: "quantity",
    initial: "" as string,
    isEmpty: (value) => value.trim() === "",
    renderToggle: (slot) => <QuantityToggle {...slot} />,
    renderPanel: (slot) => <QuantityPanel {...slot} />,
    renderChip: (slot) => <QuantityChip {...slot} />,
});
