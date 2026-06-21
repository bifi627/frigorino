# List Document Attachments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a list item be a PDF **Document** — uploaded, stored, served, and opened in a new tab — reusing the image/recipe-attachment pipeline already on `stage`.

**Architecture:** The domain (`List.AddMediaItem` validating `Document`), the flat media columns, the `ListItemResponse` DTO, the `/file` serve endpoint, and reorder/toggle/soft-delete/orphan-cleanup are **already complete** for `Document`. This plan fills the only gaps: the upload slice's image-vs-document branch (a verbatim port from the recipe slice) and the frontend (enable the attach menu item, derive the type, branch the preview, add a renderer + an auth'd open hook).

**Tech Stack:** .NET 10 vertical slices (FluentResults), EF Core (Postgres), React 19 + TanStack Query/Router, MUI, hey-api generated client, xUnit + FakeItEasy (unit), Reqnroll + Playwright + Postgres Testcontainers (integration).

## Global Constraints

- **No migration.** All required columns (`StorageKey`, `ThumbnailStorageKey`, `OriginalFileName`, `ContentType`, `FileSizeBytes`) and the `ListItemType.Document` enum value already exist. Do not add a migration.
- **No domain change.** `List.AddMediaItem(type, caption, file)` already validates `Document`. Do not touch the `List` aggregate or `ListItem`.
- **PDF only (v1):** `application/pdf`. Source of truth is `ListItem.DocumentContentTypes` — do not widen it.
- **C# brace style:** always block braces `{ }`, even single-line.
- **i18n:** never assert on translated text in tests — use testids / `data-*`. New `lists.*` keys go in **both** `en` and `de` `translation.json`; the `lists` namespace is `Record<string,string>` in `i18next.d.ts`, so **no** `.d.ts` change is needed.
- **Frontend tooling:** use `npm run tsc` / `npm run lint` / `npm run build` (from `ClientApp/`), never raw `npx`.
- **The integration harness serves `ClientApp/build`**, not live source — run `npm run build` after any React change before running UI integration tests.
- **Reqnroll FQN filters** match the sanitized *Feature/Scenario title*, never the `.feature` file name; confirm the run reports the expected scenario count.

---

## Phase 1 — Backend (the one slice gap)

### Task 1: Branch `CreateMediaItem` into image vs document paths

**Files:**
- Modify: `Application/Frigorino.Features/Lists/Items/CreateMediaItem.cs` (the `Handle` method body)
- Test: `Application/Frigorino.Test/Features/CreateMediaItemSliceTests.cs`

**Interfaces:**
- Consumes: `List.AddMediaItem(ListItemType type, string? caption, StoredFile file)` (already exists — validates the `Document` content-type allowlist and the no-thumbnail invariant); `StoredFile(string StorageKey, string? ThumbnailKey, string ContentType, string OriginalFileName, long SizeBytes)`; `IFileStorage.SaveAsync(Stream, CancellationToken)`; `ListItem.ImageContentTypes` / `ListItem.DocumentContentTypes`.
- Produces: `POST .../items/media` now accepts `type=Document` with a `application/pdf` file and persists a document `ListItem` (raw blob, `ThumbnailStorageKey == null`). No signature change — the endpoint already takes `[FromForm] ListItemType type`.

- [ ] **Step 1: Add the failing document tests**

In `CreateMediaItemSliceTests.cs`, first extend the `FakeFile` helper to accept a content-type and add a single-key storage helper. Replace the existing `FakeFile` method:

```csharp
        private static IFormFile FakeFile(string name, long length, byte[]? content = null, string contentType = "image/jpeg")
        {
            var stream = new MemoryStream(content ?? Encoding.UTF8.GetBytes("raw-bytes"));
            return new FormFile(stream, 0, length, "file", name)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType,
            };
        }

        private static IFileStorage SingleKeyStorage(string key, out IFileStorage storage)
        {
            var fake = A.Fake<IFileStorage>();
            A.CallTo(() => fake.SaveAsync(A<Stream>._, A<CancellationToken>._)).Returns(key);
            storage = fake;
            return fake;
        }
```

