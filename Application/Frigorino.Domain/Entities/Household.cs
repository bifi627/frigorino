using FluentResults;
using Frigorino.Domain.Errors;

namespace Frigorino.Domain.Entities
{
    public class Household
    {
        // Source of truth for length constraints. Both the factory (Household.Create) and the
        // EF configuration (HouseholdConfiguration) read from these so DB and aggregate agree.
        public const int NameMaxLength = 255;
        public const int DescriptionMaxLength = 1000;

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string CreatedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Navigation properties
        public User CreatedByUser { get; set; } = null!;
        public ICollection<UserHousehold> UserHouseholds { get; set; } = new List<UserHousehold>();
        public ICollection<List> Lists { get; set; } = new List<List>();
        public ICollection<Inventory> Inventories { get; set; } = new List<Inventory>();

        // The "Property" metadata key duplicates Frigorino.Features.Results.ResultExtensions.PropertyMetadataKey
        // by convention — Domain stays free of a Features dependency.
        public static Result<Household> Create(string name, string? description, string ownerUserId)
        {
            var errors = new List<IError>();
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new Error("Household name is required.")
                    .WithMetadata("Property", nameof(Name)));
            }
            else if (name.Trim().Length > NameMaxLength)
            {
                errors.Add(new Error($"Household name must be {NameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Name)));
            }
            if (description is not null && description.Length > DescriptionMaxLength)
            {
                errors.Add(new Error($"Household description must be {DescriptionMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(Description)));
            }
            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                errors.Add(new Error("Owner user id is required.")
                    .WithMetadata("Property", nameof(CreatedByUserId)));
            }
            if (errors.Count > 0)
            {
                return Result.Fail<Household>(errors);
            }

            var now = DateTime.UtcNow;
            var trimmedDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
            var household = new Household
            {
                Name = name.Trim(),
                Description = trimmedDescription,
                CreatedByUserId = ownerUserId,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            // Seed owner membership in the navigation collection so a single SaveChanges persists
            // both rows in one transaction (EF relationship fixup assigns HouseholdId on insert).
            // ApplicationDbContext.SaveChangesAsync only re-stamps timestamps when == default,
            // so the values set here are preserved.
            household.UserHouseholds.Add(new UserHousehold
            {
                UserId = ownerUserId,
                Role = HouseholdRole.Owner,
                JoinedAt = now,
                IsActive = true,
            });
            return Result.Ok(household);
        }

        // Aggregate-internal mutation. Caller membership, role policy, last-Owner/already-member
        // invariants, and reactivation-vs-append branching all live here so the slice handler
        // shrinks to load → call → persist. The handler resolves the cross-aggregate target user
        // by email separately and passes the resolved external id in.
        public Result<UserHousehold> AddMember(string callerUserId, string targetUserId, HouseholdRole role)
        {
            var caller = UserHouseholds.FirstOrDefault(uh => uh.UserId == callerUserId && uh.IsActive);
            if (caller is null)
            {
                return Result.Fail<UserHousehold>(
                    new EntityNotFoundError("Caller is not a member of this household."));
            }

            if (!caller.Role.CanManageMembers())
            {
                return Result.Fail<UserHousehold>(
                    new AccessDeniedError("Members cannot add other members."));
            }

            if (!caller.Role.CanGrantRole(role))
            {
                return Result.Fail<UserHousehold>(
                    new AccessDeniedError("Only an Owner can grant the Owner role."));
            }

            var existing = UserHouseholds.FirstOrDefault(uh => uh.UserId == targetUserId);
            if (existing is not null)
            {
                if (existing.IsActive)
                {
                    return Result.Fail<UserHousehold>(
                        new Error("User is already a member.")
                            .WithMetadata("Property", "email"));
                }

                existing.IsActive = true;
                existing.Role = role;
                existing.JoinedAt = DateTime.UtcNow;
                return Result.Ok(existing);
            }

            var creation = UserHousehold.CreateMembership(targetUserId, Id, role);
            if (creation.IsFailed)
            {
                return creation;
            }

            UserHouseholds.Add(creation.Value);
            return creation;
        }

        // Aggregate-internal mutation. Self-removal short-circuits the role guard (any role can
        // remove themselves). Cross-user removal requires Admin/Owner. Last active Owner is
        // protected — soft-deleting them would orphan the household.
        public Result RemoveMember(string callerUserId, string targetUserId)
        {
            var caller = UserHouseholds.FirstOrDefault(uh => uh.UserId == callerUserId && uh.IsActive);
            if (caller is null)
            {
                return Result.Fail(
                    new EntityNotFoundError("Caller is not a member of this household."));
            }

            var target = UserHouseholds.FirstOrDefault(uh => uh.UserId == targetUserId && uh.IsActive);
            if (target is null)
            {
                return Result.Fail(
                    new EntityNotFoundError("Target is not a member of this household."));
            }

            var isSelfRemoval = caller.UserId == target.UserId;
            if (!isSelfRemoval && !caller.Role.CanManageMembers())
            {
                return Result.Fail(
                    new AccessDeniedError("Members cannot remove other members."));
            }

            if (target.Role == HouseholdRole.Owner && IsLastActiveOwner())
            {
                return Result.Fail(
                    new Error("Cannot remove the last owner.")
                        .WithMetadata("Property", "userId"));
            }

            target.IsActive = false;
            return Result.Ok();
        }

        // Aggregate-internal mutation. Owner-protection (only an Owner may change another Owner)
        // and last-Owner-self-demote protection both live here next to the role-policy rules
        // they share with AddMember/RemoveMember.
        public Result<UserHousehold> ChangeMemberRole(string callerUserId, string targetUserId, HouseholdRole newRole)
        {
            var caller = UserHouseholds.FirstOrDefault(uh => uh.UserId == callerUserId && uh.IsActive);
            if (caller is null)
            {
                return Result.Fail<UserHousehold>(
                    new EntityNotFoundError("Caller is not a member of this household."));
            }

            if (!caller.Role.CanManageMembers())
            {
                return Result.Fail<UserHousehold>(
                    new AccessDeniedError("Members cannot change member roles."));
            }

            if (!caller.Role.CanGrantRole(newRole))
            {
                return Result.Fail<UserHousehold>(
                    new AccessDeniedError("Only an Owner can grant the Owner role."));
            }

            var target = UserHouseholds.FirstOrDefault(uh => uh.UserId == targetUserId && uh.IsActive);
            if (target is null)
            {
                return Result.Fail<UserHousehold>(
                    new EntityNotFoundError("Target is not a member of this household."));
            }

            if (target.Role == HouseholdRole.Owner && caller.Role != HouseholdRole.Owner)
            {
                return Result.Fail<UserHousehold>(
                    new AccessDeniedError("Only an Owner can change another Owner's role."));
            }

            var demotingSelfFromOwner = caller.UserId == target.UserId
                && target.Role == HouseholdRole.Owner
                && newRole != HouseholdRole.Owner;

            if (demotingSelfFromOwner && IsLastActiveOwner())
            {
                return Result.Fail<UserHousehold>(
                    new Error("Cannot remove the last owner.")
                        .WithMetadata("Property", "role"));
            }

            target.Role = newRole;
            return Result.Ok(target);
        }

        // Aggregate-internal mutation. Owner-only soft-delete cascades to all memberships in
        // one pass, so a single SaveChanges persists the household + all rows in one transaction.
        public Result SoftDelete(string callerUserId)
        {
            var caller = UserHouseholds.FirstOrDefault(uh => uh.UserId == callerUserId && uh.IsActive);
            if (caller is null)
            {
                return Result.Fail(
                    new EntityNotFoundError("Caller is not a member of this household."));
            }

            if (caller.Role != HouseholdRole.Owner)
            {
                return Result.Fail(
                    new AccessDeniedError("Only an Owner can delete this household."));
            }

            IsActive = false;
            UpdatedAt = DateTime.UtcNow;
            foreach (var membership in UserHouseholds)
            {
                membership.IsActive = false;
            }

            return Result.Ok();
        }

        private bool IsLastActiveOwner()
        {
            return UserHouseholds.Count(uh => uh.Role == HouseholdRole.Owner && uh.IsActive) <= 1;
        }
    }
}
