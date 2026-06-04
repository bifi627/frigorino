import dayGridPlugin from "@fullcalendar/daygrid";
import FullCalendar from "@fullcalendar/react";
import type { EventClickArg, EventContentArg } from "@fullcalendar/core";
import { Tune } from "@mui/icons-material";
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
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { PageHeadActionBar } from "../../../../components/shared/PageHeadActionBar";
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
import { useExpiryCalendar } from "../useExpiryCalendar";
import { useCalendarViewSettings } from "../calendarViewSettings";
import { calendarLevelColor } from "../calendarColors";
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

    const windowDays = useCalendarViewSettings((s) => s.windowDays);
    const fullRunway = useCalendarViewSettings((s) => s.fullRunway);
    const levels = useCalendarViewSettings((s) => s.levels);

    const levelColor = useMemo(
        () => (level: ExpiryLevel) => calendarLevelColor(theme, level),
        [theme],
    );

    const events = useMemo(
        () =>
            buildExpiryEvents(items ?? [], todayIsoDate(), levelColor, {
                windowDays,
                fullRunway,
                levels,
            }),
        [items, levelColor, windowDays, fullRunway, levels],
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

    const handleEventClick = (info: EventClickArg) => {
        // Stop the wrapper's clear-on-empty handler from firing for this same click.
        info.jsEvent.stopPropagation();
        const props = info.event.extendedProps as ExpiryEventProps;
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
                ]}
                menuActions={[]}
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
                        onClick={() => setSelectedId(null)}
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
        </>
    );
};
