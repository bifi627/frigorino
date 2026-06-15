namespace Frigorino.Infrastructure.Services
{
    // Logical blob "areas" — one per blob-owning feature. The area name is the DI key for that
    // area's IFileStorage/IFileStorageMaintenance and the folder segment in the composed prefix
    // ({env}/{area}). Adding a blob-owning feature = add a constant here, register its area in
    // AddFileStorage, and add an IBlobReferenceSource for it. See FileStorageDependencyInjection.
    public static class BlobAreas
    {
        public const string ListItem = "list-item";
    }
}
