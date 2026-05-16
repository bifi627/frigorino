namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class ListItemSteps(ScenarioContextHolder ctx, TestApiClient api)
{
    [Given("there is a list named {string} with item {string}")]
    public async Task GivenThereIsAListNamedWithItem(string listName, string itemText)
    {
        var listId = await api.CreateListAsync(listName);
        ctx.ListIds[listName] = listId;
        var itemId = await api.CreateListItemAsync(listId, itemText);
        ctx.ListItemIds[itemText] = itemId;
    }

    [Given("the list {string} also has item {string}")]
    public async Task GivenTheListAlsoHasItem(string listName, string itemText)
    {
        var listId = ctx.ListIds[listName];
        var itemId = await api.CreateListItemAsync(listId, itemText);
        ctx.ListItemIds[itemText] = itemId;
    }

    [When("I add item {string} to the list")]
    public async Task WhenIAddItemToTheList(string itemText)
    {
        // Await the POST so back-to-back "add item" steps don't fire overlapping requests —
        // optimistic order can match server order, but cache invalidation racing the next
        // step's UI assertion has flaked here before.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.EndsWith("/items")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("autocomplete-input-textfield").ClickAsync();
        await ctx.Page.GetByTestId("autocomplete-input-textfield").PressSequentiallyAsync(itemText);
        await ctx.Page.GetByTestId("autocomplete-input-submit-button").ClickAsync();
        await responseTask;
    }

    [When("I toggle {string} as done")]
    public async Task WhenIToggleAsDone(string itemText)
    {
        // Wait for the server's toggle-status response before the step returns so a follow-up
        // toggle (uncheck-after-check scenario) doesn't fire while the first request is still
        // in flight — the two clicks would otherwise race and cancel out.
        var item = ctx.Page.GetByTestId($"toggle-item-{itemText}");
        var selector = $"[data-testid='toggle-item-{itemText}']";
        await item.WaitForAsync();
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Url.Contains("/toggle-status")
            && r.Request.Method == "PATCH");
        await ctx.Page.DispatchEventAsync(selector, "click");
        await responseTask;
    }

    [When("I open the item menu for {string}")]
    public async Task WhenIOpenTheItemMenuFor(string itemText)
    {
        // The menu button sits inside dnd-kit's sortable container, which contributes
        // ancestor aria attributes that Playwright's actionability check reads as
        // "element is not enabled". Force=true skips the check — the button itself has no
        // disabled prop, so the click is safe.
        await ctx.Page.GetByTestId($"item-menu-button-{itemText}")
            .ClickAsync(new LocatorClickOptions { Force = true });
    }

    [When("I click delete from the item menu")]
    public async Task WhenIClickDeleteFromTheItemMenu()
    {
        // Wait for the DELETE response so the next Then-step inspects post-server-confirm DOM
        // rather than the optimistic-update window where rollback could still re-add the row.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Request.Method == "DELETE"
            && r.Status == 204);
        await ctx.Page.GetByTestId("delete-item-button")
            .ClickAsync(new LocatorClickOptions { Force = true });
        await responseTask;
    }

    [When("I enable drag handles")]
    public async Task WhenIEnableDragHandles()
    {
        await ctx.Page.GetByTestId("list-toggle-drag-handles").ClickAsync();
        // Wait for at least one drag handle to render so the next "I drag" step doesn't race
        // the toggle re-render.
        await ctx.Page.Locator("[data-testid^='drag-handle-item-']").First.WaitForAsync();
    }

    [When("I drag {string} above {string}")]
    public async Task WhenIDragAbove(string sourceText, string targetText)
    {
        // dnd-kit's PointerSensor uses activationConstraint { distance: 8 }, so we have to
        // move >8px after mouse-down before a real drag activates. The two-hop move below
        // (small wiggle to activate, then the long drag to the target) mirrors how a real
        // pointer interaction unfolds and keeps the reorder request deterministic.
        var sourceHandle = ctx.Page.GetByTestId($"drag-handle-item-{sourceText}");
        var targetHandle = ctx.Page.GetByTestId($"drag-handle-item-{targetText}");
        await sourceHandle.WaitForAsync();
        await targetHandle.WaitForAsync();

        var sourceBox = await sourceHandle.BoundingBoxAsync()
            ?? throw new InvalidOperationException($"No bounding box for drag source '{sourceText}'.");
        var targetBox = await targetHandle.BoundingBoxAsync()
            ?? throw new InvalidOperationException($"No bounding box for drag target '{targetText}'.");

        var sx = (float)(sourceBox.X + sourceBox.Width / 2);
        var sy = (float)(sourceBox.Y + sourceBox.Height / 2);
        var tx = (float)(targetBox.X + targetBox.Width / 2);
        var ty = (float)(targetBox.Y + targetBox.Height / 2);

        // Subscribe before mouse.up — handleDragEnd fires the PATCH on release.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Url.Contains("/reorder")
            && r.Request.Method == "PATCH");

        await ctx.Page.Mouse.MoveAsync(sx, sy);
        await ctx.Page.Mouse.DownAsync();
        await ctx.Page.Mouse.MoveAsync(sx, sy + 12, new MouseMoveOptions { Steps = 3 });
        await ctx.Page.Mouse.MoveAsync(tx, ty, new MouseMoveOptions { Steps = 10 });
        await ctx.Page.Mouse.UpAsync();
        await responseTask;
    }

    [Then("{string} appears in the list")]
    public async Task ThenAppearsInTheList(string itemText)
    {
        await ctx.Page.Locator("[data-section='unchecked-items']")
            .GetByText(itemText).WaitForAsync();
    }

    [Then("{string} is shown as checked")]
    public async Task ThenIsShownAsChecked(string itemText)
    {
        await ctx.Page.Locator("[data-section='checked-items'] li")
            .Filter(new LocatorFilterOptions { HasText = itemText })
            .WaitForAsync();
    }

    [Then("{string} no longer appears in the list")]
    public async Task ThenNoLongerAppearsInTheList(string itemText)
    {
        await Assertions.Expect(ctx.Page.GetByTestId($"toggle-item-{itemText}"))
            .Not.ToBeVisibleAsync();
    }

    [Then("the unchecked items appear in order: {string}")]
    public async Task ThenTheUncheckedItemsAppearInOrder(string commaSeparated)
    {
        var expected = commaSeparated.Split(',').Select(s => s.Trim()).ToArray();
        var toggles = ctx.Page.Locator("[data-section='unchecked-items'] [data-testid^='toggle-item-']");

        // Wait for the row count to stabilize before reading testids — guards against
        // optimistic-update churn where the DOM is mid-rerender when CountAsync fires.
        await Assertions.Expect(toggles).ToHaveCountAsync(expected.Length);

        var count = await toggles.CountAsync();
        var actual = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var testId = await toggles.Nth(i).GetAttributeAsync("data-testid");
            actual.Add(testId!["toggle-item-".Length..]);
        }
        actual.Should().Equal(expected);
    }
}
