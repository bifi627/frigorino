using Frigorino.Domain.Entities;

namespace Frigorino.Features.Me.ActiveHousehold
{
    public sealed record ActiveHouseholdResponse(
        int? HouseholdId,
        HouseholdRole? Role,
        bool HasActiveHousehold);
}
