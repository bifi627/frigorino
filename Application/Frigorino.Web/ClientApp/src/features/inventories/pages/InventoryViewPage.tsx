import { DragIndicator, Edit, Schedule, Search } from "@mui/icons-material";
import {
    Alert,
    Box,
    CircularProgress,
    Container,
    Typography,
} from "@mui/material";
import { useParams, useRouter } from "@tanstack/react-router";
import { useCallback, useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { SearchInputRow } from "../../../components/shared/SearchInputRow";
import type {
    CreateInventoryItemRequest,
    InventoryItemResponse,
    UpdateInventoryItemRequest,
} from "../../../lib/api";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { InventoryContainer } from "../items/components/InventoryContainer";
import { InventoryFooter } from "../items/components/InventoryFooter";
import { useCreateInventoryItem } from "../items/useCreateInventoryItem";
import { useInventoryItems } from "../items/useInventoryItems";
import { useUpdateInventoryItem } from "../items/useUpdateInventoryItem";
import { useInventory } from "../useInventory";

type SortMode = "custom" | "expiryDateAsc" | "expiryDateDesc";

const SORT_MODES: SortMode[] = ["custom", "expiryDateAsc", "expiryDateDesc"];

export const InventoryViewPage = () => {
    const router = useRouter();
    const { t } = useTranslation();
    const { inventoryId: inventoryIdParam } = useParams({
        from: "/inventories/$inventoryId/view",
    });
    const inventoryId = parseInt(inventoryIdParam);

    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const sortModeStorageKey = `frigorino-inventory-sort-mode-${inventoryId}`;

    const loadSortMode = (): SortMode => {
        try {
            const saved = localStorage.getItem(sortModeStorageKey);
            if (saved && (SORT_MODES as string[]).includes(saved)) {
                return saved as SortMode;
            }
        } catch (err) {
            console.warn("Failed to load sort mode from localStorage:", err);
        }
        return "custom";
    };

    const [editingItem, setEditingItem] =
        useState<InventoryItemResponse | null>(null);
    const [sortMode, setSortMode] = useState<SortMode>(loadSortMode());
    const [searchOpen, setSearchOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");

    useEffect(() => {
        try {
            localStorage.setItem(sortModeStorageKey, sortMode);
        } catch (err) {
            console.warn("Failed to save sort mode to localStorage:", err);
        }
    }, [sortMode, sortModeStorageKey]);

    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: inventory,
        isLoading: inventoryLoading,
        error: inventoryError,
    } = useInventory(householdId, inventoryId, householdId > 0);

    const { data: items = [] } = useInventoryItems(
        householdId,
        inventoryId,
        !!inventory,
    );

    const createMutation = useCreateInventoryItem();
    const updateMutation = useUpdateInventoryItem();

    const scrollToLastUncheckedItem = useCallback(() => {
        if (scrollContainerRef.current) {
            const uncheckedSection = scrollContainerRef.current.querySelector(
                '[data-section="unchecked-items"]',
            );
            if (uncheckedSection) {
                const listItems =
                    uncheckedSection.querySelectorAll(".MuiListItem-root");
                const lastItem = listItems[listItems.length - 1];
                if (lastItem) {
                    lastItem.scrollIntoView({
                        behavior: "smooth",
                        block: "center",
                    });
                }
            }
        }
    }, []);

    const handleEdit = useCallback(() => {
        router.navigate({ to: `/inventories/${inventoryId}/edit` });
    }, [router, inventoryId]);

    const handleToggleSortMode = useCallback(() => {
        setSortMode((prev) => {
            const next =
                SORT_MODES[(SORT_MODES.indexOf(prev) + 1) % SORT_MODES.length];
            return next;
        });
    }, []);

    const handleToggleSearch = useCallback(() => {
        setSearchOpen((prev) => {
            // Clear the query when collapsing so the filter resets (ephemeral by design).
            if (prev) {
                setSearchQuery("");
            }
            return !prev;
        });
    }, []);

    const getSortModeIcon = (mode: SortMode) => {
        switch (mode) {
            case "expiryDateAsc":
                return <Schedule fontSize="small" />;
            case "expiryDateDesc":
                return (
                    <Schedule
                        fontSize="small"
                        style={{ transform: "scaleY(-1)" }}
                    />
                );
            case "custom":
            default:
                return <DragIndicator fontSize="small" />;
        }
    };

    const getSortModeLabel = (mode: SortMode) => {
        switch (mode) {
            case "expiryDateAsc":
                return t("inventory.sortExpiryAsc");
            case "expiryDateDesc":
                return t("inventory.sortExpiryDesc");
            case "custom":
            default:
                return t("inventory.sortManual");
        }
    };

    const handleAddItem = useCallback(
        (data: CreateInventoryItemRequest) => {
            if (!householdId) return;
            createMutation.mutate({
                path: { householdId, inventoryId },
                body: data,
            });
        },
        [createMutation, householdId, inventoryId],
    );

    const handleUpdateItem = useCallback(
        (data: UpdateInventoryItemRequest) => {
            if (editingItem?.id && householdId) {
                updateMutation.mutate({
                    path: {
                        householdId,
                        inventoryId,
                        itemId: editingItem.id,
                    },
                    body: data,
                });
                setEditingItem(null);
            }
        },
        [editingItem, updateMutation, householdId, inventoryId],
    );

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="warning">
                    {t("common.pleaseSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    if (inventoryLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    {t("inventory.loadingInventory")}
                </Typography>
            </Container>
        );
    }

    if (inventoryError || !inventory) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    {t("inventory.failedToLoadInventory")}
                </Alert>
            </Container>
        );
    }

    const directActions: HeadNavigationAction[] = [];
    const menuActions: HeadNavigationAction[] = [
        {
            text: t("common.edit"),
            icon: <Edit fontSize="small" />,
            onClick: handleEdit,
            testId: "inventory-edit-button",
        },
        {
            text: t("inventory.sortOrder"),
            secondaryText: getSortModeLabel(sortMode),
            icon: getSortModeIcon(sortMode),
            onClick: handleToggleSortMode,
            testId: "inventory-sort-toggle",
        },
        {
            text: t("common.search"),
            icon: <Search fontSize="small" />,
            onClick: handleToggleSearch,
            testId: "inventory-search-button",
        },
    ];

    return (
        <Box
            sx={{
                height: "calc(100dvh - 56px)",
                display: "flex",
                flexDirection: "column",
                overflow: "hidden",
            }}
        >
            <PageHeadActionBar
                title={inventory.name || t("inventory.untitledInventory")}
                subtitle={inventory.description || undefined}
                section="inventory"
                directActions={directActions}
                menuActions={menuActions}
                menuButtonTestId="inventory-header-menu-toggle"
            />

            <SearchInputRow
                open={searchOpen}
                query={searchQuery}
                onQueryChange={setSearchQuery}
                onClose={handleToggleSearch}
                placeholder={t("inventory.searchPlaceholder")}
                testIdPrefix="inventory-search"
            />

            <InventoryContainer
                ref={scrollContainerRef}
                householdId={householdId}
                inventoryId={inventoryId}
                editingItem={editingItem}
                onEdit={setEditingItem}
                sortMode={sortMode}
                searchQuery={searchQuery}
            />

            <InventoryFooter
                editingItem={editingItem}
                existingItems={items}
                onAddItem={(text, quantity, expiryDate) =>
                    handleAddItem({
                        text,
                        quantity,
                        expiryDate: expiryDate ?? null,
                    })
                }
                onUpdateItem={(text, quantity, expiryDate) =>
                    // The edit composer is authoritative for quantity: a non-null value sets it,
                    // an empty one clears it (clearQuantity). Text is always sent, so this never
                    // collides with the domain's null=preserve semantics for the other fields.
                    handleUpdateItem({
                        text,
                        quantity,
                        clearQuantity: quantity === null,
                        expiryDate: expiryDate ?? null,
                    })
                }
                onCancelEdit={() => setEditingItem(null)}
                onUncheckExisting={() => {}}
                isLoading={createMutation.isPending || updateMutation.isPending}
                onScrollToLastUnchecked={scrollToLastUncheckedItem}
            />
        </Box>
    );
};
