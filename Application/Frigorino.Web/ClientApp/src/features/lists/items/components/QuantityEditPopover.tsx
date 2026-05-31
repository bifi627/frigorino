import { Box, Button, MenuItem, Popover, TextField } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { QuantityDto } from "../../../../lib/api";
import { QUANTITY_UNIT_VALUES, unitLabel } from "../quantityFormat";

interface Props {
    anchorEl: HTMLElement | null;
    current: QuantityDto | null;
    onClose: () => void;
    onSave: (quantity: QuantityDto) => void;
}

export function QuantityEditPopover({
    anchorEl,
    current,
    onClose,
    onSave,
}: Props) {
    const { t } = useTranslation();
    const [value, setValue] = useState(current ? String(current.value) : "1");
    const [unit, setUnit] = useState<number>(current?.unit ?? 4); // default Piece

    const numeric = Number(value.replace(",", "."));
    const valid = Number.isFinite(numeric) && numeric > 0;

    return (
        <Popover
            open={Boolean(anchorEl)}
            anchorEl={anchorEl}
            onClose={onClose}
            anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
        >
            <Box
                sx={{ display: "flex", gap: 1, alignItems: "center", p: 1.5 }}
                data-testid="quantity-edit-popover"
            >
                <TextField
                    autoFocus
                    size="small"
                    type="text"
                    inputMode="decimal"
                    placeholder={t("common.quantity")}
                    value={value}
                    onChange={(e) => setValue(e.target.value)}
                    sx={{ width: 90 }}
                />
                <TextField
                    select
                    size="small"
                    value={unit}
                    onChange={(e) => setUnit(Number(e.target.value))}
                    sx={{ width: 110 }}
                >
                    {QUANTITY_UNIT_VALUES.map((u) => (
                        <MenuItem key={u} value={u}>
                            {unitLabel(t, u)}
                        </MenuItem>
                    ))}
                </TextField>
                <Button
                    variant="contained"
                    size="small"
                    disabled={!valid}
                    onClick={() => {
                        onSave({ value: numeric, unit });
                        onClose();
                    }}
                >
                    {t("common.save")}
                </Button>
            </Box>
        </Popover>
    );
}
