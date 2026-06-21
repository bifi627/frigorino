namespace Frigorino.IntegrationTests.Slices.Lists;

[Binding]
public class MediaItemSteps
{
    private readonly ScenarioContextHolder ctx;
    private readonly TestApiClient api;

    public MediaItemSteps(ScenarioContextHolder ctx, TestApiClient api)
    {
        this.ctx = ctx;
        this.api = api;
    }

    // Small 8x8 RGBA PNG (valid, CRC-correct — the decoder (Magick.NET) validates IDAT CRC).
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAAFklEQVR4nGOpCDjxnwEPYGEgAIaHAgCvwgKw2JOr9gAAAABJRU5ErkJggg==");

    // Minimal valid PDF bytes — the document path stores the raw bytes (no parsing), so a header +
    // EOF marker round-trips and serves back as application/pdf.
    private static readonly byte[] TinyPdf = System.Text.Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

    [When("I upload a photo with caption {string} to {string} via the API")]
    public async Task WhenIUploadAPhotoViaTheApi(string caption, string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryUploadImageAsync(listId, caption);
        if (ctx.LastApiResponse.Ok)
        {
            var json = await ctx.LastApiResponse.JsonAsync();
            ctx.SetListItemId(listName, "__photo__", json!.Value.GetProperty("id").GetInt32());
        }
    }

