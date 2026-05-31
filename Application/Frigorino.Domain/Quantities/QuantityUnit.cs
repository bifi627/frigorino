namespace Frigorino.Domain.Quantities
{
    // Fixed grocery/pantry unit set. Stored as int (EF default) and serialized as int on the
    // wire (matches the existing enum convention; no JsonStringEnumConverter). Piece is the
    // default for a bare count.
    public enum QuantityUnit
    {
        Gram = 0,
        Kilogram = 1,
        Milliliter = 2,
        Liter = 3,
        Piece = 4,
        Pack = 5,
        Can = 6,
        Bottle = 7,
        Bag = 8,
    }
}
