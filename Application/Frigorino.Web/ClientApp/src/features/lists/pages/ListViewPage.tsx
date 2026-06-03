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
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import type { ListItemResponse, QuantityDto } from "../../../lib/api";
import { useCurrentHousehold } from "../../me/activeHousehold/useCurrentHousehold";
import { ListContainer } from "../items/components/ListContainer";
import { ListFooter } from "../items/components/ListFooter";
import { MediaCaptionSheet } from "../items/components/MediaCaptionSheet";
import { MediaPreviewSheet } from "../items/components/MediaPreviewSheet";
import { useCreateListItem } from "../items/useCreateListItem";
import { useCreateMediaItem } from "../items/useCreateMediaItem";
import { useListItems } from "../items/useListItems";
import { useToggleListItemStatus } from "../items/useToggleListItemStatus";
import { useUpdateListItem } from "../items/useUpdateListItem";
import { useExtractionPoll } from "../items/useExtractionPoll";
import { useList } from "../useList";
import { PromoteBar } from "../promote/PromoteBar";

export const ListViewPage = () => {
    const router = useRouter();
    const { listId } = useParams({ from: "/lists/$listId/view" });
    const { t } = useTranslation();
    const [editingItem, setEditingItem] = useState<ListItemResponse | null>(
        null,
    );
    const [showDragHandles, setShowDragHandles] = useState(false);
    // True when edit mode was opened via the quantity chip — the composer then starts
    // with the quantity panel expanded.
    const [editOpenQuantity, setEditOpenQuantity] = useState(false);
    // True when edit mode was opened via tapping the comment — the composer then starts
    // with the comment panel expanded.
    const [editOpenComment, setEditOpenComment] = useState(false);
    const [pendingExtraction, setPendingExtraction] = useState<{
        id: number;
        extractionPending: boolean;
    } | null>(null);
    const [pendingFile, setPendingFile] = useState<File | null>(null);
    // Media items have no text/quantity — editing one opens the caption sheet, not the footer composer.
    const [editingMediaItem, setEditingMediaItem] =
        useState<ListItemResponse | null>(null);
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
    const createMediaMutation = useCreateMediaItem();

    const { isExtracting, extractingItemId } = useExtractionPoll(
        householdId,
        listIdNum,
        pendingExtraction?.id ?? null,
        pendingExtraction?.extractionPending ?? false,
    );

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
        async (data: string, comment: string | null) => {
            if (!householdId) return;
            try {
                const created = await createMutation.mutateAsync({
                    path: { householdId, listId: listIdNum },
                    body: { text: data, comment },
                });
                // Only the latest add is polled for extraction; rapid successive adds
                // replace this, so just the last item shows the extracting spinner (v1).
                // The server's create response is the single authority on whether an async
                // extraction was enqueued — no client-side digit gate to drift from it.
                setPendingExtraction({
                    id: created.id,
                    extractionPending: created.extractionPending,
                });
            } catch {
                // createMutation.onError rolls back the optimistic item; nothing to do here.
            }
        },
        [createMutation, householdId, listIdNum],
    );

    const handleUpdateItem = useCallback(
        (
            data: string,
            quantity: QuantityDto | null,
            comment: string | null,
        ) => {
            if (editingItem?.id && householdId) {
                updateMutation.mutate({
                    path: {
                        householdId,
                        listId: listIdNum,
                        itemId: editingItem.id,
                    },
                    body: {
                        text: data,
                        quantity,
                        clearQuantity: quantity === null,
                        status: null,
                        comment,
                    },
                });
                setEditOpenQuantity(false);
                setEditOpenComment(false);
                setEditingItem(null);
            }
        },
        [editingItem?.id, updateMutation, householdId, listIdNum],
    );

    const handleEditItem = useCallback((item: ListItemResponse) => {
        // Image/Document items only expose a caption — route them to the caption sheet.
        if (item.type !== "Text") {
            setEditingMediaItem(item);
            return;
        }
        setEditOpenQuantity(false);
        setEditOpenComment(false);
        setEditingItem(item);
    }, []);

    const handleSaveCaption = useCallback(
        (caption: string) => {
            if (!householdId || !editingMediaItem) return;
            // Comment-only update: "" clears the caption, non-empty sets it. text/quantity stay null
            // so the server keeps the media item's clean-separation invariant.
            updateMutation.mutate({
                path: {
                    householdId,
                    listId: listIdNum,
                    itemId: editingMediaItem.id,
                },
                body: {
                    text: null,
                    quantity: null,
                    clearQuantity: false,
                    status: null,
                    comment: caption,
                },
            });
            setEditingMediaItem(null);
        },
        [householdId, listIdNum, editingMediaItem, updateMutation],
    );

    const handleEditQuantity = useCallback((item: ListItemResponse) => {
        setEditOpenComment(false);
        setEditOpenQuantity(true);
        setEditingItem(item);
    }, []);

    const handleEditComment = useCallback((item: ListItemResponse) => {
        setEditOpenQuantity(false);
        setEditOpenComment(true);
        setEditingItem(item);
    }, []);

    const handleCancelEdit = useCallback(() => {
        setEditOpenQuantity(false);
        setEditOpenComment(false);
        setEditingItem(null);
    }, []);

    const handleAttachFile = useCallback((file: File) => {
        setPendingFile(file);
    }, []);

    const handleSendMedia = useCallback(
        async (caption: string | null) => {
            if (!householdId || !pendingFile) return;
            try {
                await createMediaMutation.mutateAsync({
                    path: { householdId, listId: listIdNum },
                    body: {
                        file: pendingFile,
                        type: "Image",
                        caption: caption ?? undefined,
                    },
                });
                setPendingFile(null);
                scrollToLastUncheckedItem();
            } catch {
                // Mutation surfaces the error; keep the sheet open so the user can retry/cancel.
            }
        },
        [
            householdId,
            listIdNum,
            pendingFile,
            createMediaMutation,
            scrollToLastUncheckedItem,
        ],
    );

    const handleUncheckExisting = useCallback(
        (itemId: number) => {
            if (!householdId) return;
            toggleMutation.mutate({
                path: { householdId, listId: listIdNum, itemId },
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
        {
            icon: <DragHandle />,
            onClick: handleToggleDragHandles,
            testId: "list-toggle-drag-handles",
        },
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
                section="lists"
                directActions={directActions}
                menuActions={menuActions}
            />

            <PromoteBar householdId={householdId} listId={listIdNum} />

            <ListContainer
                ref={scrollContainerRef}
                householdId={householdId}
                listId={listIdNum}
                editingItem={editingItem}
                onEdit={handleEditItem}
                onEditQuantity={handleEditQuantity}
                onEditComment={handleEditComment}
                showDragHandles={showDragHandles}
                isExtracting={isExtracting}
                extractingItemId={extractingItemId}
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
                onAttachFile={handleAttachFile}
                openQuantityPanel={editOpenQuantity}
                openCommentPanel={editOpenComment}
            />

            <MediaPreviewSheet
                file={pendingFile}
                isUploading={createMediaMutation.isPending}
                onSend={handleSendMedia}
                onClose={() => setPendingFile(null)}
            />

            <MediaCaptionSheet
                householdId={householdId}
                item={editingMediaItem}
                isSaving={updateMutation.isPending}
                onSave={handleSaveCaption}
                onClose={() => setEditingMediaItem(null)}
            />
        </Box>
    );
};
