using Frigorino.Domain.Entities;

namespace Frigorino.Features.CurrentHousehold
{
    public sealed record CurrentHouseholdResponse(
        int? HouseholdId,
        HouseholdRole? Role,
        bool HasActiveHousehold);
}
