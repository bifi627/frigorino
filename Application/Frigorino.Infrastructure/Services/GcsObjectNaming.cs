namespace Frigorino.Infrastructure.Services
{
    // Maps between bare storage keys (what the DB stores) and prefixed GCS object names. The prefix
    // namespaces all Frigorino objects so the orphan sweep only ever enumerates/deletes our own
    // objects, even in a shared bucket.
    public static class GcsObjectNaming
    {
        public static string ToObjectName(string prefix, string key)
        {
            return $"{prefix}/{key}";
        }

        public static string ToKey(string prefix, string objectName)
        {
            var head = prefix + "/";
            if (objectName.StartsWith(head, StringComparison.Ordinal))
            {
                return objectName[head.Length..];
            }

            return objectName;
        }
    }
}
