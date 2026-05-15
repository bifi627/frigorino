import { ArrowBack, DragHandle, Edit } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    Typography,
} from "@mui/material";
import { useParams, useRouter } from "@tanstack/react-router";
import { useCallback, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ListContainer } from "../../../components/list/ListContainer";
import { ListFooter } from "../../../components/list/ListFooter";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import {
    useCreateListItem,
    useListItems,
    useToggleListItemStatus,
    useUpdateListItem,
    type ListItemDto,
} from "../../../hooks/useListItemQueries";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { useList } from "../useList";

export const ListViewPage = () => {
    const router = useRouter();
    const { listId } = useParams({ from: "/lists/$listId/view" });
    const { t } = useTranslation();
    const [editingItem, setEditingItem] = useState<ListItemDto | null>(null);
    const [showDragHandles, setShowDragHandles] = useState(false);
    const scrollContainerRef = useRef<HTMLDivElement>(null);

    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;
    const listIdNum = parseInt(listId);

    const {
        data: list,
        isLoading,
        error,
    } = useList(householdId, listIdNum, householdId > 0);

    const { data: items = [] } = useListItems(householdId, listIdNum);

    const createMutation = useCreateListItem();
    const updateMutation = useUpdateListItem();
    const toggleMutation = useToggleListItemStatus();

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
        router.navigate({ to: `/lists/${listId}/edit` });
    }, [router, listId]);

    const handleToggleDragHandles = useCallback(() => {
        setShowDragHandles((prev) => !prev);
    }, []);

    const handleAddItem = useCallback(
        (data: string, quantity?: string) => {
            if (!householdId) return;
            createMutation.mutate({
                householdId,
                listId: listIdNum,
                data: { text: data, quantity: quantity || undefined },
            });
        },
        [createMutation, householdId, listIdNum],
    );

    const handleUpdateItem = useCallback(
        (data: string, quantity?: string) => {
            if (editingItem?.id && householdId) {
                updateMutation.mutate({
                    householdId,
                    listId: listIdNum,
                    itemId: editingItem.id,
                    data: { text: data, quantity: quantity || undefined },
                });
                setEditingItem(null);
            }
        },
        [editingItem?.id, updateMutation, householdId, listIdNum],
    );

    const handleCancelEdit = useCallback(() => setEditingItem(null), []);

    const handleUncheckExisting = useCallback(
        (itemId: number) => {
            if (!householdId) return;
            toggleMutation.mutate({
                householdId,
                listId: listIdNum,
                itemId,
            });
        },
        [toggleMutation, householdId, listIdNum],
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

    if (isLoading) {
        return (
            <Container maxWidth="sm" sx={{ py: 4, textAlign: "center" }}>
                <CircularProgress />
                <Typography variant="body2" sx={{ mt: 2 }}>
                    {t("lists.loadingList")}
                </Typography>
            </Container>
        );
    }

    if (error || !list) {
        return (
            <Container maxWidth="sm" sx={{ py: 4 }}>
                <Alert severity="error" sx={{ mb: 2 }}>
                    {t("lists.failedToLoadList")}
                </Alert>
                <Button
                    variant="outlined"
                    startIcon={<ArrowBack />}
                    onClick={() => window.history.back()}
                >
                    {t("lists.backToLists")}
                </Button>
            </Container>
        );
    }

    const directActions = [
        { icon: <Edit />, onClick: handleEdit },
        { icon: <DragHandle />, onClick: handleToggleDragHandles },
    ];
    const menuActions: HeadNavigationAction[] = [];

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
                title={list.name || t("lists.untitledList")}
                subtitle={list.description || undefined}
                directActions={directActions}
                menuActions={menuActions}
            />

            <ListContainer
                ref={scrollContainerRef}
                householdId={householdId}
                listId={listIdNum}
                editingItem={editingItem}
                onEdit={setEditingItem}
                showDragHandles={showDragHandles}
            />

            <ListFooter
                editingItem={editingItem}
                existingItems={items}
                onAddItem={handleAddItem}
                onUpdateItem={handleUpdateItem}
                onCancelEdit={handleCancelEdit}
                onUncheckExisting={handleUncheckExisting}
                isLoading={createMutation.isPending || updateMutation.isPending}
                onScrollToLastUnchecked={scrollToLastUncheckedItem}
            />
        </Box>
    );
};
