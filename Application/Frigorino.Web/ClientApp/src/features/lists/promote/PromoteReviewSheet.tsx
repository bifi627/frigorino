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
import { useHouseholdInventories } from "../../inventories/useHouseholdInventories";
import { useCreateInventoryItem } from "../../inventories/items/useCreateInventoryItem";
import { formatQuantity } from "../items/quantityFormat";
import { getExpiryInfo } from "../../../utils/dateUtils";
import {
    usePromotableForList,
    usePromotableStore,
    type PromotableEntry,
} from "./promotableStore";

interface PromoteReviewSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    listId: number;
}

// Per-row editable draft (quantity as the inventory's free-text string; expiry as YYYY-MM-DD).
interface RowDraft {
    selected: boolean;
    quantity: string;
    expiry: string;
}

export const PromoteReviewSheet = ({
    open,
    onClose,
    householdId,
    listId,
}: PromoteReviewSheetProps) => {
    const { t } = useTranslation();
    const entries = usePromotableForList(listId);
    const remove = usePromotableStore((s) => s.remove);
    const clearForList = usePromotableStore((s) => s.clearForList);
    const createItem = useCreateInventoryItem();

    const { data: inventories = [] } = useHouseholdInventories(
        householdId,
        householdId > 0,
    );

    const [inventoryId, setInventoryId] = useState<number | null>(null);
    // Effective target: explicit pick, else the newest inventory (GetInventories is newest-first).
    const targetId = inventoryId ?? inventories[0]?.id ?? null;
    const targetName = inventories.find((i) => i.id === targetId)?.name ?? "";

    // Drafts keyed by itemId; (re)seeded from the current entries.
    const [drafts, setDrafts] = useState<Record<number, RowDraft>>({});
    const seeded = useMemo(() => {
        const next: Record<number, RowDraft> = {};
        for (const e of entries) {
            next[e.itemId] = drafts[e.itemId] ?? {
                selected: true,
                quantity: e.quantity ? formatQuantity(t, e.quantity) : "",
                expiry: e.suggestedExpiry ?? "",
            };
        }
        return next;
    }, [entries, t, drafts]);

    const updateDraft = (itemId: number, patch: Partial<RowDraft>) =>
        setDrafts((d) => ({
            ...d,
            [itemId]: { ...(d[itemId] ?? seeded[itemId]), ...patch },
        }));

    const selectedCount = entries.filter(
        (e) => seeded[e.itemId]?.selected,
    ).length;

    const handleOmit = (itemId: number) => {
        remove(itemId);
        setDrafts((d) => {
            const next = { ...d };
            delete next[itemId];
            return next;
        });
    };

    const handleClearAll = () => {
        clearForList(listId);
        setDrafts({});
        onClose();
    };

    const handleAdd = async () => {
        if (!targetId) return;
        const toAdd = entries.filter((e) => seeded[e.itemId]?.selected);
        for (const entry of toAdd) {
            const draft = seeded[entry.itemId];
            try {
                await createItem.mutateAsync({
                    path: { householdId, inventoryId: targetId },
                    body: {
                        text: entry.name,
                        quantity: draft.quantity || null,
                        expiryDate: draft.expiry || null,
                    },
                });
                remove(entry.itemId);
            } catch {
                // Leave the entry in the batch on failure; the user can retry.
            }
        }
        // Close once nothing is left for this list.
        if (
            usePromotableStore
                .getState()
                .entries.every((e) => e.listId !== listId)
        ) {
            onClose();
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

                <Stack spacing={1.5}>
                    {entries.map((entry) => (
                        <PromoteRow
                            key={entry.itemId}
                            entry={entry}
                            draft={seeded[entry.itemId]}
                            onChange={(patch) =>
                                updateDraft(entry.itemId, patch)
                            }
                            onOmit={() => handleOmit(entry.itemId)}
                        />
                    ))}
                </Stack>

                <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
                    <Button
                        fullWidth
                        color="inherit"
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
                            createItem.isPending
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
    entry: PromotableEntry;
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

    return (
        <Box
            data-testid={`promote-row-${entry.name}`}
            sx={{ border: 1, borderColor: "divider", borderRadius: 2, p: 1.5 }}
        >
            <Stack direction="row" sx={{ alignItems: "center" }} spacing={1}>
                <Checkbox
                    edge="start"
                    checked={draft.selected}
                    onChange={(e) => onChange({ selected: e.target.checked })}
                    data-testid={`promote-row-select-${entry.name}`}
                />
                <Typography sx={{ flex: 1, fontWeight: 600 }}>
                    {entry.name}
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
                    aria-label="omit"
                    data-testid={`promote-row-omit-${entry.name}`}
                >
                    <Close fontSize="small" />
                </IconButton>
            </Stack>
            <Stack direction="row" spacing={1} sx={{ mt: 1 }}>
                <TextField
                    size="small"
                    label={t("promote.quantity")}
                    value={draft.quantity}
                    onChange={(e) => onChange({ quantity: e.target.value })}
                    sx={{ width: 110 }}
                />
                <TextField
                    size="small"
                    type="date"
                    label={t("promote.expiry")}
                    value={draft.expiry}
                    onChange={(e) => onChange({ expiry: e.target.value })}
                    slotProps={{ inputLabel: { shrink: true } }}
                    helperText={info?.humanReadable || " "}
                    sx={{ flex: 1 }}
                />
            </Stack>
        </Box>
    );
};
