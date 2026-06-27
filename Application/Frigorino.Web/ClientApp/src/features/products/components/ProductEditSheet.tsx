import {
    Box,
    Button,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    FormControl,
    InputLabel,
    MenuItem,
    Select,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { ConfirmDialog } from "../../../components/dialogs/ConfirmDialog";
import type {
    ExpiryHandling,
    ProductCatalogItem,
    ProductCategory,
} from "../../../lib/api/types.gen";
import {
    EXPIRY_HANDLING_OPTIONS,
    PRODUCT_CATEGORY_OPTIONS,
} from "../productClassificationOptions";
import { useDeleteProduct } from "../useDeleteProduct";
import { useOverrideProductClassification } from "../useOverrideProductClassification";
import { useResetProductClassification } from "../useResetProductClassification";

interface Props {
    open: boolean;
    onClose: () => void;
    householdId: number;
    product: ProductCatalogItem | null;
}

export function ProductEditSheet({
    open,
    onClose,
    householdId,
    product,
}: Props) {
    // Remount the inner form per opened product so fields seed from the row via useState
    // initializers (no reset-in-effect).
    return (
        <Dialog open={open} onClose={onClose} fullWidth maxWidth="xs">
            {product && (
                <ProductEditSheetInner
                    key={product.id}
                    onClose={onClose}
                    householdId={householdId}
                    product={product}
                />
            )}
        </Dialog>
    );
}

interface InnerProps {
    onClose: () => void;
    householdId: number;
    product: ProductCatalogItem;
}

function ProductEditSheetInner({ onClose, householdId, product }: InnerProps) {
    const { t } = useTranslation();
    const override = useOverrideProductClassification();
    const reset = useResetProductClassification();
    const del = useDeleteProduct();

    const [confirmDelete, setConfirmDelete] = useState(false);

    const [category, setCategory] = useState<ProductCategory>(
        product.effectiveCategory,
    );
    const [handling, setHandling] = useState<ExpiryHandling>(
        product.effectiveExpiryHandling,
    );
    const [days, setDays] = useState(() =>
        product.effectiveShelfLifeDays != null
            ? String(product.effectiveShelfLifeDays)
            : "",
    );

    const showDays = handling === "AiRecommendsShelfLife";

    const onHandlingChange = (next: ExpiryHandling) => {
        setHandling(next);
        if (next !== "AiRecommendsShelfLife") {
            setDays("");
        }
    };

    const save = async () => {
        let shelfLifeDays: number | null = null;
        if (handling === "AiRecommendsShelfLife") {
            const parsed = Number(days);
            if (!Number.isInteger(parsed) || parsed < 1 || parsed > 365) {
                toast.error(t("products.saveFailed"));
                return;
            }
            shelfLifeDays = parsed;
        }

        try {
            await override.mutateAsync({
                path: { householdId, productId: product.id },
                body: { category, expiryHandling: handling, shelfLifeDays },
            });
            toast.success(t("products.saved"));
            onClose();
        } catch {
            toast.error(t("products.saveFailed"));
        }
    };

    const resetToAi = async () => {
        try {
            await reset.mutateAsync({
                path: { householdId, productId: product.id },
            });
            toast.success(t("products.resetDone"));
            onClose();
        } catch {
            toast.error(t("products.saveFailed"));
        }
    };

    const deleteProduct = async () => {
        try {
            await del.mutateAsync({
                path: { householdId, productId: product.id },
            });
            toast.success(t("products.deleted"));
            onClose();
        } catch {
            toast.error(t("products.deleteFailed"));
            setConfirmDelete(false);
        }
    };

    const busy = override.isPending || reset.isPending || del.isPending;

    return (
        <>
            <DialogTitle sx={{ textTransform: "capitalize" }}>
                {product.name}
            </DialogTitle>
            <DialogContent>
                <Stack spacing={2} sx={{ mt: 1 }}>
                    <FormControl
                        fullWidth
                        size="small"
                        data-testid="product-category-control"
                    >
                        <InputLabel>{t("products.category")}</InputLabel>
                        <Select
                            label={t("products.category")}
                            value={category}
                            onChange={(e) =>
                                setCategory(e.target.value as ProductCategory)
                            }
                        >
                            {PRODUCT_CATEGORY_OPTIONS.map((c) => (
                                <MenuItem key={c} value={c}>
                                    {t(`productCategories.${c}`)}
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>

                    <FormControl
                        fullWidth
                        size="small"
                        data-testid="product-expiry-control"
                    >
                        <InputLabel>{t("products.expiry")}</InputLabel>
                        <Select
                            label={t("products.expiry")}
                            value={handling}
                            onChange={(e) =>
                                onHandlingChange(
                                    e.target.value as ExpiryHandling,
                                )
                            }
                        >
                            {EXPIRY_HANDLING_OPTIONS.map((h) => (
                                <MenuItem key={h} value={h}>
                                    {t(`expiryHandlings.${h}`)}
                                </MenuItem>
                            ))}
                        </Select>
                    </FormControl>

                    {showDays && (
                        <TextField
                            type="number"
                            size="small"
                            fullWidth
                            label={t("products.shelfLifeDays")}
                            value={days}
                            onChange={(e) => setDays(e.target.value)}
                            slotProps={{
                                htmlInput: {
                                    min: 1,
                                    max: 365,
                                    "data-testid": "product-shelf-life-input",
                                },
                            }}
                        />
                    )}

                    <Typography variant="caption" color="text.secondary">
                        {t("products.aiSuggests", {
                            value: t(`productCategories.${product.aiCategory}`),
                        })}
                    </Typography>
                </Stack>
            </DialogContent>
            <DialogActions sx={{ justifyContent: "space-between" }}>
                <Box>
                    <Button
                        color="error"
                        disabled={busy}
                        onClick={() => setConfirmDelete(true)}
                        data-testid="product-delete-button"
                    >
                        {t("products.delete")}
                    </Button>
                    {product.isOverridden && (
                        <Button
                            color="inherit"
                            disabled={busy}
                            onClick={resetToAi}
                            data-testid="product-reset-button"
                        >
                            {t("products.reset")}
                        </Button>
                    )}
                </Box>
                <Box>
                    <Button color="inherit" disabled={busy} onClick={onClose}>
                        {t("products.cancel")}
                    </Button>
                    <Button
                        variant="contained"
                        disabled={busy}
                        onClick={save}
                        data-testid="product-save-button"
                    >
                        {t("products.save")}
                    </Button>
                </Box>
            </DialogActions>

            <ConfirmDialog
                open={confirmDelete}
                onClose={() => setConfirmDelete(false)}
                onConfirm={deleteProduct}
                title={t("products.deleteConfirmTitle")}
                description={t("products.deleteConfirmBody", {
                    name: product.name,
                })}
                confirmLabel={t("products.delete")}
                cancelLabel={t("products.cancel")}
                isPending={del.isPending}
                confirmTestId="product-delete-confirm-button"
                cancelTestId="product-delete-cancel-button"
                maxWidth="xs"
            />
        </>
    );
}
