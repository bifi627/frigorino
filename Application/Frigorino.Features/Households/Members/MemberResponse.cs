using Frigorino.Domain.Entities;

namespace Frigorino.Features.Households.Members
{
    public sealed record MemberResponse(
        string ExternalId,
        string Name,
        string Email,
        HouseholdRole Role,
        DateTime JoinedAt);
}
