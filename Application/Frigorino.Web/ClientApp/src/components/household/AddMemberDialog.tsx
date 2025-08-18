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
import {
    useMutation,
    useQueryClient,
    type DefaultError,
} from "@tanstack/react-query";
import React, { useState } from "react";
import { ClientApi } from "../../common/apiClient";
import { type AddMemberRequest, type HouseholdRole } from "../../lib/api";

interface AddMemberDialogProps {
    open: boolean;
    onClose: () => void;
    householdId: number;
}

const roleLabels: Record<number, string> = {
    0: "Member",
    1: "Admin",
    2: "Owner",
};

export const AddMemberDialog: React.FC<AddMemberDialogProps> = ({
    open,
    onClose,
    householdId,
}) => {
    const [email, setEmail] = useState("");
    const [role, setRole] = useState<HouseholdRole>(0);
    const [error, setError] = useState<string | null>(null);

    const queryClient = useQueryClient();

    const addMemberMutation = useMutation({
        mutationFn: async (request: AddMemberRequest) => {
            return ClientApi.members.postApiHouseholdMembers(
                householdId,
                request,
            );
        },
        onSuccess: () => {
            // Invalidate and refetch household members
            queryClient.invalidateQueries({
                queryKey: ["household-members", householdId],
            });
            queryClient.invalidateQueries({ queryKey: ["user-households"] });
            handleClose();
        },
        onError: (error: DefaultError) => {
            setError(error.message || "Failed to add member");
        },
    });

    const handleClose = () => {
        setEmail("");
        setRole(0);
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

        addMemberMutation.mutate({
            email: email.trim(),
            role,
        });
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
                        />

                        <FormControl fullWidth>
                            <InputLabel>Role</InputLabel>
                            <Select
                                value={role}
                                onChange={(e) =>
                                    setRole(e.target.value as HouseholdRole)
                                }
                                label="Role"
                                disabled={addMemberMutation.isPending}
                            >
                                <MenuItem value={0}>{roleLabels[0]}</MenuItem>
                                <MenuItem value={1}>{roleLabels[1]}</MenuItem>
                                <MenuItem value={2}>{roleLabels[2]}</MenuItem>
                            </Select>
                        </FormControl>
                    </Box>
                </DialogContent>

                <DialogActions>
                    <Button
                        onClick={handleClose}
                        disabled={addMemberMutation.isPending}
                    >
                        Cancel
                    </Button>
                    <Button
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