Then add these two test methods to the class:

```csharp
        [Fact]
        public async Task Post_ValidDocument_PersistsDocumentItem_OneBlob_NoThumbnail_NoProcessing()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SingleKeyStorage("key-doc", out var storage);
            var processor = OkProcessor();

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("manual.pdf", length: 4096, contentType: "application/pdf"),
                ListItemType.Document, caption: "warranty",
                UserNamed("u1"), db, storage, processor,
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<Created<ListItemResponse>>(result.Result);

            var row = await db.ListItems.SingleAsync();
            Assert.Equal(ListItemType.Document, row.Type);
            Assert.Equal("key-doc", row.StorageKey);
            Assert.Null(row.ThumbnailStorageKey);
            Assert.Equal("application/pdf", row.ContentType);
            Assert.Equal("manual.pdf", row.OriginalFileName);
            Assert.Equal(4096, row.FileSizeBytes);
            Assert.Equal("warranty", row.Comment);

            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => processor.ProcessAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public async Task Post_DocumentWithDisallowedContentType_Returns400_SavesNoBlobs()
        {
            using var db = NewContext();
            var listId = await SeedListAsync(db, "u1", householdId: 1);
            SingleKeyStorage("key-doc", out var storage);
            var processor = OkProcessor();

            var result = await CreateMediaItemEndpoint.Handle(
                householdId: 1, listId,
                FakeFile("archive.zip", length: 4096, contentType: "application/zip"),
                ListItemType.Document, caption: null,
                UserNamed("u1"), db, storage, processor,
                NullLoggerFactory.Instance, CancellationToken.None);

            Assert.IsType<ValidationProblem>(result.Result);
            Assert.Empty(await db.ListItems.ToListAsync());
            A.CallTo(() => storage.SaveAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(() => processor.ProcessAsync(A<Stream>._, A<CancellationToken>._)).MustNotHaveHappened();
        }
```

- [ ] **Step 2: Run the new tests and confirm they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CreateMediaItemSliceTests"`
Expected: the two new tests FAIL. `Post_ValidDocument_*` fails because the current slice always runs the image processor and saves a thumbnail, so `AddMediaItem(Document, … thumbnail set)` returns a validation error (not `Created`) and the processor was called. `Post_DocumentWithDisallowedContentType_*` fails on `SaveAsync … MustNotHaveHappened` because, with no pre-filter, blobs are saved before the aggregate rejects. The five existing image tests still PASS.

- [ ] **Step 3: Implement the branch**

Replace the entire `Handle` method in `CreateMediaItem.cs` (from its signature down to the `private sealed record MediaOutcome` line, exclusive) with:

