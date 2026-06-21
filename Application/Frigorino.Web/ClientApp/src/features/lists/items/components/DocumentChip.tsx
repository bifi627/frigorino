import { Description } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";

interface Props {
    name?: string | null;
    nameTestId?: string;
}

// Static "icon + filename" card shown by the document preview/caption sheets (a document has no
// image thumbnail to render). The interactive list row uses DocumentItemRenderer, not this.
export function DocumentChip({ name, nameTestId }: Props) {
    return (
        <Box
            sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.5,
                mb: 2,
                p: 1.5,
                borderRadius: 1,
                bgcolor: "action.hover",
            }}
        >
            <Description color="action" />
            <Typography
                variant="body2"
                sx={{ wordBreak: "break-word", minWidth: 0 }}
                data-testid={nameTestId}
            >
                {name}
            </Typography>
        </Box>
    );
}
