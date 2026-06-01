namespace Frigorino.IntegrationTests.Slices.Settings;

[Binding]
public class SettingsUiSteps(ScenarioContextHolder ctx)
{
    // ---- User menu ----

    [When("I open the user menu")]
    public async Task WhenIOpenTheUserMenu()
    {
        await ctx.Page.GetByTestId("user-menu-toggle").ClickAsync();
    }

    [When("I click settings in the user menu")]
    public async Task WhenIClickSettingsInTheUserMenu()
    {
        await ctx.Page.GetByTestId("user-menu-settings").ClickAsync();
    }

    [Then("the language select is visible")]
    public async Task ThenTheLanguageSelectIsVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("settings-language-select"))
            .ToBeVisibleAsync();
    }

    [Then("the page URL contains {string}")]
    public async Task ThenThePageUrlContains(string fragment)
    {
        await ctx.Page.WaitForURLAsync(url => url.Contains(fragment));
    }

    // ---- User language ----

    [When("I select the language {string}")]
    public async Task WhenISelectTheLanguage(string code)
    {
        var value = ctx.Page.GetByTestId("settings-language-value");

        // The select defaults to the browser locale, which varies by host machine. If the target is
        // already selected there is no change (MUI fires no onChange, no PUT), so the assertion would
        // never bind to a request. Guard against that flake by asserting on the resulting value: only
        // await the PUT when the selection is an actual change.
        var alreadySelected = (await value.InputValueAsync()) == code;

        // The MUI `TextField select` opens its menu when the inner combobox is clicked (the testid
        // lands on the outer FormControl, which does not open the popover), then the option is chosen.
        var responseTask = alreadySelected
            ? null
            : ctx.Page.WaitForResponseAsync(r =>
                r.Url.Contains("/api/me/settings")
                && r.Request.Method == "PUT"
                && r.Status == 200);

        await ctx.Page.GetByTestId("settings-language-select")
            .GetByRole(AriaRole.Combobox).ClickAsync();
        await ctx.Page.GetByTestId($"settings-language-option-{code}").ClickAsync();

        if (responseTask is not null)
        {
            await responseTask;
        }

        // Either way, confirm the in-memory selection took before we reload to assert persistence.
        await Assertions.Expect(value).ToHaveValueAsync(code);
    }

    [Then("the persisted language is {string}")]
    public async Task ThenThePersistedLanguageIs(string code)
    {
        // The hidden <input> of the MUI select carries the language code, so persistence is
        // asserted on the value — never on the translated option text.
        await Assertions.Expect(ctx.Page.GetByTestId("settings-language-value"))
            .ToHaveValueAsync(code);
    }

    // ---- Household retention ----

    [When("I set the household retention to {string}")]
    public async Task WhenISetTheHouseholdRetentionTo(string days)
    {
        // Commit is on blur (PUT). The card seeds the input from the settings GET via an effect,
        // so wait for the loaded (non-empty) value first — otherwise a late GET would overwrite the
        // filled value and no PUT would fire. Await the PUT 200 before returning so the persisted
        // value is observable on the subsequent reload.
        var input = ctx.Page.GetByTestId("household-retention-input");
        await Assertions.Expect(input).Not.ToHaveValueAsync("");
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/settings")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await input.FillAsync(days);
        await input.BlurAsync();
        await responseTask;
    }

    [Then("the household retention input has value {string}")]
    public async Task ThenTheHouseholdRetentionInputHasValue(string expected)
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-retention-input"))
            .ToHaveValueAsync(expected);
    }

    [Then("the household retention input is disabled")]
    public async Task ThenTheHouseholdRetentionInputIsDisabled()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("household-retention-input"))
            .ToBeDisabledAsync();
    }

    // ---- Inventory expiry override ----

    [When("I enable the inventory expiry override")]
    public async Task WhenIEnableTheInventoryExpiryOverride()
    {
        // Toggling the switch on triggers a PUT with the lead value. Await the 200 before asserting
        // the revealed input so the toggle's mutation has landed.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/settings")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await ctx.Page.GetByTestId("inventory-expiry-override-switch").ClickAsync();
        await responseTask;
    }

    [Then("the inventory expiry lead input is visible")]
    public async Task ThenTheInventoryExpiryLeadInputIsVisible()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("inventory-expiry-lead-input"))
            .ToBeVisibleAsync();
    }
}
