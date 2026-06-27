import {
    Alert,
    Chip,
    Container,
    List,
    ListItemButton,
    ListItemText,
    Skeleton,
    Stack,
    TextField,
    Typography,
} from "@mui/material";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { ProductCatalogItem } from "../../../lib/api/types.gen";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { HouseholdRoleValue, roleRank } from "../../households/householdRole";
import { ProductEditSheet } from "../components/ProductEditSheet";
import { useProducts } from "../useProducts";

export function ProductCatalogPage() {
    const { t } = useTranslation();
    const { currentHousehold, hasActiveHousehold } =
        useCurrentHouseholdWithDetails();

    const householdId = currentHousehold?.householdId ?? 0;
    const role = currentHousehold?.role;
    // ponytail: client-side filter; add server-side paging if a household exceeds a few hundred products.
    const canManage =
        !!role && roleRank[role] >= roleRank[HouseholdRoleValue.Admin];

    const { data: products, isLoading } = useProducts(householdId);

    const [query, setQuery] = useState("");
    const [selected, setSelected] = useState<ProductCatalogItem | null>(null);

    const filtered = useMemo(() => {
        const q = query.trim().toLowerCase();
        const rows = products ?? [];
        if (q.length === 0) {
            return rows;
        }
        return rows.filter((p) => p.name.toLowerCase().includes(q));
    }, [products, query]);

    if (!hasActiveHousehold || householdId === 0) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    return (
        <Container maxWidth="md" sx={pageContainerSx}>
            <Typography variant="h5" sx={{ mb: 0.5 }}>
                {t("products.title")}
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
                {t("products.subtitle")}
            </Typography>

            <TextField
                fullWidth
                size="small"
                placeholder={t("products.search")}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                sx={{ mb: 2 }}
                slotProps={{ htmlInput: { "data-testid": "product-search-input" } }}
            />

            {isLoading && <Skeleton variant="rectangular" height={240} />}

            {!isLoading && filtered.length === 0 && (
                <Alert severity="info">{t("products.empty")}</Alert>
            )}

            {!isLoading && filtered.length > 0 && (
                <List data-testid="product-catalog-list">
                    {filtered.map((p) => {
                        const expiryLabel =
                            p.effectiveExpiryHandling === "AiRecommendsShelfLife" &&
                            p.effectiveShelfLifeDays != null
                                ? `${t(`expiryHandlings.${p.effectiveExpiryHandling}`)} · ${p.effectiveShelfLifeDays}d`
                                : t(`expiryHandlings.${p.effectiveExpiryHandling}`);
                        return (
                            <ListItemButton
                                key={p.id}
                                disabled={!canManage}
                                onClick={() => canManage && setSelected(p)}
                                data-testid={`product-row-${p.id}`}
                                data-overridden={p.isOverridden ? "true" : "false"}
                            >
                                <ListItemText
                                    primary={
                                        <Stack
                                            direction="row"
                                            spacing={1}
                                            sx={{ alignItems: "center" }}
                                        >
                                            <span style={{ textTransform: "capitalize" }}>
                                                {p.name}
                                            </span>
                                            {p.isOverridden && (
                                                <Chip
                                                    size="small"
                                                    label={t("products.overridden")}
                                                    data-testid={`product-overridden-${p.id}`}
                                                />
                                            )}
                                        </Stack>
                                    }
                                    secondary={`${t(`productCategories.${p.effectiveCategory}`)} · ${expiryLabel}`}
                                />
                            </ListItemButton>
                        );
                    })}
                </List>
            )}

            {!canManage && (
                <Typography variant="caption" color="text.secondary">
                    {t("products.readOnlyHint")}
                </Typography>
            )}

            <ProductEditSheet
                open={selected !== null}
                onClose={() => setSelected(null)}
                householdId={householdId}
                product={selected}
            />
        </Container>
    );
}
