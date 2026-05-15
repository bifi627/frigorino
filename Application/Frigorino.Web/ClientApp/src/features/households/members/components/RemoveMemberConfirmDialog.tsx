import { useTranslation } from "react-i18next";
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
    const { t } = useTranslation();
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
            title={t("household.removeMember")}
            description={t("household.confirmRemoveMember", {
                memberName: member?.name || t("household.thisUser"),
            })}
            confirmLabel={t("household.remove")}
            confirmLabelPending={t("household.removing")}
            cancelLabel={t("common.cancel")}
            isPending={isPending}
            confirmTestId="household-member-remove-confirm"
            cancelTestId="household-member-remove-cancel"
        />
    );
};
