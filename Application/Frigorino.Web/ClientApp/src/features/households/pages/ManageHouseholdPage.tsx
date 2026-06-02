import { Delete } from "@mui/icons-material";
import { Alert, Box, Container, Skeleton } from "@mui/material";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    PageHeadActionBar,
    type HeadNavigationAction,
} from "../../../components/shared/PageHeadActionBar";
import { pageContainerSx } from "../../../theme";
import { useCurrentHouseholdWithDetails } from "../../me/activeHousehold/useCurrentHouseholdWithDetails";
import { DeleteHouseholdDialog } from "../components/DeleteHouseholdDialog";
import { HouseholdSettingsCard } from "../components/HouseholdSettingsCard";
import { HouseholdSummaryCard } from "../components/HouseholdSummaryCard";
import { HouseholdRoleValue, roleRank } from "../householdRole";
import { MembersPanel } from "../members/components/MembersPanel";
import { useHouseholdMembers } from "../members/useHouseholdMembers";

export function ManageHouseholdPage() {
    const { t } = useTranslation();
    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

    const {
        currentHousehold,
        currentHouseholdDetails,
        isLoading,
        error,
        hasActiveHousehold,
    } = useCurrentHouseholdWithDetails();

    const { data: members } = useHouseholdMembers(
        currentHousehold?.householdId ?? 0,
        !!currentHousehold?.householdId,
    );

    if (isLoading) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <Skeleton
                        variant="rectangular"
                        height={40}
                        sx={{ mb: 1 }}
                    />
                    <Skeleton variant="text" width="60%" height={32} />
                </Box>
                <Skeleton variant="rectangular" height={200} />
            </Container>
        );
    }

    if (error) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="error">
                    {t("household.failedToLoadHouseholdInformation")}
                </Alert>
            </Container>
        );
    }

    if (!hasActiveHousehold || !currentHousehold?.householdId) {
        return (
            <Container maxWidth="md" sx={pageContainerSx}>
                <Alert severity="info">
                    {t("common.createOrSelectHouseholdFirst")}
                </Alert>
            </Container>
        );
    }

    const householdName =
        currentHouseholdDetails?.name || t("household.household");
    const userRole = currentHousehold.role || HouseholdRoleValue.Member;
    const isOwner = userRole === HouseholdRoleValue.Owner;
    const role = currentHousehold.role;
    const canManageSettings =
        !!role && roleRank[role] >= roleRank[HouseholdRoleValue.Admin];

    const menuActions: HeadNavigationAction[] = isOwner
        ? [
              {
                  text: t("household.deleteHousehold"),
                  icon: <Delete fontSize="small" color="error" />,
                  onClick: () => setDeleteDialogOpen(true),
                  testId: "household-manage-menu-delete",
                  color: "error",
              },
          ]
        : [];

    return (
        <>
            <PageHeadActionBar
                title={t("household.householdManagement")}
                section="household"
                maxWidth="md"
                directActions={[]}
                menuActions={menuActions}
                menuButtonTestId="household-manage-menu-toggle"
            />
            <Container maxWidth="md" sx={pageContainerSx}>
                <Box sx={{ mb: { xs: 2, sm: 3 } }}>
                    <HouseholdSummaryCard
                        householdName={householdName}
                        memberCount={members?.length ?? 0}
                        userRole={userRole}
                    />
                </Box>

                <MembersPanel
                    householdId={currentHousehold.householdId}
                    currentUserRole={userRole}
                />

                <HouseholdSettingsCard
                    householdId={currentHousehold.householdId}
                    canManage={canManageSettings}
                />

                <DeleteHouseholdDialog
                    open={deleteDialogOpen}
                    onClose={() => setDeleteDialogOpen(false)}
                    householdId={currentHousehold.householdId}
                    householdName={householdName}
                />
            </Container>
        </>
    );
}
