import {
    Alert,
    Box,
    Button,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    FormControl,
    InputLabel,
    MenuItem,
    Select,
    TextField,
} from "@mui/material";
import React, { useState } from "react";
import { ApiError, type HouseholdRole } from "../../../../lib/api";
import { HouseholdRoleValue, useRoleLabels } from "../../householdRole";
import { useAddMember } from "../useAddMember";

interface AddMemberDialogProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
}

function readEmailError(error: unknown): string {
    if (error instanceof ApiError) {
        const emailErrors = error.body?.errors?.email;
        if (Array.isArray(emailErrors) && emailErrors.length > 0) {
            return emailErrors[0];
        }
    }
    return "Failed to add member";
}

export const AddMemberDialog: React.FC<AddMemberDialogProps> = ({
    open,
    onClose,
    householdId,
}) => {
    const roleLabels = useRoleLabels();
    const [email, setEmail] = useState("");
    const [role, setRole] = useState<HouseholdRole>(HouseholdRoleValue.Member);
    const [error, setError] = useState<string | null>(null);

    const addMemberMutation = useAddMember(householdId);

    const handleClose = () => {
        setEmail("");
        setRole(HouseholdRoleValue.Member);
        setError(null);
        onClose();
    };

    const handleSubmit = (e: React.FormEvent) => {
        e.preventDefault();
        setError(null);

        if (!email.trim()) {
            setError("Email is required");
            return;
        }

        if (!email.includes("@")) {
            setError("Please enter a valid email address");
            return;
        }

        addMemberMutation.mutate(
            { email: email.trim(), role },
            {
                onSuccess: () => handleClose(),
                onError: (err) => setError(readEmailError(err)),
            },
        );
    };

    return (
        <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
            <DialogTitle>Add Member to Household</DialogTitle>
            <form onSubmit={handleSubmit}>
                <DialogContent>
                    <Box
                        sx={{
                            display: "flex",
                            flexDirection: "column",
                            gap: 2,
                            pt: 1,
                        }}
                    >
                        {error && (
                            <Alert
                                data-testid="household-add-member-error"
                                severity="error"
                                onClose={() => setError(null)}
                            >
                                {error}
                            </Alert>
                        )}

                        <TextField
                            label="Email Address"
                            type="email"
                            value={email}
                            onChange={(e) => setEmail(e.target.value)}
                            fullWidth
                            required
                            placeholder="user@example.com"
                            disabled={addMemberMutation.isPending}
                            slotProps={{
                                htmlInput: {
                                    "data-testid":
                                        "household-add-member-email-input",
                                },
                            }}
                        />

                        <FormControl fullWidth>
                            <InputLabel>Role</InputLabel>
                            <Select
                                data-testid="household-add-member-role-select"
                                value={role}
                                onChange={(e) =>
                                    setRole(e.target.value as HouseholdRole)
                                }
                                label="Role"
                                disabled={addMemberMutation.isPending}
                            >
                                <MenuItem value={HouseholdRoleValue.Member}>
                                    {roleLabels[HouseholdRoleValue.Member]}
                                </MenuItem>
                                <MenuItem value={HouseholdRoleValue.Admin}>
                                    {roleLabels[HouseholdRoleValue.Admin]}
                                </MenuItem>
                                <MenuItem value={HouseholdRoleValue.Owner}>
                                    {roleLabels[HouseholdRoleValue.Owner]}
                                </MenuItem>
                            </Select>
                        </FormControl>
                    </Box>
                </DialogContent>

                <DialogActions>
                    <Button
                        data-testid="household-add-member-cancel"
                        onClick={handleClose}
                        disabled={addMemberMutation.isPending}
                    >
                        Cancel
                    </Button>
                    <Button
                        data-testid="household-add-member-submit"
                        type="submit"
                        variant="contained"
                        disabled={addMemberMutation.isPending}
                        startIcon={
                            addMemberMutation.isPending ? (
                                <CircularProgress size={20} />
                            ) : null
                        }
                    >
                        {addMemberMutation.isPending
                            ? "Adding..."
                            : "Add Member"}
                    </Button>
                </DialogActions>
            </form>
        </Dialog>
    );
};
