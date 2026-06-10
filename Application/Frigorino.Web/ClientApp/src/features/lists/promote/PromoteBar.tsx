import { Inventory2Outlined } from "@mui/icons-material";
import { Button, Paper, Typography } from "@mui/material";
import { alpha } from "@mui/material/styles";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { featureContentPx } from "../../../theme";
import { useList } from "../useList";
import { PromoteReviewSheet } from "./PromoteReviewSheet";

interface PromoteBarProps {
    householdId: number;
    listId: number;
}

// Sits between the list header and the scrolling item list. Visible only while this list has
// pending promote candidates (perishables checked off but not yet added to inventory). When the
// count drops to zero the inner unmounts, which resets its `open` state — so the next time
// candidates appear the review sheet starts closed (no reset-in-effect needed).
export const PromoteBar = ({ householdId, listId }: PromoteBarProps) => {
    const { data: list } = useList(householdId, listId);
    const count = list?.pendingPromotionCount ?? 0;

    if (count === 0) {
        return null;
    }

    return (
        <PromoteBarInner
            householdId={householdId}
            listId={listId}
            count={count}
        />
    );
};

interface PromoteBarInnerProps {
    householdId: number;
    listId: number;
    count: number;
}

const PromoteBarInner = ({
    householdId,
    listId,
    count,
}: PromoteBarInnerProps) => {
    const { t } = useTranslation();
    const [open, setOpen] = useState(false);

    return (
        <>
            <Paper
                elevation={0}
                data-testid="promote-bar"
                data-count={count}
                sx={{
                    mx: featureContentPx,
                    mb: 1,
                    px: 1.5,
                    py: 1,
                    display: "flex",
                    alignItems: "center",
                    gap: 1,
                    bgcolor: (theme) => alpha(theme.palette.primary.main, 0.12),
                    color: "text.primary",
                    border: "1px solid",
                    borderColor: (theme) =>
                        alpha(theme.palette.primary.main, 0.3),
                    borderLeft: "3px solid",
                    borderLeftColor: "primary.main",
                }}
            >
                <Inventory2Outlined
                    fontSize="small"
                    sx={{ color: "primary.main" }}
                />
                <Typography variant="body2" sx={{ flex: 1 }}>
                    {t("promote.barReady", { count })}
                </Typography>
                <Button
                    size="small"
                    variant="contained"
                    data-testid="promote-bar-review"
                    onClick={() => setOpen(true)}
                    sx={{
                        bgcolor: (theme) =>
                            alpha(theme.palette.primary.main, 0.85),
                        "&:hover": { bgcolor: "primary.main" },
                    }}
                >
                    {t("promote.review")}
                </Button>
            </Paper>

            <PromoteReviewSheet
                open={open}
                onClose={() => setOpen(false)}
                householdId={householdId}
                listId={listId}
            />
        </>
    );
};
