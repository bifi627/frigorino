import dayGridPlugin from "@fullcalendar/daygrid";
import FullCalendar from "@fullcalendar/react";
import type { EventClickArg, EventContentArg } from "@fullcalendar/core";
import { Search, Tune } from "@mui/icons-material";
import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Container,
    Typography,
    useTheme,
} from "@mui/material";
import { useNavigate } from "@tanstack/react-router";
import { useCallback, useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { ConfirmDialog } from "../../../../components/dialogs/ConfirmDialog";
import { PageHeadActionBar } from "../../../../components/shared/PageHeadActionBar";
import { SearchInputRow } from "../../../../components/shared/SearchInputRow";
import { useCurrentHousehold } from "../../../me/activeHousehold/useCurrentHousehold";
import { pageContainerSx } from "../../../../theme";
import {
    formatLocalDate,
    todayIsoDate,
    type ExpiryLevel,
} from "../../../../utils/dateUtils";
import {
    buildExpiryEvents,
    type ExpiryEventProps,
} from "../expiryCalendarEvents";
import { matchesQuery } from "../../../../utils/searchUtils";
import { useExpiryCalendar } from "../useExpiryCalendar";
import { useCalendarViewSettings } from "../calendarViewSettings";
import { calendarLevelColor } from "../calendarColors";
import { useQueryClient } from "@tanstack/react-query";
import { getExpiryCalendarQueryKey } from "../../../../lib/api/@tanstack/react-query.gen";
import type { QuantityDto } from "../../../../lib/api";
import { useDeleteInventoryItem } from "../../items/useDeleteInventoryItem";
import { useUpdateInventoryItem } from "../../items/useUpdateInventoryItem";
import { CalendarItemActionBar } from "../components/CalendarItemActionBar";
import { CalendarLevelToggles } from "../components/CalendarLevelToggles";
import { CalendarSettingsSheet } from "../components/CalendarSettingsSheet";
import "../expiryCalendar.css";

export const ExpiryCalendarPage = () => {
    const theme = useTheme();
    const navigate = useNavigate();
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;

    const {
        data: items,
        isLoading,
        error,
    } = useExpiryCalendar(householdId, householdId > 0);

    // Single-select focus: the tapped item is highlighted, the rest dim. null = nothing selected.
    const [selectedId, setSelectedId] = useState<number | null>(null);

    const [settingsOpen, setSettingsOpen] = useState(false);

    const [searchOpen, setSearchOpen] = useState(false);
    const [searchQuery, setSearchQuery] = useState("");

    const handleToggleSearch = useCallback(() => {
        setSearchOpen((prev) => {
            if (prev) {
                setSearchQuery("");
            }
            return !prev;
        });
    }, []);

    // Edit mode for the selected item. Selection (selectedId) can be active without editing.
    const [editing, setEditing] = useState(false);

    // Delete confirmation for the selected item.
    const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);

    const queryClient = useQueryClient();
    const updateMutation = useUpdateInventoryItem();
    const deleteMutation = useDeleteInventoryItem();

    // The full selected item (incl. quantity, which isn't in the FullCalendar event props).
    const selectedItem = items?.find((item) => item.id === selectedId) ?? null;

    // If the selected item leaves the calendar (e.g. its expiry was cleared on save, so it no
    // longer matches the calendar query, or it was deleted elsewhere), drop the stale selection.
    // Otherwise selectedId would point at an absent item — dimming every remaining bar with none
    // highlighted and no action bar to recover from.
    useEffect(() => {
        const itemGone =
            selectedId !== null &&
            !isLoading &&
            items !== undefined &&
            !items.some((item) => item.id === selectedId);
        if (itemGone) {
            setSelectedId(null);
            setEditing(false);
        }
    }, [selectedId, items, isLoading]);

    const windowDays = useCalendarViewSettings((s) => s.windowDays);
    const fullRunway = useCalendarViewSettings((s) => s.fullRunway);
    const levels = useCalendarViewSettings((s) => s.levels);

    const levelColor = useMemo(
        () => (level: ExpiryLevel) => calendarLevelColor(theme, level),
        [theme],
    );

    const trimmedSearch = searchQuery.trim();
    const searchedItems = useMemo(() => {
        if (trimmedSearch.length === 0) {
            return items ?? [];
        }
        return (items ?? []).filter((item) =>
            matchesQuery(item.text, trimmedSearch),
        );
    }, [items, trimmedSearch]);

    const events = useMemo(
        () =>
            buildExpiryEvents(searchedItems, todayIsoDate(), levelColor, {
                windowDays,
                fullRunway,
                levels,
            }),
        [searchedItems, levelColor, windowDays, fullRunway, levels],
    );

    // Classes drive the focus-select visuals (see expiryCalendar.css).
    const eventClassNames = (arg: EventContentArg): string[] => {
        const props = arg.event.extendedProps as ExpiryEventProps;
        const classes: string[] = [];
        if (props.activeToday) {
            classes.push("cal-active");
        }
        if (selectedId !== null) {
            classes.push(
                selectedId === props.itemId ? "cal-selected" : "cal-dimmed",
            );
        }
        return classes;
    };

    // Stamp the expiry date on the bar tail (isEnd) and a continuation marker on wrapped
    // segments (!isStart) so a long Mon->Sun span is identifiable on any week-row it touches.
    // data-selected is the test hook for the focus-select assertion.
    const renderEventContent = (arg: EventContentArg) => {
        const props = arg.event.extendedProps as ExpiryEventProps;

        // Narrow bars (expired markers, near-expiry items) can't fit "name · inventory · date":
        // render the name only, ellipsized; selecting one surfaces the rest in the action bar.
        if (props.compact) {
            return (
                <Box
                    data-testid={`cal-event-${arg.event.title}`}
                    data-selected={
                        selectedId === props.itemId ? "true" : "false"
                    }
                    sx={{
                        overflow: "hidden",
                        whiteSpace: "nowrap",
                        textOverflow: "ellipsis",
                        fontSize: "0.7rem",
                        fontWeight: 600,
                        px: 0.25,
                    }}
                >
                    {arg.event.title}
                </Box>
            );
        }

        const isSelected = selectedId === props.itemId;
        return (
            <Box
                data-testid={`cal-event-${arg.event.title}`}
                data-selected={isSelected ? "true" : "false"}
                sx={{
                    display: "flex",
                    alignItems: "center",
                    gap: 0.5,
                    overflow: "hidden",
                    whiteSpace: "nowrap",
                    fontSize: "0.7rem",
                    px: 0.25,
                }}
            >
                {!arg.isStart && <span>↩</span>}
                <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>
                    {arg.event.title}
                </span>
                <span style={{ opacity: 0.8 }}>· {props.inventoryName}</span>
                {arg.isEnd && (
                    <span style={{ marginLeft: "auto", fontWeight: 600 }}>
                        {formatLocalDate(props.expiryDate)}
                    </span>
                )}
            </Box>
        );
    };

    const handleSave = async (
        text: string,
        quantity: QuantityDto | null,
        expiryDate: string | null,
    ) => {
        if (!selectedItem) {
            return;
        }
        await updateMutation.mutateAsync({
            path: {
                householdId,
                inventoryId: selectedItem.inventoryId,
                itemId: selectedItem.id,
            },
            // Mirrors InventoryViewPage.handleUpdateItem: text always sent; clearQuantity on
            // an empty quantity; expiryDate is write-through (null clears).
            body: {
                text,
                quantity,
                clearQuantity: quantity === null,
                expiryDate,
            },
        });
        // The shared hook invalidates the inventory-items query. The calendar reads a separate
        // query, so invalidate it here too — an expiry change then moves the bar immediately.
        await queryClient.invalidateQueries({
            queryKey: getExpiryCalendarQueryKey({ path: { householdId } }),
        });
        // Leave edit mode but keep the item selected/highlighted.
        setEditing(false);
    };

    // Delete the selected item once confirmed. The shared hook surfaces the undo toast and keeps
    // both the inventory-items and expiry-calendar queries in sync. Drop the selection immediately
    // so the action bar slides out without waiting for the calendar refetch.
    const handleConfirmDelete = () => {
        if (!selectedItem) {
            return;
        }
        deleteMutation.mutate({
            path: {
                householdId,
                inventoryId: selectedItem.inventoryId,
                itemId: selectedItem.id,
            },
        });
        setConfirmDeleteOpen(false);
        setEditing(false);
        setSelectedId(null);
    };

    const handleEventClick = (info: EventClickArg) => {
        // Stop the wrapper's clear-on-empty handler from firing for this same click.
        info.jsEvent.stopPropagation();
        const props = info.event.extendedProps as ExpiryEventProps;
        // Every bar (compact or wide) now selects + highlights. Switching items leaves edit mode.
        setEditing(false);
        setSelectedId((prev) => (prev === props.itemId ? null : props.itemId));
    };

    if (!householdId) {
        return (
            <Container maxWidth="sm" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("inventory.selectHouseholdToViewInventories")}
                    <Button
                        onClick={() => navigate({ to: "/" })}
                        sx={{ mt: 1, display: "block" }}
                    >
                        {t("common.goBackToDashboard")}
                    </Button>
                </Alert>
            </Container>
        );
    }

    return (
        <>
            <PageHeadActionBar
                title={t("inventory.calendar.title")}
                section="inventory"
                directActions={[
                    {
                        icon: <Tune />,
                        onClick: () => setSettingsOpen(true),
                        testId: "calendar-settings-button",
                    },
                    {
                        icon: <Search />,
                        onClick: handleToggleSearch,
                        testId: "calendar-search-button",
                    },
                ]}
                menuActions={[]}
            />
            <SearchInputRow
                open={searchOpen}
                query={searchQuery}
                onQueryChange={setSearchQuery}
                onClose={handleToggleSearch}
                placeholder={t("inventory.searchPlaceholder")}
                testIdPrefix="calendar-search"
            />
            <Container maxWidth="sm" sx={pageContainerSx}>
                <CalendarLevelToggles />
                {isLoading && (
                    <Box
                        sx={{
                            display: "flex",
                            justifyContent: "center",
                            py: 4,
                        }}
                    >
                        <CircularProgress />
                    </Box>
                )}
                {error && (
                    <Alert severity="error" sx={{ mb: 3 }}>
                        {t("inventory.calendar.failedToLoad")}
                    </Alert>
                )}
                {!isLoading &&
                    !error &&
                    events.length === 0 &&
                    (items?.length ?? 0) > 0 && (
                        <Typography
                            variant="body2"
                            sx={{
                                color: "text.secondary",
                                textAlign: "center",
                                py: 4,
                            }}
                            data-testid="calendar-empty-filtered"
                        >
                            {t("inventory.calendar.emptyFiltered")}
                        </Typography>
                    )}
                {!isLoading &&
                    !error &&
                    events.length === 0 &&
                    (items?.length ?? 0) === 0 && (
                        <Typography
                            variant="body2"
                            sx={{
                                color: "text.secondary",
                                textAlign: "center",
                                py: 4,
                            }}
                            data-testid="calendar-empty"
                        >
                            {t("inventory.calendar.empty")}
                        </Typography>
                    )}
                {!isLoading && !error && events.length > 0 && (
                    <Box
                        className="expiry-calendar"
                        data-testid="expiry-calendar"
                        onClick={() => {
                            // While editing the user must Save or Cancel; a stray grid tap
                            // shouldn't discard the edit by clearing the selection.
                            if (!editing) {
                                setSelectedId(null);
                            }
                        }}
                        sx={{
                            "--fc-border-color": theme.palette.divider,
                            "--fc-page-bg-color":
                                theme.palette.background.paper,
                            "--fc-neutral-bg-color": theme.palette.action.hover,
                            "--fc-today-bg-color":
                                theme.palette.action.selected,
                            "& .fc": { fontSize: "0.8rem" },
                        }}
                    >
                        <FullCalendar
                            plugins={[dayGridPlugin]}
                            initialView="dayGridMonth"
                            height="auto"
                            firstDay={1}
                            dayMaxEvents={false}
                            dayMaxEventRows={false}
                            headerToolbar={{
                                left: "prev,next today",
                                center: "title",
                                right: "",
                            }}
                            events={events}
                            eventContent={renderEventContent}
                            eventClassNames={eventClassNames}
                            eventClick={handleEventClick}
                        />
                    </Box>
                )}
            </Container>
            <CalendarSettingsSheet
                open={settingsOpen}
                onClose={() => setSettingsOpen(false)}
            />
            <CalendarItemActionBar
                item={selectedItem}
                editing={editing}
                onEdit={() => setEditing(true)}
                onDelete={() => setConfirmDeleteOpen(true)}
                onCancelEdit={() => setEditing(false)}
                onSave={handleSave}
                isSaving={updateMutation.isPending}
            />
            <ConfirmDialog
                open={confirmDeleteOpen && selectedItem !== null}
                onClose={() => setConfirmDeleteOpen(false)}
                onConfirm={handleConfirmDelete}
                title={t("common.delete")}
                description={
                    <>
                        {t("common.confirmDelete")} "{selectedItem?.text}"?
                    </>
                }
                confirmLabel={t("common.delete")}
                confirmLabelPending={t("common.deleting")}
                cancelLabel={t("common.cancel")}
                isPending={deleteMutation.isPending}
                confirmTestId="calendar-delete-confirm"
                cancelTestId="calendar-delete-cancel"
                maxWidth="xs"
            />
        </>
    );
};