```csharp
        public static async Task<Results<Created<ListItemResponse>, NotFound, ValidationProblem, StatusCodeHttpResult>> Handle(
            int householdId,
            int listId,
            IFormFile file,
            [FromForm] ListItemType type,
            [FromForm] string? caption,
            ICurrentUserService currentUser,
            ApplicationDbContext db,
            [FromKeyedServices(BlobAreas.ListItem)] IFileStorage storage,
            IImageProcessor imageProcessor,
            ILoggerFactory loggerFactory,
            CancellationToken ct)
        {
            var logger = loggerFactory.CreateLogger(typeof(CreateMediaItemEndpoint));

            var membership = await db.FindActiveMembershipAsync(householdId, currentUser.UserId, ct);
            if (membership is null)
            {
                return TypedResults.NotFound();
            }

            var listExists = await db.Lists
                .AnyAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
            if (!listExists)
            {
                return TypedResults.NotFound();
            }

            if (file is null || file.Length <= 0)
            {
                return new Error("A file is required.").WithProperty("file").ToValidationProblemResult();
            }

            // App-level size gate — the real limit; framework defaults (Kestrel/multipart) are only the outer backstop.
            if (file.Length > ListItem.MaxFileSizeBytes)
            {
                return TypedResults.StatusCode(StatusCodes.Status413PayloadTooLarge);
            }

            // Content-type pre-filter: reject anything not allowed for the requested type BEFORE touching
            // storage. The image path is additionally guarded by the decoder; the document path has no
            // processor, so this is its only pre-storage gate (and matches the recipe attachment slice).
            var allowed = type switch
            {
                ListItemType.Image => ListItem.ImageContentTypes,
                ListItemType.Document => ListItem.DocumentContentTypes,
                _ => System.Array.Empty<string>(),
            };
            if (string.IsNullOrWhiteSpace(file.ContentType) || !allowed.Contains(file.ContentType))
            {
                return new Error($"Content type '{file.ContentType}' is not allowed for {type} items.")
                    .WithProperty("file").ToValidationProblemResult();
            }

            // Orphan-safe ordering: save blobs BEFORE the row exists; compensate on any later failure.
            // Both saves live inside the try so a throw on the SECOND save still compensates the first.
            string? storageKey = null;
            string? thumbnailKey = null;
            try
            {
                StoredFile stored;
                if (type == ListItemType.Image)
                {
                    Result<ProcessedImage> processed;
                    await using (var upload = file.OpenReadStream())
                    {
                        processed = await imageProcessor.ProcessAsync(upload, ct);
                    }
                    if (processed.IsFailed)
                    {
                        return processed.ToValidationProblem();
                    }

                    storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
                    thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);
                    stored = new StoredFile(
                        storageKey, thumbnailKey, processed.Value.ContentType,
                        file.FileName, processed.Value.FullResSizeBytes);
                }
                else
                {
                    // Document path: store the raw PDF bytes, no processing, no thumbnail.
                    await using (var upload = file.OpenReadStream())
                    {
                        storageKey = await storage.SaveAsync(upload, ct);
                    }
                    stored = new StoredFile(storageKey, null, file.ContentType, file.FileName, file.Length);
                }

                // AddMediaItem mints a Rank (append after the last unchecked item); a concurrent append
                // can collide on the partial unique index. RankRetry reloads fresh state and re-mints —
                // only the EF insert is retried; the blobs are already stored above and not re-uploaded.
                var outcome = await RankRetry.SaveWithRetryAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var freshList = await db.Lists
                        .Include(l => l.ListItems)
                        .FirstOrDefaultAsync(l => l.Id == listId && l.HouseholdId == householdId && l.IsActive, ct);
                    if (freshList is null)
                    {
                        return new MediaOutcome(null, NotFound: true, Problem: null);
                    }

                    var result = freshList.AddMediaItem(type, caption, stored);
                    if (result.IsFailed)
                    {
                        return new MediaOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                    }

                    await db.SaveChangesAsync(ct);
                    return new MediaOutcome(ListItemResponse.From(result.Value), NotFound: false, Problem: null);
                });

                if (outcome.NotFound)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                    return TypedResults.NotFound();
                }
                if (outcome.Problem is not null)
                {
                    await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                    return outcome.Problem;
                }

                var response = outcome.Response!;
                return TypedResults.Created(
                    $"/api/household/{householdId}/lists/{listId}/items/{response.Id}",
                    response);
            }
            catch
            {
                await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                throw;
            }
        }
```

Note: the initial list lookup is now `AnyAsync` (the previous `Include(l => l.ListItems)` load was dead — the aggregate is re-read fresh inside `RankRetry`). `CompensateAsync` / `DeleteQuietlyAsync` / `MediaOutcome` are unchanged.

