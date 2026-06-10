import { Close } from "@mui/icons-material";
import {
    Box,
    Button,
    Checkbox,
    Chip,
    Drawer,
    IconButton,
    MenuItem,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
    draftToQuantity,
    isDraftValid,
    quantityToDraft,
    QUANTITY_UNIT_VALUES,
    unitLabel,
    type QuantityDraft,
} from "../../../components/composer";
import type { QuantityUnit } from "../../../lib/api";
import type { PendingPromotionResponse } from "../../../lib/api/types.gen";
import { ExpiryDatePicker } from "../../../components/ExpiryDatePicker";
import { useHouseholdInventories } from "../../inventories/useHouseholdInventories";
import { getExpiryInfo } from "../../../utils/dateUtils";
import { usePendingPromotions } from "./usePendingPromotions";
import { usePromoteListItems } from "./usePromoteListItems";
import { useSkipPromotion } from "./useSkipPromotion";

interface PromoteReviewSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    listId: number;
}

// Per-row editable draft. Quantity is editable here (a QuantityDraft) so the inventory can
// reflect what was actually bought — the server may have had fewer items, or you bought more
// than the list requested. Expiry is YYYY-MM-DD.
interface RowDraft {
    selected: boolean;
    expiry: string;
    quantity: QuantityDraft;
}

export const PromoteReviewSheet = ({
    open,
    onClose,
    householdId,
    listId,
}: PromoteReviewSheetProps) => {
    const { t } = useTranslation();
    const { data: entries = [] } = usePendingPromotions(
        householdId,
        listId,
        open,
    );
    const promote = usePromoteListItems();
    const skip = useSkipPromotion();

    const { data: inventories = [] } = useHouseholdInventories(
        householdId,
        householdId > 0,
    );

    const [inventoryId, setInventoryId] = useState<number | null>(null);
    // Effective target: explicit pick, else the newest inventory (GetInventories is newest-first).
    const targetId = inventoryId ?? inventories[0]?.id ?? null;
    const targetName = inventories.find((i) => i.id === targetId)?.name ?? "";

    // Drafts keyed by listItemId; (re)seeded from the current entries.
    const [drafts, setDrafts] = useState<Record<number, RowDraft>>({});
    const seeded = useMemo(() => {
        const next: Record<number, RowDraft> = {};
        for (const e of entries) {
            next[e.listItemId] = drafts[e.listItemId] ?? {
                selected: true,
                expiry: e.suggestedExpiry ?? "",
                quantity: quantityToDraft(e.quantity ?? null),
            };
        }
        return next;
    }, [entries, drafts]);

    const updateDraft = (listItemId: number, patch: Partial<RowDraft>) =>
        setDrafts((d) => ({
            ...d,
            [listItemId]: {
                ...(d[listItemId] ?? seeded[listItemId]),
                ...patch,
            },
        }));

    const selectedCount = entries.filter(
        (e) => seeded[e.listItemId]?.selected,
    ).length;

    const allSelected = entries.length > 0 && selectedCount === entries.length;
    const someSelected = selectedCount > 0 && selectedCount < entries.length;

    // Master toggle: flip every row to `selected` in one go, so targeting a single item is
    // "deselect all, then check the one" instead of unchecking each row by hand.
    const handleToggleAll = (selected: boolean) =>
        setDrafts((d) => {
            const next = { ...d };
            for (const e of entries) {
                next[e.listItemId] = {
                    ...(d[e.listItemId] ?? seeded[e.listItemId]),
                    selected,
                };
            }
            return next;
        });

    const hasRowMissingDate = entries.some(
        (e) => seeded[e.listItemId]?.selected && !seeded[e.listItemId]?.expiry,
    );

    const hasRowInvalidQuantity = entries.some(
        (e) =>
            seeded[e.listItemId]?.selected &&
            !isDraftValid(seeded[e.listItemId].quantity),
    );

    const handleOmit = (listItemId: number) => {
        skip.mutate({
            path: { householdId, listId },
            body: { listItemIds: [listItemId] },
        });
        setDrafts((d) => {
            const next = { ...d };
            delete next[listItemId];
            return next;
        });
    };

    const handleClearAll = () => {
        skip.mutate({
            path: { householdId, listId },
            body: { listItemIds: entries.map((e) => e.listItemId) },
        });
        setDrafts({});
        onClose();
    };

    const handleAdd = async () => {
        if (!targetId) return;
        const items = entries
            .filter((e) => seeded[e.listItemId]?.selected)
            .map((e) => {
                const draft = seeded[e.listItemId];
                return {
                    listItemId: e.listItemId,
                    quantity: draftToQuantity(draft.quantity),
                    expiryDate: draft.expiry || null,
                };
            });
        if (items.length === 0) return;
        try {
            const result = await promote.mutateAsync({
                path: { householdId, listId },
                body: { inventoryId: targetId, items },
            });
            if (result.promotedCount > 0) {
                toast.success(
                    t("promote.added", {
                        count: result.promotedCount,
                        inventory: targetName,
                    }),
                );
            }
            onClose();
        } catch {
            // Leave the batch intact on failure; the user can retry.
        }
    };

    return (
        <Drawer
            anchor="bottom"
            open={open}
            onClose={onClose}
            data-testid="promote-sheet"
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
                        {t("promote.sheetTitle")}
                    </Typography>
                    <IconButton
                        onClick={onClose}
                        size="small"
                        aria-label="close"
                    >
                        <Close />
                    </IconButton>
                </Stack>
                <Typography
                    variant="body2"
                    color="text.secondary"
                    sx={{ mb: 2 }}
                >
                    {t("promote.sheetSubtitle")}
                </Typography>

                {inventories.length > 1 && (
                    <TextField
                        select
                        fullWidth
                        size="small"
                        label={t("promote.target")}
                        value={targetId ?? ""}
                        onChange={(e) => setInventoryId(Number(e.target.value))}
                        data-testid="promote-inventory-picker"
                        sx={{ mb: 2 }}
                    >
                        {inventories.map((inv) => (
                            <MenuItem key={inv.id} value={inv.id}>
                                {inv.name}
                            </MenuItem>
                        ))}
                    </TextField>
                )}

                {entries.length > 1 && (
                    <Stack direction="row" sx={{ alignItems: "center", mb: 1 }}>
                        <Checkbox
                            edge="start"
                            checked={allSelected}
                            indeterminate={someSelected}
                            onChange={(e) => handleToggleAll(e.target.checked)}
                            data-testid="promote-select-all"
                        />
                        <Typography variant="body2" color="text.secondary">
                            {t("promote.selectAll")}
                        </Typography>
                    </Stack>
                )}

                <Stack spacing={1.5}>
                    {entries.map((entry) => (
                        <PromoteRow
                            key={entry.listItemId}
                            entry={entry}
                            draft={seeded[entry.listItemId]}
                            onChange={(patch) =>
                                updateDraft(entry.listItemId, patch)
                            }
                            onOmit={() => handleOmit(entry.listItemId)}
                        />
                    ))}
                </Stack>

                <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                    <Button
                        fullWidth
                        color="error"
                        onClick={handleClearAll}
                        data-testid="promote-clear-all"
                    >
                        {t("promote.clearAll")}
                    </Button>
                    <Button
                        fullWidth
                        variant="contained"
                        disabled={
                            selectedCount === 0 ||
                            !targetId ||
                            promote.isPending ||
                            hasRowMissingDate ||
                            hasRowInvalidQuantity
                        }
                        onClick={handleAdd}
                        data-testid="promote-add-button"
                    >
                        {t("promote.addCount", {
                            count: selectedCount,
                            inventory: targetName,
                        })}
                    </Button>
                </Stack>
            </Box>
        </Drawer>
    );
};

