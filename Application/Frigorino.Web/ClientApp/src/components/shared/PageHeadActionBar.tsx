import { ArrowBack, MoreVert } from "@mui/icons-material";
import {
    Box,
    type Breakpoint,
    Container,
    IconButton,
    ListItemIcon,
    ListItemText,
    Menu,
    MenuItem,
    Typography,
} from "@mui/material";
import { useRouter } from "@tanstack/react-router";
import { memo, useCallback, useState } from "react";
import { sectionIcons } from "../../common/sections";
import {
    featureContentPx,
    neutralActionColor,
    sectionColors,
    tintedActionButtonSx,
    type SectionKey,
} from "../../theme";

export type HeadNavigationAction = {
    text?: string;
    secondaryText?: string;
    icon?: React.ReactNode;
    onClick: () => void;
    testId?: string;
    // "error" renders the menu item in the destructive (red) color, matching the
    // overview cards' delete styling so destructive actions look consistent everywhere.
    color?: "error";
};

export interface HeadNavigationProps {
    title: string;
    subtitle?: string;
    menuActions: HeadNavigationAction[];
    directActions: HeadNavigationAction[];
    maxWidth?: Breakpoint;
    menuButtonTestId?: string;
    // When set, shows the section's identity icon (section-colored glyph on a
    // neutral surface) before the title — the same wayfinding cue used on the
    // dashboard, continued into the feature.
    section?: SectionKey;
}

export const PageHeadActionBar = memo(
    ({
        title,
        subtitle,
        menuActions,
        directActions,
        maxWidth = "sm",
        menuButtonTestId,
        section,
    }: HeadNavigationProps) => {
        const router = useRouter();
        const SectionIcon = section ? sectionIcons[section] : null;
        const sectionColor = section ? sectionColors[section] : undefined;
        // The primary direct action (edit) takes the page's section color so it
        // matches the identity glyph; falls back to the brand green when a page
        // hasn't declared a section.
        const identityColor = sectionColor ?? "#43A047";
        const [menuAnchorEl, setMenuAnchorEl] = useState<null | HTMLElement>(
            null,
        );

        const handleBack = useCallback(() => {
            router.history.back();
        }, [router]);

        const handleMenuOpen = useCallback(
            (event: React.MouseEvent<HTMLElement>) => {
                setMenuAnchorEl(event.currentTarget);
            },
            [],
        );

        const handleMenuClose = useCallback(() => {
            setMenuAnchorEl(null);
        }, []);

        const handleMenuAction = useCallback(
            (action: HeadNavigationAction) => {
                action.onClick();
                handleMenuClose();
            },
            [handleMenuClose],
        );

        return (
            <>
                <Container
                    maxWidth={maxWidth}
                    sx={{ px: featureContentPx, py: 1.5, flexShrink: 0 }}
                >
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 2,
                        }}
                    >
                        <IconButton onClick={handleBack} sx={{ p: 1 }}>
                            <ArrowBack />
                        </IconButton>
                        {SectionIcon && (
                            <Box
                                sx={{
                                    p: 1,
                                    borderRadius: 2,
                                    bgcolor: "action.hover",
                                    color: sectionColor,
                                    display: "flex",
                                    alignItems: "center",
                                    flexShrink: 0,
                                }}
                            >
                                <SectionIcon />
                            </Box>
                        )}
                        <Box sx={{ flex: 1 }}>
                            <Typography
                                variant="h5"
                                component="h1"
                                sx={{ fontWeight: 600, mb: 0.5 }}
                            >
                                {title}
                            </Typography>
                            {subtitle && (
                                <Typography
                                    variant="body2"
                                    sx={{
                                        color: "text.secondary",
                                        lineHeight: 1.4,
                                    }}
                                >
                                    {subtitle}
                                </Typography>
                            )}
                        </Box>
                        <Box sx={{ display: "flex", gap: 1, ml: "auto" }}>
                            {directActions.map((action, index) => (
                                <IconButton
                                    key={index}
                                    onClick={action.onClick}
                                    data-testid={action.testId}
                                    sx={tintedActionButtonSx(
                                        index === 0
                                            ? identityColor
                                            : neutralActionColor,
                                    )}
                                >
                                    {action.icon}
                                </IconButton>
                            ))}
                            {menuActions.length > 0 && (
                                <IconButton
                                    onClick={handleMenuOpen}
                                    data-testid={menuButtonTestId}
                                    sx={tintedActionButtonSx(
                                        neutralActionColor,
                                    )}
                                >
                                    <MoreVert />
                                </IconButton>
                            )}
                        </Box>
                    </Box>
                </Container>
                {menuActions.length > 0 && (
                    <Menu
                        anchorEl={menuAnchorEl}
                        open={Boolean(menuAnchorEl)}
                        onClose={handleMenuClose}
                        anchorOrigin={{
                            vertical: "bottom",
                            horizontal: "right",
                        }}
                        transformOrigin={{
                            vertical: "top",
                            horizontal: "right",
                        }}
                    >
                        {menuActions.map((action, index) => (
                            <MenuItem
                                key={index}
                                onClick={() => handleMenuAction(action)}
                                data-testid={action.testId}
                                sx={
                                    action.color === "error"
                                        ? { color: "error.main" }
                                        : undefined
                                }
                            >
                                {action.icon && (
                                    <ListItemIcon>{action.icon}</ListItemIcon>
                                )}
                                <ListItemText
                                    primary={action.text}
                                    secondary={action.secondaryText}
                                />
                            </MenuItem>
                        ))}
                    </Menu>
                )}
            </>
        );
    },
);

PageHeadActionBar.displayName = "HeadNavigation";