- [ ] **Step 4: Run the slice tests and confirm all pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~CreateMediaItemSliceTests"`
Expected: all 7 tests PASS (5 existing image tests + 2 new document tests).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Features/Lists/Items/CreateMediaItem.cs Application/Frigorino.Test/Features/CreateMediaItemSliceTests.cs
git commit -m "feat(lists): document upload path in CreateMediaItem slice"
```

---

## Phase 2 — Frontend

### Task 2: Enable the document upload flow (menu, type derivation, preview)

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/components/composer/features/attachComposerFeature.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx` (the `handleSendMedia` callback, ~lines 233-249)
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/MediaPreviewSheet.tsx`

**Interfaces:**
- Consumes: `createMediaItemMutation` body `{ file, type, caption }` (the generated `type` accepts the `"Image" | "Document"` string union); the `AttachPayload` shape stays `{ file }`.
- Produces: the attach menu's "Document" item now opens a PDF picker; a picked PDF is sent as `type: "Document"` and previewed as a file card.

- [ ] **Step 1: Enable the Document menu item + PDF accept toggle**

In `attachComposerFeature.tsx`, add accept constants right after the `AttachPayload` interface (before `const AttachTrigger`):

```tsx
// One hidden input serves both photo and document picks; `accept` is toggled imperatively per choice
// (the same imperative technique already used for the camera `capture` attribute below).
const IMAGE_ACCEPT = "image/jpeg,image/png,image/webp";
const DOCUMENT_ACCEPT = "application/pdf";
```

Replace the existing `openPicker` function with this pair (sets the image accept explicitly, plus a new document picker):

```tsx
    const openPicker = (useCamera: boolean) => {
        setAnchor(null);
        const input = fileInputRef.current;
        if (!input) {
            return;
        }
        input.setAttribute("accept", IMAGE_ACCEPT);
        if (useCamera) {
            input.setAttribute("capture", "environment");
        } else {
            input.removeAttribute("capture");
        }
        input.click();
    };

    // Documents: clear the camera capture and constrain the picker to PDFs, then reuse the same input.
    const openDocumentPicker = () => {
        setAnchor(null);
        const input = fileInputRef.current;
        if (!input) {
            return;
        }
        input.removeAttribute("capture");
        input.setAttribute("accept", DOCUMENT_ACCEPT);
        input.click();
    };
```

Replace the disabled Document `MenuItem` (and its `{/* Document arrives in sub-feature #3. */}` comment) with the enabled version:

```tsx
                                    <MenuItem
                                        data-testid="composer-attach-document"
                                        onClick={openDocumentPicker}
                                    >
                                        <ListItemIcon>
                                            <Description fontSize="small" />
                                        </ListItemIcon>
                                        <ListItemText>
                                            {t("lists.attachDocument")}
                                        </ListItemText>
                                    </MenuItem>
