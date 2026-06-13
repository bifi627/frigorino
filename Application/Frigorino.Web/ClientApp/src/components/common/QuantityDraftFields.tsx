import { MenuItem, Stack, TextField } from "@mui/material";
import { useTranslation } from "react-i18next";
import {
    isDraftValid,
    QUANTITY_UNIT_VALUES,
    unitLabel,
    type QuantityDraft,
} from "../composer";
import type { QuantityUnit } from "../../lib/api";

interface QuantityDraftFieldsProps {
    draft: QuantityDraft;
    onChange: (draft: QuantityDraft) => void;
    /** Full testid for the value input (placed on the htmlInput). */
    valueTestId: string;
    /** Full testid for the unit Select (placed on the FormControl). */
    unitTestId: string;
}

// Value + unit pair for editing a QuantityDraft. Shared by the promote review sheet and the
// inventory → list re-order sheet so both edit quantity identically.
export const QuantityDraftFields = ({
    draft,
    onChange,
    valueTestId,
    unitTestId,
}: QuantityDraftFieldsProps) => {
    const { t } = useTranslation();
    return (
        <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
            <TextField
                size="small"
                type="text"
                label={t("common.quantity")}
                placeholder={t("common.quantity")}
                value={draft.value}
                onChange={(e) => onChange({ ...draft, value: e.target.value })}
                error={!isDraftValid(draft)}
                slotProps={{
                    inputLabel: { shrink: true },
                    htmlInput: {
                        inputMode: "decimal",
                        "data-testid": valueTestId,
                    },
                }}
                sx={{ width: 90 }}
            />
            <TextField
                select
                size="small"
                label={t("common.unit")}
                value={draft.unit}
                onChange={(e) =>
                    onChange({ ...draft, unit: e.target.value as QuantityUnit })
                }
                data-testid={unitTestId}
                slotProps={{ inputLabel: { shrink: true } }}
                sx={{ flex: 1, minWidth: 120 }}
            >
                {QUANTITY_UNIT_VALUES.map((u) => (
                    <MenuItem key={u} value={u}>
                        {unitLabel(t, u)}
                    </MenuItem>
                ))}
            </TextField>
        </Stack>
    );
};