interface PromoteRowProps {
    entry: PendingPromotionResponse;
    draft: RowDraft;
    onChange: (patch: Partial<RowDraft>) => void;
    onOmit: () => void;
}

const PromoteRow = ({ entry, draft, onChange, onOmit }: PromoteRowProps) => {
    const { t } = useTranslation();
    const isRecommended = entry.expiryHandling === "AiRecommendsShelfLife";
    // Wrap t to satisfy getExpiryInfo's (key: string) => string signature.
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const translateKey = (key: string): string => t(key as any);
    // Same readable hint the inventory list uses; pure fn of the field, so it updates live.
    const info = draft.expiry
        ? getExpiryInfo(draft.expiry, translateKey)
        : null;
    const expiryMissing = draft.selected && !draft.expiry;

    return (
        <Box
            data-testid={`promote-row-${entry.text}`}
            sx={{ border: 1, borderColor: "divider", borderRadius: 2, p: 1.5 }}
        >
            <Stack direction="row" sx={{ alignItems: "center" }} spacing={1}>
                <Checkbox
                    edge="start"
                    checked={draft.selected}
                    onChange={(e) => onChange({ selected: e.target.checked })}
                    data-testid={`promote-row-select-${entry.text}`}
                />
                <Typography sx={{ flex: 1, fontWeight: 600 }}>
                    {entry.text}
                </Typography>
                <Chip
                    size="small"
                    label={
                        isRecommended
                            ? t("promote.tagRecommended")
                            : t("promote.tagEnterDate")
                    }
                    color={isRecommended ? "success" : "warning"}
                    variant="outlined"
                />
                <IconButton
                    size="small"
                    onClick={onOmit}
                    aria-label={t("promote.omit")}
                    data-testid={`promote-row-omit-${entry.text}`}
                >
                    <Close fontSize="small" />
                </IconButton>
            </Stack>
            <Stack spacing={1} sx={{ mt: 1 }}>
                {/* Always editable: an item listed without a quantity ("Apples") can still get
                    one here — you may have bought a specific count. Pre-filled when the source
                    had a value, empty (with placeholder) otherwise. */}
                <Stack
                    direction="row"
                    spacing={1}
                    sx={{ alignItems: "center" }}
                >
                    <TextField
                        size="small"
                        type="text"
                        label={t("common.quantity")}
                        placeholder={t("common.quantity")}
                        value={draft.quantity.value}
                        onChange={(e) =>
                            onChange({
                                quantity: {
                                    ...draft.quantity,
                                    value: e.target.value,
                                },
                            })
                        }
                        error={!isDraftValid(draft.quantity)}
                        slotProps={{
                            inputLabel: { shrink: true },
                            htmlInput: {
                                inputMode: "decimal",
                                "data-testid": `promote-row-quantity-value-${entry.text}`,
                            },
                        }}
                        sx={{ width: 90 }}
                    />
                    <TextField
                        select
                        size="small"
                        label={t("common.unit")}
                        value={draft.quantity.unit}
                        onChange={(e) =>
                            onChange({
                                quantity: {
                                    ...draft.quantity,
                                    unit: e.target.value as QuantityUnit,
                                },
                            })
                        }
                        data-testid={`promote-row-quantity-unit-${entry.text}`}
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
                <ExpiryDatePicker
                    fullWidth
                    value={draft.expiry || null}
                    onChange={(v) => onChange({ expiry: v ?? "" })}
                    label={t("promote.expiry")}
                    error={expiryMissing}
                    helperText={
                        expiryMissing
                            ? t("promote.expiryRequired")
                            : info?.humanReadable || " "
                    }
                    dataTestId={`promote-row-expiry-${entry.text}`}
                />
            </Stack>
        </Box>
    );
};
