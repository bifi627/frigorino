using Frigorino.Domain.Quantities;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ExtractionApiSteps(ScenarioContextHolder ctx)
{
    [Then("the list item eventually has text {string} with quantity {int} unit {int}")]
    public async Task ThenItemHasQuantity(string expectedText, int value, int unit)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = ctx.Factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.ListItems
                .AsNoTracking()
                .Include(i => i.List)
                .FirstOrDefaultAsync(i => i.Text == expectedText
                    && i.List.HouseholdId == ctx.HouseholdId);
            if (item is not null && item.QuantityValue == (decimal)value
                && item.QuantityUnit == (QuantityUnit)unit)
            {
                return;
            }
            await Task.Delay(100);
        }
        Assert.Fail($"Item '{expectedText}' with quantity {value}/{unit} did not appear in time.");
    }
}
