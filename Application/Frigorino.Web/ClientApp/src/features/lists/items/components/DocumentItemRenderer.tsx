import { Description } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ListItemResponse } from "../../../../lib/api";
import { useCurrentHousehold } from "../../../me/activeHousehold/useCurrentHousehold";
import { useOpenItemFile } from "../useOpenItemFile";

interface Props {
    item: ListItemResponse;
}

export function DocumentItemRenderer({ item }: Props) {
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;
    const openFile = useOpenItemFile(householdId, item.listId);

    return (
        <Box
            role="button"
            tabIndex={0}
            aria-label={t("lists.openDocument")}
            data-testid={`list-item-document-${item.id}`}
            onClick={() => openFile(item.id)}
            onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    openFile(item.id);
                }
            }}
            sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.5,
                flex: 1,
                minWidth: 0,
                cursor: "pointer",
                borderRadius: 1,
                p: 0.5,
                "&:hover": { bgcolor: "action.hover" },
            }}
        >
            <Description color="action" sx={{ flexShrink: 0 }} />
            <Box sx={{ minWidth: 0 }}>
                <Typography
                    variant="body2"
                    sx={{ wordBreak: "break-word" }}
                    data-testid={`list-item-document-${item.id}-name`}
                >
                    {item.fileName}
                </Typography>
                {item.comment ? (
                    <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ display: "block", wordBreak: "break-word" }}
                    >
                        {item.comment}
                    </Typography>
                ) : null}
            </Box>
        </Box>
    );
}
