namespace Frigorino.Domain.Entities
{
    // Stored as int in Postgres (EF default); serialized as its string name on the wire via the
    // global JsonStringEnumConverter. Existing rows backfill to Text (the migration default).
    public enum ListItemType
    {
        Text = 0,
        Image = 1,
        Document = 2,
    }
}
