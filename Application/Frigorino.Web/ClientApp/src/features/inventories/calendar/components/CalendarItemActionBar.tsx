import { AddShoppingCart, Delete, Edit } from "@mui/icons-material";
import {
    Box,
    Button,
    IconButton,
    Paper,
    Slide,
    Stack,
    Typography,
} from "@mui/material";
import { useTranslation } from "react-i18next";
import {
    Composer,
    draftToQuantity,
    expiryFeature,
    formatQuantity,
    quantityComposerFeature,
    quantityToDraft,
    type Completion,
} from "../../../../components/composer";
import type {
    ExpiryCalendarItemResponse,
    QuantityDto,
} from "../../../../lib/api";
import { formatLocalDate } from "../../../../utils/dateUtils";

const features = [quantityComposerFeature, expiryFeature] as const;

interface CalendarItemActionBarProps {
    // The selected item, or null when nothing is selected (bar slides out).
    item: ExpiryCalendarItemResponse | null;
    editing: boolean;
    onEdit: () => void;
    onDelete: () => void;
    // Optional re-order action — adds the item to a shopping list. Omitted when the household
    // has no lists, in which case the icon button is not rendered.
    onAddToList?: () => void;
    onCancelEdit: () => void;
    onSave: (
        text: string,
        quantity: QuantityDto | null,
        expiryDate: string | null,
    ) => void;
    isSaving: boolean;
}

// Bottom action bar for the expiry calendar. Slim by default (name · inventory · date · Edit);
// expands in place to host the shared Composer when Edit is tapped. Non-modal (a fixed Paper,
// not a Drawer) so the calendar grid stays tappable for switching/clearing the selection.
export const CalendarItemActionBar = ({
    item,
    editing,
    onEdit,
    onDelete,
    onAddToList,
    onCancelEdit,
    onSave,
    isSaving,
}: CalendarItemActionBarProps) => {
    const { t } = useTranslation();

    const initialDraft = item
        ? {
              text: item.text,
              values: {
                  quantity: quantityToDraft(item.quantity),
                  expiry: item.expiryDate ?? null,
              },
          }
        : undefined;

    // info line: inventory · quantity · date — quantity omitted when the item has none.
    const metaLine = item
        ? [
              item.inventoryName,
              item.quantity ? formatQuantity(t, item.quantity) : null,
              formatLocalDate(item.expiryDate),
          ]
              .filter(Boolean)
              .join(" · ")
        : "";

    const handleComplete = (r: Completion<typeof features>) => {
        if (r.kind !== "text") {
            return;
        }
        const quantity = draftToQuantity(r.quantity);
        onSave(r.text, quantity, r.expiry ?? null);
    };

    return (
        <Slide direction="up" in={Boolean(item)} mountOnEnter unmountOnExit>
            <Paper
                elevation={8}
                data-testid="calendar-item-action-bar"
                // Clicks inside the bar must not bubble to the page's clear-on-empty handler.
                onClick={(e) => e.stopPropagation()}
                sx={{
                    position: "fixed",
                    left: 0,
                    right: 0,
                    bottom: 0,
                    zIndex: (theme) => theme.zIndex.drawer,
                    borderTopLeftRadius: 16,
                    borderTopRightRadius: 16,
                    px: 2,
                    py: 1.5,
                    maxWidth: 600,
                    mx: "auto",
                }}
            >
                {item && !editing && (
                    <Stack
                        direction="row"
                        spacing={1}
                        sx={{ alignItems: "center" }}
                    >
                        <Box sx={{ minWidth: 0, flex: 1 }}>
                            <Typography
                                variant="subtitle1"
                                noWrap
                                data-testid="calendar-action-bar-title"
                            >
                                {item.text}
                            </Typography>
                            <Typography
                                variant="body2"
                                color="text.secondary"
                                noWrap
                            >
                                {metaLine}
                            </Typography>
                        </Box>
                        {onAddToList && (
                            <IconButton
                                size="small"
                                color="primary"
                                onClick={onAddToList}
                                aria-label={t("reorder.addToList")}
                                data-testid="calendar-action-bar-add-to-list"
                            >
                                <AddShoppingCart />
                            </IconButton>
                        )}
                        <IconButton
                            size="small"
                            color="error"
                            onClick={onDelete}
                            aria-label={t("common.delete")}
                            data-testid="calendar-action-bar-delete"
                        >
                            <Delete />
                        </IconButton>
                        <Button
                            variant="contained"
                            size="small"
                            startIcon={<Edit />}
                            onClick={onEdit}
                            data-testid="calendar-action-bar-edit"
                        >
                            {t("common.edit")}
                        </Button>
                    </Stack>
                )}
                {item && editing && (
                    <Box data-testid="calendar-action-bar-composer">
                        <Composer
                            key={item.id}
                            features={features}
                            disabled={isSaving}
                            editing={{ active: true, onCancel: onCancelEdit }}
                            initialDraft={initialDraft}
                            onComplete={handleComplete}
                        />
                    </Box>
                )}
            </Paper>
        </Slide>
    );
};
