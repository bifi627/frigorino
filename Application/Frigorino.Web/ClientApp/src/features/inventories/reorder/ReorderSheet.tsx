import { Close } from "@mui/icons-material";
import {
    Box,
    Button,
    Drawer,
    IconButton,
    MenuItem,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { QuantityDraftFields } from "../../../components/common/QuantityDraftFields";
import {
    draftToQuantity,
    EMPTY_QUANTITY_DRAFT,
    isDraftValid,
    quantityToDraft,
    type QuantityDraft,
} from "../../../components/composer";
import type { InventoryItemResponse } from "../../../lib/api";
import { useCreateListItem } from "../../lists/items/useCreateListItem";
import { useHouseholdLists } from "../../lists/useHouseholdLists";

interface ReorderSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    item: InventoryItemResponse | null;
}

// Single-item mirror of the promote review sheet, retargeted at a shopping list. Pre-fills the
// inventory item's name + structured quantity; confirming creates a list item via the existing
// CreateItem write. The inventory item is never touched.
export const ReorderSheet = ({
    open,
    onClose,
    householdId,
    item,
}: ReorderSheetProps) => {
    const { t } = useTranslation();
    const { data: lists = [] } = useHouseholdLists(householdId, householdId > 0);
    const createItem = useCreateListItem();

    const [name, setName] = useState("");
    const [draft, setDraft] = useState<QuantityDraft>(EMPTY_QUANTITY_DRAFT);
    const [listId, setListId] = useState<number | null>(null);

    // Re-seed the editable form each time the sheet opens for a (possibly different) item.
    // The fields are local editable copies of props, so reset-on-open is a legitimate effect.
    useEffect(() => {
        if (open && item) {
            /* eslint-disable react-hooks/set-state-in-effect */
            setName(item.text ?? "");
            setDraft(quantityToDraft(item.quantity ?? null));
            setListId(null);
            /* eslint-enable react-hooks/set-state-in-effect */
        }
    }, [open, item]);

    // Effective target: explicit pick, else the newest list (GetLists is newest-first).
    const targetId = listId ?? lists[0]?.id ?? null;
    const targetName = lists.find((l) => l.id === targetId)?.name ?? "";

    const trimmedName = name.trim();
    const canSubmit =
        trimmedName.length > 0 &&
        targetId !== null &&
        isDraftValid(draft) &&
        !createItem.isPending;

    const handleConfirm = async () => {
        if (targetId === null || trimmedName.length === 0) {
            return;
        }
        try {
            await createItem.mutateAsync({
                path: { householdId, listId: targetId },
                body: {
                    text: trimmedName,
                    comment: null,
                    quantity: draftToQuantity(draft),
                },
            });
            toast.success(
                t("reorder.added", { name: trimmedName, list: targetName }),
            );
            onClose();
        } catch {
            // Leave the sheet open on failure so the user can retry.
        }
    };

    return (
        <Drawer
            anchor="bottom"
            open={open}
            onClose={onClose}
            data-testid="reorder-sheet"
            slotProps={{
                paper: {
                    sx: { borderTopLeftRadius: 16, borderTopRightRadius: 16 },
                },
            }}
        >
            <Box sx={{ p: 2, maxWidth: 600, mx: "auto", width: "100%" }}>
                <Stack
                    direction="row"
                    sx={{
                        alignItems: "center",
                        justifyContent: "space-between",
                    }}
                >
                    <Typography variant="h6">
                        {t("reorder.sheetTitle")}
                    </Typography>
                    <IconButton
                        onClick={onClose}
                        size="small"
                        aria-label="close"
                    >
                        <Close />
                    </IconButton>
                </Stack>

                <Stack spacing={2} sx={{ mt: 2 }}>
                    <TextField
                        fullWidth
                        size="small"
                        label={t("reorder.name")}
                        value={name}
                        onChange={(e) => setName(e.target.value)}
                        slotProps={{
                            inputLabel: { shrink: true },
                            htmlInput: { "data-testid": "reorder-name-input" },
                        }}
                    />

                    <QuantityDraftFields
                        draft={draft}
                        onChange={setDraft}
                        valueTestId="reorder-quantity-value"
                        unitTestId="reorder-quantity-unit"
                    />

                    {lists.length > 1 && (
                        <TextField
                            select
                            fullWidth
                            size="small"
                            label={t("reorder.targetList")}
                            value={targetId ?? ""}
                            onChange={(e) => setListId(Number(e.target.value))}
                            data-testid="reorder-list-picker"
                        >
                            {lists.map((l) => (
                                <MenuItem key={l.id} value={l.id}>
                                    {l.name}
                                </MenuItem>
                            ))}
                        </TextField>
                    )}

                    <Button
                        fullWidth
                        variant="contained"
                        disabled={!canSubmit}
                        onClick={handleConfirm}
                        data-testid="reorder-confirm-button"
                    >
                        {t("reorder.add", { list: targetName })}
                    </Button>
                </Stack>
            </Box>
        </Drawer>
    );
};
