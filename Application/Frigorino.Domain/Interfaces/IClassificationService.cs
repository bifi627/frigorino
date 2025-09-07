
namespace Frigorino.Domain.Interfaces
{
    public interface IClassificationService
    {
        Task Classify(IEnumerable<int> listItemIds);
    }
}