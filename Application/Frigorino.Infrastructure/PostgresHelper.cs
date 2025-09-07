using System.Text.RegularExpressions;

namespace Frigorino.Infrastructure
{
    public static class PostgresHelper
    {
        public static string ConvertPostgresUrlToConnectionString(string url)
        {
            // Regex pattern to parse the PostgreSQL URL
            var pattern = @"^postgres(?:ql)?://([^:]+):([^@]+)@([^:]+):(\d+)/(.+)$";
            var match = Regex.Match(url, pattern);

            if (!match.Success)
                throw new ArgumentException("Invalid PostgreSQL URL format.");

            var user = match.Groups[1].Value;
            var password = match.Groups[2].Value;
            var host = match.Groups[3].Value;
            var port = match.Groups[4].Value;
            var database = match.Groups[5].Value;

            return $"User Id={user};Password={password};Server={host};Port={port};Database={database};";
        }
    }
}
