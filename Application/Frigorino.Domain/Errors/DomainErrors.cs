using FluentResults;

namespace Frigorino.Domain.Errors
{
    // Domain-emitted error categories. The Features layer pattern-matches on these to map
    // to HTTP responses (Forbid / NotFound / ValidationProblem). Keeping the categories in
    // Domain lets aggregate methods speak the rule semantically without reaching for HTTP.
    public sealed class AccessDeniedError : Error
    {
        public AccessDeniedError(string message) : base(message)
        {
        }
    }

    public sealed class EntityNotFoundError : Error
    {
        public EntityNotFoundError(string message) : base(message)
        {
        }
    }
}