    [Then("the uploaded item in {string} serves a thumbnail with content-type {string}")]
    public async Task ThenServesThumbnail(string listName, string contentType)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, "__photo__");
        var resp = await api.TryGetItemThumbnailAsync(listId, itemId);
        Assert.Equal(200, resp.Status);
        Assert.Contains(contentType, resp.Headers["content-type"]);
    }

    [Then("the uploaded item in {string} serves a file with content-type {string}")]
    public async Task ThenServesFile(string listName, string contentType)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, "__photo__");
        var resp = await api.TryGetItemFileAsync(listId, itemId);
        Assert.Equal(200, resp.Status);
        Assert.Contains(contentType, resp.Headers["content-type"]);
    }

    [When("I upload a document with caption {string} to {string} via the API")]
    public async Task WhenIUploadADocumentViaTheApi(string caption, string listName)
    {
        var listId = ctx.ListIds[listName];
        ctx.LastApiResponse = await api.TryUploadDocumentAsync(listId, caption);
        if (ctx.LastApiResponse.Ok)
        {
            var json = await ctx.LastApiResponse.JsonAsync();
            ctx.SetListItemId(listName, "__document__", json!.Value.GetProperty("id").GetInt32());
        }
    }

    [Then("the uploaded document in {string} serves a file with content-type {string}")]
    public async Task ThenDocumentServesFile(string listName, string contentType)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, "__document__");
        var resp = await api.TryGetItemFileAsync(listId, itemId);
        Assert.Equal(200, resp.Status);
        Assert.Contains(contentType, resp.Headers["content-type"]);
    }

    [Then("the uploaded document in {string} has no thumbnail")]
    public async Task ThenDocumentHasNoThumbnail(string listName)
    {
        var listId = ctx.ListIds[listName];
        var itemId = ctx.GetListItemId(listName, "__document__");
        var resp = await api.TryGetItemThumbnailAsync(listId, itemId);
        Assert.Equal(404, resp.Status);
    }

    [When("I attach a photo with caption {string}")]
    public async Task WhenIAttachAPhoto(string caption)
    {
        await ctx.Page.GetByTestId("composer-attach-button").ClickAsync();
        await ctx.Page.GetByTestId("composer-attach-photo").ClickAsync();
        await ctx.Page.GetByTestId("composer-attach-file-input").SetInputFilesAsync(new FilePayload
        {
            Name = "photo.png",
            MimeType = "image/png",
            Buffer = TinyPng,
        });
        await ctx.Page.GetByTestId("media-caption-input").FillAsync(caption);

        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.EndsWith("/items/media") && r.Request.Method == "POST" && r.Status == 201);
        await ctx.Page.GetByTestId("media-send-button").ClickAsync();
        await responseTask;
    }

    [Then("a photo thumbnail appears in the list")]
    public async Task ThenThumbnailAppears()
    {
        await Assertions.Expect(
            ctx.Page.Locator("[data-testid^='list-item-image-']").First).ToBeVisibleAsync();
    }

    [When("I open the photo")]
    public async Task WhenIOpenThePhoto()
    {
        await ctx.Page.Locator("[data-testid^='list-item-image-']").First.ClickAsync();
    }

    [Then("the image lightbox is shown")]
    public async Task ThenLightboxShown()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("image-lightbox")).ToBeVisibleAsync();
    }

    // Checks off the photo row. An image item has no text, so its toggle testid is keyed by id
    // (toggle-item-{id}); rather than know the id, we scope to the row (li) that contains the image
    // thumbnail and click its toggle. Awaits the toggle-status PATCH so the remount (the row moves
    // between the unchecked/checked sections) has actually happened before the next assertion.
    [When("I check off the photo")]
    public async Task WhenICheckOffThePhoto()
    {
        var photoRow = ctx.Page.Locator("li:has([data-testid^='list-item-image-'])").First;
        var toggle = photoRow.Locator("[data-testid^='toggle-item-']");
        await toggle.WaitForAsync();
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/items/")
            && r.Url.Contains("/toggle-status")
            && r.Request.Method == "PATCH");
        await toggle.ClickAsync();
        await responseTask;
    }

    // Regression guard for the thumbnail-breaks-on-toggle bug (fixed in useItemImage.ts by caching
    // the Blob, not the object URL). A revoked object URL still renders a "visible" <img>, so
    // ToBeVisible would NOT catch it — we must assert the image actually decoded: complete &&
    // naturalWidth > 0. Polled to ride out the remount's brief skeleton/load gap.
    [Then("the photo thumbnail is still shown")]
    public async Task ThenThumbnailStillShown()
    {
        var img = ctx.Page.Locator("[data-testid^='list-item-image-'] img").First;
        await Assertions.Expect(img).ToBeVisibleAsync();

        var deadline = DateTime.UtcNow.AddSeconds(10);
        var loaded = false;
        while (DateTime.UtcNow < deadline)
        {
            loaded = await img.EvaluateAsync<bool>(
                "el => el.complete && el.naturalWidth > 0");
            if (loaded)
            {
                break;
            }

            await Task.Delay(200);
        }

        Assert.True(loaded, "Thumbnail <img> did not finish loading (complete && naturalWidth > 0) after toggle.");
    }

    [When("I attach a document to the list with caption {string}")]
    public async Task WhenIAttachADocument(string caption)
    {
        await ctx.Page.GetByTestId("composer-attach-button").ClickAsync();
        await ctx.Page.GetByTestId("composer-attach-document").ClickAsync();
        await ctx.Page.GetByTestId("composer-attach-file-input").SetInputFilesAsync(new FilePayload
        {
            Name = "manual.pdf",
            MimeType = "application/pdf",
            Buffer = TinyPdf,
        });
        await ctx.Page.GetByTestId("media-caption-input").FillAsync(caption);

        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.EndsWith("/items/media") && r.Request.Method == "POST" && r.Status == 201);
        await ctx.Page.GetByTestId("media-send-button").ClickAsync();
        await responseTask;
    }

    [Then("a document row appears in the list")]
    public async Task ThenDocumentRowAppears()
    {
        await Assertions.Expect(
            ctx.Page.Locator("[data-testid^='list-item-document-']").First).ToBeVisibleAsync();
    }

    [When("I open the caption editor for the document")]
    public async Task WhenIOpenTheCaptionEditorForTheDocument()
    {
        // Locate the document row, then open its per-row MoreVert menu and click edit.
        // We scope the menu-button search to the li that contains the document row to
        // avoid accidental matches on other rows.
        var documentRow = ctx.Page.Locator("[data-testid^='list-item-document-']").First;
        await documentRow.ScrollIntoViewIfNeededAsync();

        var rowLi = documentRow.Locator("xpath=ancestor::li[1]");
        var menuButton = rowLi.Locator("[data-testid^='item-menu-button-']");
        await menuButton.ScrollIntoViewIfNeededAsync();
        await menuButton.ClickAsync();

        // Wait for the Menu to be visible before clicking the edit button inside it.
        var editButton = ctx.Page.GetByTestId("edit-item-button");
        await Assertions.Expect(editButton).ToBeVisibleAsync();
        await editButton.ClickAsync();

        // Wait for any stale MUI Menu backdrop to detach so the sheet opens cleanly.
        await ctx.Page.WaitForSelectorAsync(".MuiBackdrop-root", new() { State = Microsoft.Playwright.WaitForSelectorState.Detached, Timeout = 3000 })
            .ContinueWith(_ => Task.CompletedTask);

        await Assertions.Expect(ctx.Page.GetByTestId("media-caption-sheet")).ToBeVisibleAsync();
    }

    [Then("the caption editor shows the document filename")]
    public async Task ThenCaptionEditorShowsDocumentFilename()
    {
        var fileNameEl = ctx.Page.GetByTestId("media-caption-document-name");
        await Assertions.Expect(fileNameEl).ToBeVisibleAsync();
        await Assertions.Expect(fileNameEl).ToContainTextAsync("manual.pdf");
    }
}
