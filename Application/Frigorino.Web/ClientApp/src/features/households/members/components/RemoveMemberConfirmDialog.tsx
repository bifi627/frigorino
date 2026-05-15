import { ConfirmDialog } from "../../../../components/dialogs/ConfirmDialog";
import type { MemberResponse } from "../../../../lib/api";
import { useRemoveMember } from "../useRemoveMember";

interface RemoveMemberConfirmDialogProps {
    open: boolean;
    onClose: () => void;
    member: MemberResponse | null;
    householdId: number;
}

export const RemoveMemberConfirmDialog = ({
    open,
    onClose,
    member,
    householdId,
}: RemoveMemberConfirmDialogProps) => {
    const { mutate: removeMember, isPending } = useRemoveMember(householdId);

    const handleConfirm = () => {
        if (!member?.externalId) return;
        removeMember(member.externalId, {
            onSuccess: () => onClose(),
        });
    };

    return (
        <ConfirmDialog
            open={open}
            onClose={onClose}
            onConfirm={handleConfirm}
            title="Remove Member"
            description={
                <>
                    Are you sure you want to remove{" "}
                    {member?.name || "this user"} from this household? This
                    action cannot be undone.
                </>
            }
            confirmLabel="Remove"
            confirmLabelPending="Removing..."
            cancelLabel="Cancel"
            isPending={isPending}
            confirmTestId="household-member-remove-confirm"
            cancelTestId="household-member-remove-cancel"
        />
    );
};
