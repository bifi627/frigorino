namespace Frigorino.IntegrationTests.Slices.Recipes;

[Binding]
public class RecipeAttachmentSteps
{
    private readonly ScenarioContextHolder ctx;
    private readonly TestApiClient api;

    public RecipeAttachmentSteps(ScenarioContextHolder ctx, TestApiClient api)
    {
        this.ctx = ctx;
        this.api = api;
    }

    // Same valid 8x8 RGBA PNG used by the media-item steps — the image path re-encodes via Magick.NET,
    // which validates the IDAT CRC, so the bytes must be a real image.
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAAFklEQVR4nGOpCDjxnwEPYGEgAIaHAgCvwgKw2JOr9gAAAABJRU5ErkJggg==");

    // Minimal PDF — the document path stores the raw bytes as-is (no parsing).
    private static readonly byte[] TinyPdf = System.Text.Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

    // ---- Seed helpers (API) ----

    [Given("the recipe {string} has an image attachment captioned {string}")]
    public async Task GivenRecipeHasImageAttachment(string recipeName, string caption)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.RecipeAttachmentIds[caption] = await api.CreateRecipeImageAttachmentAsync(recipeId, caption);
    }

    [Given("the recipe {string} has a document attachment captioned {string}")]
    public async Task GivenRecipeHasDocumentAttachment(string recipeName, string caption)
    {
        var recipeId = ctx.RecipeIds[recipeName];
        ctx.RecipeAttachmentIds[caption] = await api.CreateRecipeDocumentAttachmentAsync(recipeId, caption);
    }

    // ---- Section expand ----

    [When("I expand the attachments section")]
    public async Task WhenIExpandTheAttachmentsSection()
    {
        // The "Sources & photos" strip lives inside the collapsed "Details" accordion on the edit
        // page — open it, then wait for the add-attachment control to become actionable.
        await ctx.Page.GetByTestId("recipe-details-accordion").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-add-attachment")).ToBeVisibleAsync();
    }

    // ---- Upload via UI ----

    [When("I attach an image with caption {string}")]
    public async Task WhenIAttachAnImageWithCaption(string caption)
    {
        await ctx.Page.GetByTestId("recipe-add-attachment").ClickAsync();
        await ctx.Page.GetByTestId("recipe-attachment-photo").ClickAsync();
        await ctx.Page.GetByTestId("recipe-attachment-file-input").SetInputFilesAsync(new FilePayload
        {
            Name = "photo.png",
            MimeType = "image/png",
            Buffer = TinyPng,
        });
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-attachment-preview-sheet")).ToBeVisibleAsync();
        await ctx.Page.GetByTestId("recipe-attachment-caption-input").FillAsync(caption);
        await SendAttachmentAndCaptureIdAsync(caption);
    }

    [When("I attach a document with caption {string}")]
    public async Task WhenIAttachADocumentWithCaption(string caption)
    {
        await ctx.Page.GetByTestId("recipe-add-attachment").ClickAsync();
        await ctx.Page.GetByTestId("recipe-attachment-document").ClickAsync();
        await ctx.Page.GetByTestId("recipe-attachment-document-input").SetInputFilesAsync(new FilePayload
        {
            Name = "sheet.pdf",
            MimeType = "application/pdf",
            Buffer = TinyPdf,
        });
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-attachment-document-preview")).ToBeVisibleAsync();
        await ctx.Page.GetByTestId("recipe-attachment-caption-input").FillAsync(caption);
        await SendAttachmentAndCaptureIdAsync(caption);
    }

    // Clicks Send, awaits the POST 201, and records the new attachment id so later steps can target
    // its row/tile by testid (the id is part of every attachment testid).
    private async Task SendAttachmentAndCaptureIdAsync(string caption)
    {
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/attachments")
            && r.Request.Method == "POST"
            && r.Status == 201);
        await ctx.Page.GetByTestId("recipe-attachment-send-button").ClickAsync();
        var response = await responseTask;
        var json = await response.JsonAsync();
        ctx.RecipeAttachmentIds[caption] = json!.Value.GetProperty("id").GetInt32();
    }

    // ---- Caption edit ----

    [When("I edit the attachment captioned {string} to {string}")]
    public async Task WhenIEditTheAttachmentCaption(string oldCaption, string newCaption)
    {
        var id = ctx.RecipeAttachmentIds[oldCaption];
        await ctx.Page.GetByTestId($"recipe-attachment-{id}-edit").ClickAsync();
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-attachment-caption-sheet")).ToBeVisibleAsync();
        await ctx.Page.GetByTestId("recipe-attachment-caption-edit-input").FillAsync(newCaption);

        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/attachments/")
            && r.Request.Method == "PUT"
            && r.Status == 200);
        await ctx.Page.GetByTestId("recipe-attachment-caption-save-button").ClickAsync();
        await responseTask;
    }

    // ---- Reorder via drag ----

    [When("I drag the attachment captioned {string} above {string}")]
    public async Task WhenIDragTheAttachmentAbove(string sourceCaption, string targetCaption)
    {
        // dnd-kit PointerSensor has activationConstraint { distance: 8 }, so a real drag must move
        // >8px after mouse-down before activation.
        var sourceHandle = ctx.Page.GetByTestId($"recipe-link-drag-handle-{ctx.RecipeAttachmentIds[sourceCaption]}");
        var targetHandle = ctx.Page.GetByTestId($"recipe-link-drag-handle-{ctx.RecipeAttachmentIds[targetCaption]}");
        await sourceHandle.WaitForAsync();
        await targetHandle.WaitForAsync();

        // The attachments section sits near the bottom of the long edit page — below the 1280x720
        // viewport fold. Raw Mouse.Move addresses viewport coordinates and does not auto-scroll, so
        // the handles must be scrolled into view first or the pointer never hovers the target.
        await sourceHandle.ScrollIntoViewIfNeededAsync();
        await targetHandle.ScrollIntoViewIfNeededAsync();

        var sourceBox = await sourceHandle.BoundingBoxAsync()
            ?? throw new Exception("Source drag handle has no bounding box.");
        var targetBox = await targetHandle.BoundingBoxAsync()
            ?? throw new Exception("Target drag handle has no bounding box.");

        var sx = (float)(sourceBox.X + sourceBox.Width / 2);
        var sy = (float)(sourceBox.Y + sourceBox.Height / 2);
        var tx = (float)(targetBox.X + targetBox.Width / 2);
        var ty = (float)(targetBox.Y + targetBox.Height / 2);

        // handleDragEnd fires the reorder PATCH on mouse-up; register the wait before releasing.
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains("/attachments/")
            && r.Url.Contains("/reorder")
            && r.Request.Method == "PATCH");

        // dnd-kit's collision detection runs per pointer-move against droppable rects that are only
        // measured after the activation triggers an onDragStart re-render. Mouse.Move(Steps=N) emits
        // its sub-moves back-to-back with no scheduler gap, so they can all land before that render
        // commits — collision finds no droppable, `over` stays null, and mouse-up yields no reorder
        // (a hard 30s flake). The fix, verified against the live SPA, is to space the moves out with
        // a real pause between each so dnd-kit's RAF-driven measurement runs between them.
        await ctx.Page.Mouse.MoveAsync(sx, sy);
        await ctx.Page.Mouse.DownAsync();
        await ctx.Page.Mouse.MoveAsync(sx, sy + 12); // cross the 8px activation distance
        await ctx.Page.WaitForTimeoutAsync(100); // let onDragStart commit + measure droppables

        const int steps = 10;
        for (var i = 1; i <= steps; i++)
        {
            var mx = sx + (tx - sx) * i / steps;
            var my = (sy + 12) + (ty - (sy + 12)) * i / steps;
            await ctx.Page.Mouse.MoveAsync(mx, my);
            await ctx.Page.WaitForTimeoutAsync(20);
        }

        await ctx.Page.Mouse.MoveAsync(tx, ty);
        await ctx.Page.WaitForTimeoutAsync(100); // let the final over-target collision settle
        await ctx.Page.Mouse.UpAsync();
        await responseTask;
    }

    // ---- Delete ----

    [When("I delete the attachment captioned {string}")]
    public async Task WhenIDeleteTheAttachment(string caption)
    {
        var id = ctx.RecipeAttachmentIds[caption];
        var responseTask = ctx.Page.WaitForResponseAsync(r =>
            r.Url.Contains($"/attachments/{id}")
            && r.Request.Method == "DELETE"
            && r.Status == 204);
        await ctx.Page.GetByTestId($"recipe-attachment-{id}-delete").ClickAsync();
        await responseTask;
    }

    // ---- View page interactions ----

    [When("I open the attachment tile captioned {string}")]
    public async Task WhenIOpenTheAttachmentTile(string caption)
    {
        var id = ctx.RecipeAttachmentIds[caption];
        await ctx.Page.GetByTestId($"recipe-attachment-{id}").ClickAsync();
    }

    // ---- Assertions ----

    [Then("the attachments list shows an attachment captioned {string}")]
    public async Task ThenTheAttachmentsListShowsCaptioned(string caption)
    {
        // Match the caption by its (user-entered, non-translated) text within the section's caption
        // labels, so this works for both freshly-uploaded and edited attachments without an id.
        await Assertions.Expect(
            ctx.Page.GetByTestId("recipe-section-attachments-content")
                .Locator("[data-testid$='-caption']")
                .Filter(new LocatorFilterOptions { HasText = caption })).ToBeVisibleAsync();
    }

    [Then("the first attachment is captioned {string}")]
    public async Task ThenTheFirstAttachmentIsCaptioned(string caption)
    {
        var firstCaption = ctx.Page.GetByTestId("recipe-section-attachments-content")
            .Locator("[data-testid$='-caption']").First;
        await Assertions.Expect(firstCaption).ToContainTextAsync(caption);
    }

    [Then("the attachment captioned {string} is no longer listed")]
    public async Task ThenTheAttachmentIsNoLongerListed(string caption)
    {
        var id = ctx.RecipeAttachmentIds[caption];
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-attachment-row-{id}")).Not.ToBeVisibleAsync();
    }

    [Then("the attachment tile captioned {string} is shown")]
    public async Task ThenTheAttachmentTileIsShown(string caption)
    {
        var id = ctx.RecipeAttachmentIds[caption];
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-view-attachments")).ToBeVisibleAsync();
        await Assertions.Expect(ctx.Page.GetByTestId($"recipe-attachment-{id}")).ToBeVisibleAsync();
    }

    [Then("the attachment lightbox shows the full-resolution image")]
    public async Task ThenTheLightboxShowsTheImage()
    {
        await Assertions.Expect(ctx.Page.GetByTestId("recipe-attachment-lightbox")).ToBeVisibleAsync();

        // A revoked/broken object URL still renders a "visible" <img>, so assert the image actually
        // decoded (complete && naturalWidth > 0) — this confirms the /file endpoint served real bytes.
        var img = ctx.Page.GetByTestId("recipe-attachment-lightbox").Locator("img");
        await img.WaitForAsync();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        var loaded = false;
        while (DateTime.UtcNow < deadline)
        {
            loaded = await img.EvaluateAsync<bool>("el => el.complete && el.naturalWidth > 0");
            if (loaded)
            {
                break;
            }

            await Task.Delay(200);
        }

        Assert.True(loaded, "Lightbox <img> did not finish loading (complete && naturalWidth > 0).");
    }
}
