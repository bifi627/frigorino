import { Business, Group } from "@mui/icons-material";
import { Box, Chip, Paper, Stack, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { HouseholdRole } from "../../../lib/api";
import { roleColors, useRoleLabels } from "../householdRole";

interface HouseholdSummaryCardProps {
    householdName: string;
    memberCount: number;
    userRole: HouseholdRole;
}

export const HouseholdSummaryCard = ({
    householdName,
    memberCount,
    userRole,
}: HouseholdSummaryCardProps) => {
    const { t } = useTranslation();
    const roleLabels = useRoleLabels();

    return (
        <Paper
            variant="outlined"
            sx={{
                display: "flex",
                alignItems: "center",
                gap: { xs: 1.5, sm: 2 },
                p: { xs: 2, sm: 3 },
            }}
        >
            <Box
                sx={{
                    p: 1,
                    borderRadius: 1,
                    bgcolor: "primary.main",
                    color: "primary.contrastText",
                    display: "flex",
                    alignItems: "center",
                }}
            >
                <Business fontSize="small" />
            </Box>

            <Box sx={{ flexGrow: 1, minWidth: 0 }}>
                <Typography
                    variant="h6"
                    sx={{
                        fontWeight: 600,
                        mb: 0.5,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                    }}
                >
                    {householdName}
                </Typography>

                <Stack
                    direction="row"
                    spacing={1}
                    alignItems="center"
                    sx={{ flexWrap: "wrap", gap: 0.5 }}
                >
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 0.5,
                        }}
                    >
                        <Group
                            sx={{ fontSize: 14, color: "text.secondary" }}
                        />
                        <Typography variant="caption" color="text.secondary">
                            {memberCount} {t("household.members")}
                        </Typography>
                    </Box>

                    <Chip
                        label={roleLabels[userRole]}
                        size="small"
                        color={roleColors[userRole]}
                    />
                </Stack>
            </Box>
        </Paper>
    );
};
