namespace Frigorino.Domain.Interfaces
{
    public interface IMaintenanceTask
    {
        Task Run(CancellationToken cancellationToken = default);
    }

}
