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

    // Small 8x8 RGBA PNG (valid, CRC-correct — ImageSharp validates IDAT CRC).
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAYAAADED76LAAAACXBIWXMAAA7EAAAOxAGVKw4bAAAAFklEQVR4nGOpCDjxnwEPYGEgAIaHAgCvwgKw2JOr9gAAAABJRU5ErkJggg==");

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
}
