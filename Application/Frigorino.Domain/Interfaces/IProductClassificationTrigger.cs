namespace Frigorino.Domain.Interfaces
{
    // Called by the list-item slices when a product name is referenced. The enabled implementation
    // enqueues the classify job; the disabled implementation is a no-op. This seam is the localized
    // swap point if classification later moves to domain events.
    public interface IProductClassificationTrigger
    {
        void OnProductReferenced(int householdId, string rawName);
    }
}
