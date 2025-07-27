namespace Frigorino.Domain.Interfaces
{
    public interface ICurrentUserService
    {
        string UserId { get; }
        string? Email { get; }
        string? UserName { get; }
    }
}
