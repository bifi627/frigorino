using Frigorino.Domain.Quantities;

namespace Frigorino.Features.Lists.Items
{
    // Atomic nested DTO — value and unit can never be transmitted apart. Nullable on the wire
    // (null = no quantity). QuantityUnit serializes as an integer (existing enum convention).
    public sealed record QuantityDto(decimal Value, QuantityUnit Unit);
}