```

(The static `accept="image/jpeg,image/png,image/webp"` on the `<input>` stays as the default; both handlers now set it explicitly.)

- [ ] **Step 2: Derive the item type from the picked file's MIME**

In `ListViewPage.tsx`, replace the `handleSendMedia` callback body's mutation call. Change:

```tsx
            try {
                await createMediaMutation.mutateAsync({
                    path: { householdId, listId: listIdNum },
                    body: {
                        file: pendingFile,
                        type: "Image",
                        caption: caption ?? undefined,
                    },
                });
```

to:

```tsx
            // The media slice validates the type against the file's content-type, so derive it from the
            // picked file's MIME: a PDF becomes a Document item, everything else an Image.
            const itemType =
                pendingFile.type === "application/pdf" ? "Document" : "Image";
            try {
                await createMediaMutation.mutateAsync({
                    path: { householdId, listId: listIdNum },
                    body: {
                        file: pendingFile,
                        type: itemType,
                        caption: caption ?? undefined,
                    },
                });
```

- [ ] **Step 3: Branch the preview sheet for PDFs**

In `MediaPreviewSheet.tsx`, update the imports — add `Description` and `Typography`:

```tsx
import { Close, Description, Send } from "@mui/icons-material";
import {
    Box,
    Button,
    CircularProgress,
    Dialog,
    DialogActions,
    DialogContent,
    DialogTitle,
    IconButton,
    TextField,
    Typography,
} from "@mui/material";
```

Add a `isPdf` flag right after the `caption` state declaration:

```tsx
    const isPdf = file?.type === "application/pdf";
```

Change the dialog title from `{t("lists.attachPhoto")}` to:

```tsx
                {isPdf ? t("lists.attachDocument") : t("lists.attachPhoto")}
```

Replace the image-preview block (the `{previewUrl ? (<Box component="img" … />) : null}` at the top of `DialogContent`) with:

```tsx
                {isPdf ? (
                    <Box
                        sx={{
                            display: "flex",
                            alignItems: "center",
                            gap: 1.5,
                            mb: 2,
                            p: 1.5,
                            borderRadius: 1,
                            bgcolor: "action.hover",
                        }}
                    >
                        <Description color="action" />
                        <Typography
                            variant="body2"
                            sx={{ wordBreak: "break-word", minWidth: 0 }}
                        >
                            {file?.name}
                        </Typography>
                    </Box>
                ) : previewUrl ? (
                    <Box
                        component="img"
                        src={previewUrl}
                        alt=""
                        sx={{
                            width: "100%",
                            maxHeight: "50vh",
                            objectFit: "contain",
                            borderRadius: 1,
                            mb: 2,
                        }}
                    />
                ) : null}
```

(The object-URL effect is left unchanged — it harmlessly creates an unused blob URL for PDFs and revokes it on cleanup; reworking that StrictMode-safe effect is not worth the risk.)

- [ ] **Step 4: Type-check and lint**

Run (from `Application/Frigorino.Web/ClientApp/`): `npm run tsc && npm run lint`
Expected: both pass with no errors.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/components/composer/features/attachComposerFeature.tsx Application/Frigorino.Web/ClientApp/src/features/lists/pages/ListViewPage.tsx Application/Frigorino.Web/ClientApp/src/features/lists/items/components/MediaPreviewSheet.tsx
git commit -m "feat(lists): document attach menu, type derivation, preview"
```

### Task 3: Document renderer + authenticated open hook

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/useOpenItemFile.ts`
- Create: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/DocumentItemRenderer.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

**Interfaces:**
- Consumes: the configured `client` from `lib/api/client.gen` (injects the Firebase bearer token); `useCurrentHousehold()` → `{ data: { householdId } }`; `ListItemResponse` (`type`, `fileName`, `comment`, `listId`, `id`).
- Produces: `useOpenItemFile(householdId, listId)` → `(itemId) => void`; `DocumentItemRenderer({ item })`; `ListItemContent` renders it for `item.type === "Document"`.

- [ ] **Step 1: Create the authenticated open hook**

Create `useOpenItemFile.ts`:

```ts
import { useCallback } from "react";
import { client } from "../../../lib/api/client.gen";

// Opens a list item's stored file (a PDF document) in a new tab. The /file endpoint requires the
// Bearer token (injected by the fetch client), so a naked link/window.open(url) would 401. Instead
// we fetch the bytes as an authenticated blob and point a tab at the resulting object URL (the
// browser renders the PDF in its native viewer). The tab is opened SYNCHRONOUSLY inside the click
// gesture, then navigated once the fetch resolves — opening after the await would be eaten by popup
// blockers. Mirrors features/recipes/attachments/useOpenRecipeAttachmentFile.ts.
export const useOpenItemFile = (householdId: number, listId: number) =>
    useCallback(
        (itemId: number) => {
            const win = window.open("", "_blank");
            void (async () => {
                try {
                    const { data, error } = await client.get({
                        url: `/api/household/${householdId}/lists/${listId}/items/${itemId}/file`,
                        parseAs: "blob",
                    });
                    if (error || !data || !win) {
                        win?.close();
                        return;
                    }
                    const objectUrl = URL.createObjectURL(data as Blob);
                    win.location.href = objectUrl;
                    setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
                } catch {
                    win?.close();
                }
            })();
        },
        [householdId, listId],
    );
```

- [ ] **Step 2: Create the document renderer**

Create `components/DocumentItemRenderer.tsx`:

```tsx
import { Description } from "@mui/icons-material";
import { Box, Typography } from "@mui/material";
import { useTranslation } from "react-i18next";
import type { ListItemResponse } from "../../../../lib/api";
import { useCurrentHousehold } from "../../../me/activeHousehold/useCurrentHousehold";
import { useOpenItemFile } from "../useOpenItemFile";

interface Props {
    item: ListItemResponse;
}

export function DocumentItemRenderer({ item }: Props) {
    const { t } = useTranslation();
    const { data: currentHousehold } = useCurrentHousehold();
    const householdId = currentHousehold?.householdId ?? 0;
    const openFile = useOpenItemFile(householdId, item.listId);

    return (
        <Box
            role="button"
            tabIndex={0}
            aria-label={t("lists.openDocument")}
            data-testid={`list-item-document-${item.id}`}
            onClick={() => openFile(item.id)}
            onKeyDown={(e) => {
                if (e.key === "Enter" || e.key === " ") {
                    e.preventDefault();
                    openFile(item.id);
                }
            }}
            sx={{
                display: "flex",
                alignItems: "center",
                gap: 1.5,
                flex: 1,
                minWidth: 0,
                cursor: "pointer",
                borderRadius: 1,
                p: 0.5,
                "&:hover": { bgcolor: "action.hover" },
            }}
        >
            <Description color="action" sx={{ flexShrink: 0 }} />
            <Box sx={{ minWidth: 0 }}>
                <Typography
                    variant="body2"
                    sx={{ wordBreak: "break-word" }}
                    data-testid={`list-item-document-${item.id}-name`}
                >
                    {item.fileName}
                </Typography>
                {item.comment ? (
                    <Typography
                        variant="caption"
                        color="text.secondary"
                        sx={{ display: "block", wordBreak: "break-word" }}
                    >
                        {item.comment}
                    </Typography>
                ) : null}
            </Box>
        </Box>
    );
}
```

- [ ] **Step 3: Wire the renderer into the switch**

Replace the contents of `ListItemContent.tsx` with:

```tsx
import type { ListItemResponse } from "../../../../lib/api";
import { DocumentItemRenderer } from "./DocumentItemRenderer";
import { ImageItemRenderer } from "./ImageItemRenderer";
import { TextItemRenderer } from "./TextItemRenderer";

interface Props {
    item: ListItemResponse;
    onEditQuantity?: () => void;
    onEditComment?: () => void;
}

// Renderer switch keyed by item.type.
export function ListItemContent({
    item,
    onEditQuantity,
    onEditComment,
}: Props) {
    if (item.type === "Image") {
        return <ImageItemRenderer item={item} />;
    }
    if (item.type === "Document") {
        return <DocumentItemRenderer item={item} />;
    }
    return (
        <TextItemRenderer
            item={item}
            onEditQuantity={onEditQuantity}
            onEditComment={onEditComment}
        />
    );
}
```

- [ ] **Step 4: Add the `lists.openDocument` i18n key (en + de)**

In `public/locales/en/translation.json`, change the last `lists` entry from:

```json
        "editCaption": "Edit caption"
```

to:

```json
        "editCaption": "Edit caption",
        "openDocument": "Open document"
```

In `public/locales/de/translation.json`, change the last `lists` entry from:

```json
        "editCaption": "Bildunterschrift bearbeiten"
```

to:

```json
        "editCaption": "Bildunterschrift bearbeiten",
        "openDocument": "Dokument öffnen"
```

- [ ] **Step 5: Type-check and lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: both pass.

- [ ] **Step 6: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/lists/items/useOpenItemFile.ts Application/Frigorino.Web/ClientApp/src/features/lists/items/components/DocumentItemRenderer.tsx Application/Frigorino.Web/ClientApp/src/features/lists/items/components/ListItemContent.tsx Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(lists): document item renderer + authed open-in-new-tab hook"
```

---

## Phase 3 — Integration tests & finalization

### Task 4: API integration test — upload + serve + no thumbnail

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.Api.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/MediaItemSteps.cs`

**Interfaces:**
- Consumes: `ctx.HouseholdId`, `ctx.ListIds[name]`, `ctx.SetListItemId` / `ctx.GetListItemId`, `ctx.LastApiResponse`; the existing `TinyPdf` field already on `TestApiClient`; `TryGetItemFileAsync` / `TryGetItemThumbnailAsync`.
- Produces: `TestApiClient.TryUploadDocumentAsync(listId, caption)`; the "Media Items API" feature gains a document scenario.

- [ ] **Step 1: Add the document upload helper**

In `TestApiClient.cs`, add this method to the "Media items" region, right after `TryUploadImageAsync` (the existing `TinyPdf` field further down in the file is accessible — C# field order doesn't matter):

```csharp
    public Task<IAPIResponse> TryUploadDocumentAsync(int listId, string caption = "", int? householdId = null)
    {
        var targetHouseholdId = householdId ?? ctx.HouseholdId;
        var form = ctx.BrowserContext.APIRequest.CreateFormData();
        form.Append("file", new FilePayload { Name = "manual.pdf", MimeType = "application/pdf", Buffer = TinyPdf });
        form.Append("type", "Document");
        form.Append("caption", caption);
        return ctx.BrowserContext.APIRequest.PostAsync(
            $"/api/household/{targetHouseholdId}/lists/{listId}/items/media",
            new APIRequestContextOptions { Headers = AuthHeaders, Multipart = form });
    }
```

- [ ] **Step 2: Add the API scenario**

Append to `MediaItems.Api.feature`:

```gherkin
  Scenario: Uploading a document stores it and serves the file without a thumbnail
    Given there is a list named "Trip"
    When I upload a document with caption "warranty" to "Trip" via the API
    Then the API response status is 201
    And the uploaded document in "Trip" serves a file with content-type "application/pdf"
    And the uploaded document in "Trip" has no thumbnail
```

- [ ] **Step 3: Add the API steps**

Add these three step methods to `MediaItemSteps.cs`:

```csharp
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
```

(`GetItemThumbnail` returns 404 when `ThumbnailStorageKey` is null — confirmed in `GetItemThumbnail.cs:46`.)

- [ ] **Step 4: Run the API media IT**

Ensure Docker Desktop is running (Testcontainers). Run:
`dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~MediaItemsAPI"`
Expected: the new document scenario plus the existing photo scenario PASS. Confirm the run reports the expected scenario count (do not trust a zero-match green).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Infrastructure/TestApiClient.cs Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.Api.feature Application/Frigorino.IntegrationTests/Slices/Lists/MediaItemSteps.cs
git commit -m "test(lists): API IT for document upload/serve/no-thumbnail"
```

### Task 5: UI integration test — attach a document, see the row

**Files:**
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.feature`
- Modify: `Application/Frigorino.IntegrationTests/Slices/Lists/MediaItemSteps.cs`

**Interfaces:**
- Consumes: testids `composer-attach-button`, `composer-attach-document`, `composer-attach-file-input`, `media-caption-input`, `media-send-button` (Task 2), and `list-item-document-{id}` (Task 3).
- Produces: a "Media Items" UI scenario covering the menu → picker → renderer path.

- [ ] **Step 1: Add the UI scenario**

Append to `MediaItems.feature`:

```gherkin
  Scenario: User attaches a document and sees it in the list
    Given there is a list named "Trip"
    When I open the list "Trip"
    And I attach a document with caption "warranty"
    Then a document row appears in the list
```

- [ ] **Step 2: Add the UI steps**

Add a PDF byte fixture and two steps to `MediaItemSteps.cs` (place the field next to the existing `TinyPng` field):

```csharp
    // Minimal valid PDF bytes — the document path stores the raw bytes (no parsing), so a header +
    // EOF marker round-trips and serves back as application/pdf.
    private static readonly byte[] TinyPdf = System.Text.Encoding.ASCII.GetBytes(
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF");

    [When("I attach a document with caption {string}")]
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
```

- [ ] **Step 3: Rebuild the SPA (the IT serves `ClientApp/build`)**

Run (from `ClientApp/`): `npm run build`
Expected: build succeeds; the new testids are now in `ClientApp/build`.

- [ ] **Step 4: Run the UI media IT**

With Docker running: `dotnet test Application/Frigorino.IntegrationTests --filter "FullyQualifiedName~MediaItems"`
Expected: the new UI scenario plus all existing Media Items (UI + API) scenarios PASS. Confirm the scenario count.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.IntegrationTests/Slices/Lists/MediaItems.feature Application/Frigorino.IntegrationTests/Slices/Lists/MediaItemSteps.cs
git commit -m "test(lists): UI IT for attaching a document"
```

### Task 6: Docs + full verification

**Files:**
- Modify: `knowledge/Lists.md`

- [ ] **Step 1: Update the knowledge doc**

In `knowledge/Lists.md`, replace the "Media items" bullet (line ~29) with:

```markdown
- **Media items.** `POST /media` takes a `type` (`Image`/`Document`) + file. **Images** re-encode to WebP + thumbnail via `IImageProcessor`; **documents** (PDF only, `application/pdf`) store the raw bytes with no thumbnail. Both store via `IFileStorage` (blob area `ListItem`) and save blobs **before** the row, compensating on failure (same orphan-safe ordering as recipe attachments). A content-type pre-filter rejects disallowed types before any blob is written. `AddMediaItem` enforces the type/thumbnail invariant (images have a thumbnail, documents don't). Documents are opened client-side in a new tab (authed blob fetch → object URL); there is no inline PDF preview. See `File_Storage.md`.
```

- [ ] **Step 2: Commit the doc**

```bash
git add knowledge/Lists.md
git commit -m "docs(knowledge): document list items live in Lists.md"
```

- [ ] **Step 3: Full backend + integration suite**

With Docker running: `dotnet test Application/Frigorino.sln`
Expected: all unit + integration tests PASS (this is the authoritative gate — covers pipeline/SPA/Dockerfile-independent behavior in one run). Capture the pass/fail summary; do not trust a piped tail exit code.

- [ ] **Step 4: Docker build (drift check)**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: image builds successfully (confirms the SPA build + publish still wire up; no new project/dependency was added, so no Dockerfile edit is expected).

- [ ] **Step 5: Final commit (if anything changed during verification)**

```bash
git status
# commit only if verification surfaced fixes
```

---

## Post-implementation (finishing the branch)

When the work is verified and ready to ship (merge to `stage`), remove the **"Rich list items: document attachments (sub-feature #3)"** entry from `IDEAS.md` as the finishing step (per the "delete tracking items when done — once their work ships" convention). This is not a coding task; do it during finishing-a-development-branch, not before.

## Self-Review

**Spec coverage** (each spec section → task):
- Backend slice gap (image/document branch + content-type pre-filter) → Task 1. ✓
- "No migration / no domain change" → Global Constraints + Task 1 touches only the slice. ✓
- Frontend: enable menu item + accept toggle → Task 2.1; derive type from MIME → Task 2.2; preview-sheet branch → Task 2.3; `useOpenItemFile` → Task 3.1; `DocumentItemRenderer` + switch wiring → Task 3.2-3.3; i18n → Task 3.4. ✓
- Data flow (upload → render → open in new tab) → Tasks 2-3, exercised by Tasks 4-5. ✓
- Error handling (400 disallowed content-type; 404 non-member/not-found; 413 oversize) → Task 1 (pre-filter + preserved guards) + Task 1 tests. ✓
- Testing (slice + API IT + UI IT) → Tasks 1, 4, 5. ✓
- Docs (knowledge/Lists.md) + IDEAS removal at ship → Task 6 + Post-implementation. ✓
- Migration: none → Global Constraints. ✓
- Dockerfile: unchanged, verified → Task 6.4. ✓

**Placeholder scan:** No TBD/TODO; every code step contains complete code; every command has expected output. ✓

**Type consistency:** `useOpenItemFile(householdId, listId)` defined in Task 3.1 and called identically in Task 3.2; `TryUploadDocumentAsync(listId, caption)` defined in Task 4.1 and called in Task 4.3; testids `composer-attach-document` / `list-item-document-{id}` produced in Tasks 2-3 and consumed in Tasks 4-5; the `__document__` item-id slot is set and read within Task 4's steps. ✓
