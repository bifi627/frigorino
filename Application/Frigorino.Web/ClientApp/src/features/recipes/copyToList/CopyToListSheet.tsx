import { Close } from "@mui/icons-material";
import {
    Box,
    Button,
    Checkbox,
    Drawer,
    IconButton,
    MenuItem,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useRouter } from "@tanstack/react-router";
import { toast } from "sonner";
import {
    draftToQuantity,
    isDraftValid,
    quantityToDraft,
    scaleQuantity,
    type QuantityDraft,
} from "../../../components/composer";
import { QuantityDraftFields } from "../../../components/common/QuantityDraftFields";
import type { RecipeItemResponse } from "../../../lib/api";
import { useHouseholdLists } from "../../lists/useHouseholdLists";
import { useRecipeItems } from "../items/useRecipeItems";
import { useCopyRecipeToList } from "./useCopyRecipeToList";

interface CopyToListSheetProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
    recipeId: number;
    // Display scale factor from the recipe view's servings stepper. 1 = unscaled.
    multiplier: number;
}

interface RowDraft {
    selected: boolean;
    quantity: QuantityDraft;
}

export const CopyToListSheet = ({
    open,
    onClose,
    householdId,
    recipeId,
    multiplier,
}: CopyToListSheetProps) => {
    const { t } = useTranslation();
    const router = useRouter();
    const { data: items = [] } = useRecipeItems(householdId, recipeId, open);
    const { data: lists = [] } = useHouseholdLists(householdId, open);
    const copy = useCopyRecipeToList();

    const [listId, setListId] = useState<number | null>(null);
    // Effective target: explicit pick, else the first list returned.
    const targetId = listId ?? lists[0]?.id ?? null;
    const targetName = lists.find((l) => l.id === targetId)?.name ?? "";

    // Drafts keyed by recipeItemId; (re)seeded from the current items, pre-filled with the SCALED
    // quantity (WYSIWYG). multiplier === 1 leaves the base quantity unchanged.
    const [drafts, setDrafts] = useState<Record<number, RowDraft>>({});
    const seeded = useMemo(() => {
        const next: Record<number, RowDraft> = {};
        for (const item of items) {
            const scaled =
                item.quantity && multiplier !== 1
                    ? scaleQuantity(item.quantity, multiplier)
                    : (item.quantity ?? null);
            next[item.id] = drafts[item.id] ?? {
                selected: true,
                quantity: quantityToDraft(scaled),
            };
        }
        return next;
    }, [items, drafts, multiplier]);

    const updateDraft = (id: number, patch: Partial<RowDraft>) =>
        setDrafts((d) => ({
            ...d,
            [id]: { ...(d[id] ?? seeded[id]), ...patch },
        }));

    const selectedCount = items.filter((i) => seeded[i.id]?.selected).length;
    const allSelected = items.length > 0 && selectedCount === items.length;
    const someSelected = selectedCount > 0 && selectedCount < items.length;

    const handleToggleAll = (selected: boolean) =>
        setDrafts((d) => {
            const next = { ...d };
            for (const i of items) {
                next[i.id] = { ...(d[i.id] ?? seeded[i.id]), selected };
            }
            return next;
        });

    // Empty quantity is allowed (text-only ingredient); only a non-empty invalid value blocks.
    const hasRowInvalidQuantity = items.some(
        (i) => seeded[i.id]?.selected && !isDraftValid(seeded[i.id].quantity),
    );

    const handleAdd = async () => {
        if (!targetId) return;
        const payload = items
            .filter((i) => seeded[i.id]?.selected)
            .map((i) => ({
                recipeItemId: i.id,
                quantity: draftToQuantity(seeded[i.id].quantity),
            }));
        if (payload.length === 0) return;
        try {
            const result = await copy.mutateAsync({
                path: { householdId, recipeId },
                body: { targetListId: targetId, items: payload },
            });
            if (result.copiedCount > 0) {
                toast.success(
                    t("copyToList.added", {
                        count: result.copiedCount,
                        list: targetName,
                    }),
                );
            }
            setDrafts({});
            onClose();
        } catch {
            // Leave the sheet intact on failure; the user can retry.
        }
    };

    const hasLists = lists.length > 0;

    return (
        <Drawer
            anchor="bottom"
            open={open}
            onClose={onClose}
            data-testid="copy-to-list-sheet"
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
                        {t("copyToList.sheetTitle")}
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
                    {t("copyToList.sheetSubtitle")}
                </Typography>

                {!hasLists ? (
                    <Stack
                        spacing={1}
                        sx={{ py: 2 }}
                        data-testid="copy-to-list-no-lists"
                    >
                        <Typography variant="body2" color="text.secondary">
                            {t("copyToList.noLists")}
                        </Typography>
                        <Button
                            variant="contained"
                            onClick={() => {
                                onClose();
                                router.navigate({ to: "/lists/create" });
                            }}
                            data-testid="copy-to-list-create-list"
                        >
                            {t("copyToList.createList")}
                        </Button>
                    </Stack>
                ) : (
                    <>
                        {lists.length > 1 && (
                            <TextField
                                select
                                fullWidth
                                size="small"
                                label={t("copyToList.target")}
                                value={targetId ?? ""}
                                onChange={(e) =>
                                    setListId(Number(e.target.value))
                                }
                                data-testid="copy-to-list-picker"
                                sx={{ mb: 2 }}
                            >
                                {lists.map((l) => (
                                    <MenuItem key={l.id} value={l.id}>
                                        {l.name}
                                    </MenuItem>
                                ))}
                            </TextField>
                        )}

                        {items.length > 1 && (
                            <Stack
                                direction="row"
                                sx={{ alignItems: "center", mb: 1 }}
                            >
                                <Checkbox
                                    edge="start"
                                    checked={allSelected}
                                    indeterminate={someSelected}
                                    onChange={(e) =>
                                        handleToggleAll(e.target.checked)
                                    }
                                    data-testid="copy-to-list-select-all"
                                />
                                <Typography
                                    variant="body2"
                                    color="text.secondary"
                                >
                                    {t("copyToList.selectAll")}
                                </Typography>
                            </Stack>
                        )}

                        <Stack spacing={1.5}>
                            {items.map((item) => (
                                <CopyRow
                                    key={item.id}
                                    item={item}
                                    draft={seeded[item.id]}
                                    onChange={(patch) =>
                                        updateDraft(item.id, patch)
                                    }
                                />
                            ))}
                        </Stack>

                        <Button
                            fullWidth
                            variant="contained"
                            sx={{ mt: 2 }}
                            disabled={
                                selectedCount === 0 ||
                                !targetId ||
                                copy.isPending ||
                                hasRowInvalidQuantity
                            }
                            onClick={handleAdd}
                            data-testid="copy-to-list-add-button"
                        >
                            {t("copyToList.addCount", {
                                count: selectedCount,
                                list: targetName,
                            })}
                        </Button>
                    </>
                )}
            </Box>
        </Drawer>
    );
};

interface CopyRowProps {
    item: RecipeItemResponse;
    draft: RowDraft;
    onChange: (patch: Partial<RowDraft>) => void;
}

const CopyRow = ({ item, draft, onChange }: CopyRowProps) => {
    return (
        <Box
            data-testid={`copy-to-list-row-${item.id}`}
            sx={{ border: 1, borderColor: "divider", borderRadius: 2, p: 1.5 }}
        >
            <Stack direction="row" sx={{ alignItems: "center" }} spacing={1}>
                <Checkbox
                    edge="start"
                    checked={draft.selected}
                    onChange={(e) => onChange({ selected: e.target.checked })}
                    data-testid={`copy-to-list-row-select-${item.id}`}
                />
                <Typography sx={{ flex: 1, fontWeight: 600 }}>
                    {item.text}
                </Typography>
            </Stack>
            <Box sx={{ mt: 1 }}>
                <QuantityDraftFields
                    draft={draft.quantity}
                    onChange={(quantity) => onChange({ quantity })}
                    valueTestId={`copy-to-list-row-quantity-value-${item.id}`}
                    unitTestId={`copy-to-list-row-quantity-unit-${item.id}`}
                />
            </Box>
        </Box>
    );
};
