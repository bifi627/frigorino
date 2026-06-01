using Frigorino.Domain.Quantities;

namespace Frigorino.Features.Quantities
{
    // Atomic nested DTO — value and unit can never be transmitted apart. Nullable on the wire
    // (null = no quantity). QuantityUnit serializes as a string name (existing enum convention).
    // Feature-neutral: shared by the Lists and Inventories slices.
    public sealed record QuantityDto(decimal Value, QuantityUnit Unit);
}
