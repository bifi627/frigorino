# Recipe Document (PDF) Attachments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a recipe carry PDF document attachments alongside images, mirroring the shipped image-attachment feature recipe-specifically.

**Architecture:** Add an `AttachmentType` discriminator to the existing flat `RecipeAttachment` table; documents store the raw PDF blob with no thumbnail. The single multipart create endpoint routes by input content-type (image → process to WebP+thumbnail; PDF → save raw). The frontend branches on `attachment.type`: documents render an icon + filename (no thumbnail fetch) and open in a new tab via an authenticated blob fetch instead of the lightbox.

**Tech Stack:** .NET 10 / EF Core (Postgres) / FluentResults vertical slices; React 19 + MUI + TanStack Query; hey-api generated client.

**Spec:** `docs/superpowers/specs/2026-06-16-recipe-document-attachments-design.md`

---

## File Structure

**Backend**
- Modify `Application/Frigorino.Domain/Entities/RecipeAttachment.cs` — add `AttachmentType` enum, `Type` field, `DocumentContentTypes`.
- Modify `Application/Frigorino.Domain/Entities/Recipe.cs` — `AddDocumentAttachment` + `ValidateAttachmentDocument`; stamp `Type=Image` in `AddAttachment`.
- Modify `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeAttachmentConfiguration.cs` — map `Type`.
- Create migration `AddRecipeAttachmentType` under `Application/Frigorino.Infrastructure/Migrations/`.
- Modify `Application/Frigorino.Features/Recipes/Attachments/RecipeAttachmentResponse.cs` — add `Type`.
- Modify `Application/Frigorino.Features/Recipes/Attachments/CreateRecipeAttachment.cs` — content-type routing.
- Modify `Application/Frigorino.Test/Recipes/RecipeAttachmentAggregateTests.cs` — document tests.

**Frontend** (`Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/`)
- Modify `components/RecipeAttachmentsSection.tsx` — enable Document menu item + PDF picker.
- Modify `components/RecipeAttachmentPreviewSheet.tsx` — document preview branch.
- Modify `components/RecipeAttachmentRow.tsx` — document row branch.
- Modify `components/RecipeAttachmentCaptionSheet.tsx` — document preview branch.
- Create `useOpenRecipeAttachmentFile.ts` — authenticated blob → new tab.
- Modify `components/RecipeViewAttachments.tsx` — document tile + open-in-tab.
- Modify `public/locales/{en,de}/translation.json` — `recipes.attachDocumentTitle`, `recipes.openDocument`.
- Regenerate `src/lib/api/**` via `npm run api`.

---

## Phase A — Backend domain + persistence

### Task 1: Add the `Type` discriminator (entity + EF config + migration)

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/RecipeAttachment.cs`
- Modify: `Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeAttachmentConfiguration.cs`
- Create: migration via CLI

- [ ] **Step 1: Add the enum, `Type` field, and `DocumentContentTypes` to the entity**

In `RecipeAttachment.cs`, add the enum above the class (same namespace `Frigorino.Domain.Entities`):

```csharp
namespace Frigorino.Domain.Entities
{
    // Discriminates the two kinds of recipe attachment. Stored as int (Image=0, Document=1);
    // serialized as its string name on the wire via the global JsonStringEnumConverter.
    public enum AttachmentType
    {
        Image = 0,
        Document = 1,
    }
```

Inside the class, after the `ImageContentTypes` field, add the document allowlist:

```csharp
        // Accepted document content types (stored as-is — no re-encoding, no thumbnail).
        public static readonly string[] DocumentContentTypes = ["application/pdf"];
```

And add the `Type` property next to the other columns (after `Caption`):

```csharp
        public AttachmentType Type { get; set; } = AttachmentType.Image;
```

- [ ] **Step 2: Map `Type` in the EF configuration**

In `RecipeAttachmentConfiguration.cs`, add after the `Caption` line (line 18):

```csharp
            builder.Property(a => a.Type).IsRequired();
```

(No `HasMaxLength` — it's an int column. EF stores the enum as int by default.)

- [ ] **Step 3: Build, then create the migration**

Run:
```bash
dotnet build Application/Frigorino.sln
dotnet ef migrations add AddRecipeAttachmentType --project Application/Frigorino.Infrastructure --startup-project Application/Frigorino.Web
```
Expected: build succeeds; a new `*_AddRecipeAttachmentType.cs` migration is generated.

- [ ] **Step 4: Verify the migration adds a defaulted non-null column**

Open the generated migration. The `Up` must contain an `AddColumn<int>` for `Type` on `RecipeAttachments` with `nullable: false` and `defaultValue: 0`. If EF emitted `defaultValue: 0` it implicitly backfills existing rows to `Image` — correct. Confirm `Down` drops the column. No manual edit needed unless the default is missing (add `defaultValue: 0` if so).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/RecipeAttachment.cs Application/Frigorino.Infrastructure/EntityFramework/Configurations/RecipeAttachmentConfiguration.cs Application/Frigorino.Infrastructure/Migrations/
git commit -m "feat(recipes): add Type discriminator to RecipeAttachment"
```

---

### Task 2: `AddDocumentAttachment` aggregate method + document validation

**Files:**
- Modify: `Application/Frigorino.Domain/Entities/Recipe.cs`
- Test: `Application/Frigorino.Test/Recipes/RecipeAttachmentAggregateTests.cs`

- [ ] **Step 1: Write the failing tests**

In `RecipeAttachmentAggregateTests.cs`, add a document-file helper next to `ValidFile()`:

```csharp
        // Valid stored document: a PDF with no thumbnail (documents never have one).
        private static StoredFile ValidDocFile() =>
            new("doc-key", null, "application/pdf", "recipe-card.pdf", 4096);
```

Then add these tests:

```csharp
        [Fact]
        public void AddAttachment_ValidImage_StampsImageType()
        {
            var recipe = NewRecipe();
            var a = recipe.AddAttachment(null, ValidFile()).Value;
            Assert.Equal(AttachmentType.Image, a.Type);
        }

        [Fact]
        public void AddDocumentAttachment_ValidPdf_SetsColumnsAndType()
        {
            var recipe = NewRecipe();

            var result = recipe.AddDocumentAttachment("scan", ValidDocFile());

            Assert.True(result.IsSuccess);
            var a = result.Value;
            Assert.Equal(AttachmentType.Document, a.Type);
            Assert.Equal("doc-key", a.StorageKey);
            Assert.Null(a.ThumbnailStorageKey);
            Assert.Equal("application/pdf", a.ContentType);
            Assert.Equal("recipe-card.pdf", a.OriginalFileName);
            Assert.Equal(4096, a.FileSizeBytes);
            Assert.Equal("scan", a.Caption);
            Assert.True(a.IsActive);
            Assert.NotEmpty(a.Rank);
            Assert.Single(recipe.Attachments);
        }

        [Fact]
        public void AddDocumentAttachment_WrongContentType_FailsKeyedOnContentType()
        {
            var recipe = NewRecipe();
            var notPdf = new StoredFile("doc-key", null, "image/webp", "x.webp", 4096);

            var result = recipe.AddDocumentAttachment(null, notPdf);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ContentType)));
        }

        [Fact]
        public void AddDocumentAttachment_WithThumbnail_FailsKeyedOnThumbnail()
        {
            var recipe = NewRecipe();
            var withThumb = new StoredFile("doc-key", "thumb-key", "application/pdf", "x.pdf", 4096);

            var result = recipe.AddDocumentAttachment(null, withThumb);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.ThumbnailStorageKey)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(RecipeAttachment.MaxFileSizeBytes + 1)]
        public void AddDocumentAttachment_BadSize_FailsKeyedOnFileSize(long size)
        {
            var recipe = NewRecipe();
            var bad = new StoredFile("doc-key", null, "application/pdf", "x.pdf", size);

            var result = recipe.AddDocumentAttachment(null, bad);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.FileSizeBytes)));
        }

        [Fact]
        public void AddDocumentAttachment_MissingStorageKey_FailsKeyedOnStorageKey()
        {
            var recipe = NewRecipe();
            var noKey = new StoredFile("  ", null, "application/pdf", "x.pdf", 4096);

            var result = recipe.AddDocumentAttachment(null, noKey);

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.StorageKey)));
        }

        [Fact]
        public void AddDocumentAttachment_CaptionTooLong_FailsKeyedOnCaption()
        {
            var recipe = NewRecipe();
            var caption = new string('x', RecipeAttachment.CaptionMaxLength + 1);

            var result = recipe.AddDocumentAttachment(caption, ValidDocFile());

            Assert.True(result.IsFailed);
            Assert.True(HasProperty(result, nameof(RecipeAttachment.Caption)));
        }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAttachmentAggregateTests"`
Expected: FAIL — `AddDocumentAttachment` does not exist (compile error) and `AttachmentType` not stamped.

- [ ] **Step 3: Implement `AddDocumentAttachment` + `ValidateAttachmentDocument`, stamp Image type**

In `Recipe.cs`, in `AddAttachment` (around line 598) add `Type = AttachmentType.Image,` to the object initializer (place it next to `IsActive = true,`).

Add the new method directly after `AddAttachment` (after line 614):

```csharp
        public Result<RecipeAttachment> AddDocumentAttachment(string? caption, StoredFile file)
        {
            var errors = ValidateAttachmentDocument(file);
            errors.AddRange(ValidateCaption(caption));
            if (errors.Count > 0)
            {
                return Result.Fail<RecipeAttachment>(errors);
            }

            var now = DateTime.UtcNow;
            var attachment = new RecipeAttachment
            {
                RecipeId = Id,
                StorageKey = file.StorageKey,
                ThumbnailStorageKey = null,
                ContentType = file.ContentType,
                OriginalFileName = file.OriginalFileName,
                FileSizeBytes = file.SizeBytes,
                Caption = NormalizeCaption(caption),
                Rank = ComputeAppendAttachmentRank(),
                Type = AttachmentType.Document,
                CreatedAt = now,
                UpdatedAt = now,
                IsActive = true,
            };
            Attachments.Add(attachment);
            return Result.Ok(attachment);
        }
```

Add the validator next to `ValidateAttachmentImage` (after line 757):

```csharp
        private static List<IError> ValidateAttachmentDocument(StoredFile file)
        {
            var errors = new List<IError>();

            if (!RecipeAttachment.DocumentContentTypes.Contains(file.ContentType))
            {
                errors.Add(new Error($"Stored content type '{file.ContentType}' is not an allowed document type.")
                    .WithMetadata("Property", nameof(RecipeAttachment.ContentType)));
            }
            if (string.IsNullOrWhiteSpace(file.StorageKey) || file.StorageKey.Length > RecipeAttachment.StorageKeyMaxLength)
            {
                errors.Add(new Error($"Storage key is required and must be {RecipeAttachment.StorageKeyMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeAttachment.StorageKey)));
            }
            if (file.ThumbnailKey is not null)
            {
                errors.Add(new Error("Document attachments must not have a thumbnail key.")
                    .WithMetadata("Property", nameof(RecipeAttachment.ThumbnailStorageKey)));
            }
            if (!string.IsNullOrEmpty(file.OriginalFileName) && file.OriginalFileName.Length > RecipeAttachment.OriginalFileNameMaxLength)
            {
                errors.Add(new Error($"File name must be {RecipeAttachment.OriginalFileNameMaxLength} characters or fewer.")
                    .WithMetadata("Property", nameof(RecipeAttachment.OriginalFileName)));
            }
            if (file.SizeBytes <= 0 || file.SizeBytes > RecipeAttachment.MaxFileSizeBytes)
            {
                errors.Add(new Error($"File size must be between 1 and {RecipeAttachment.MaxFileSizeBytes} bytes.")
                    .WithMetadata("Property", nameof(RecipeAttachment.FileSizeBytes)));
            }
            return errors;
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~RecipeAttachmentAggregateTests"`
Expected: PASS (all attachment aggregate tests, old + new).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Domain/Entities/Recipe.cs Application/Frigorino.Test/Recipes/RecipeAttachmentAggregateTests.cs
git commit -m "feat(recipes): AddDocumentAttachment aggregate method + validation"
```

---

## Phase B — API

### Task 3: Add `Type` to `RecipeAttachmentResponse`

**Files:**
- Modify: `Application/Frigorino.Features/Recipes/Attachments/RecipeAttachmentResponse.cs`

- [ ] **Step 1: Add the `Type` field to the record, factory, and projection**

Replace the record body so `Type` is included:

```csharp
using System.Linq.Expressions;
using Frigorino.Domain.Entities;

namespace Frigorino.Features.Recipes.Attachments
{
    // Storage keys are deliberately NOT exposed — the client fetches /file and /thumbnail.
    public sealed record RecipeAttachmentResponse(
        int Id,
        int RecipeId,
        AttachmentType Type,
        string ContentType,
        string? OriginalFileName,
        long FileSizeBytes,
        string? Caption,
        string Rank,
        DateTime CreatedAt,
        DateTime UpdatedAt)
    {
        public static RecipeAttachmentResponse From(RecipeAttachment a)
            => new(a.Id, a.RecipeId, a.Type, a.ContentType, a.OriginalFileName, a.FileSizeBytes, a.Caption, a.Rank, a.CreatedAt, a.UpdatedAt);

        public static readonly Expression<Func<RecipeAttachment, RecipeAttachmentResponse>> ToProjection = a =>
            new RecipeAttachmentResponse(a.Id, a.RecipeId, a.Type, a.ContentType, a.OriginalFileName, a.FileSizeBytes, a.Caption, a.Rank, a.CreatedAt, a.UpdatedAt);
    }
}
```

- [ ] **Step 2: Build to verify**

Run: `dotnet build Application/Frigorino.Features`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Attachments/RecipeAttachmentResponse.cs
git commit -m "feat(recipes): expose attachment Type in response DTO"
```

---

### Task 4: Content-type routing in `CreateRecipeAttachment`

**Files:**
- Modify: `Application/Frigorino.Features/Recipes/Attachments/CreateRecipeAttachment.cs`

- [ ] **Step 1: Replace the body of `Handle` from the content-type check onward**

Keep the method signature, the membership/recipe-existence checks, the file-null check, and the size gate (lines 47–64) **unchanged**. Replace everything from the input content-type allowlist (current line 66) through the end of the `try`/`catch` with the routed version below. This determines the attachment kind, produces the blob(s) + a `StoredFile` + the matching aggregate call per type, then runs the shared save-before-persist + compensate flow.

```csharp
            var isImage = RecipeAttachment.ImageContentTypes.Contains(file.ContentType);
            var isDocument = RecipeAttachment.DocumentContentTypes.Contains(file.ContentType);
            if (!isImage && !isDocument)
            {
                return new Error($"Content type '{file.ContentType}' is not an allowed type.")
                    .WithProperty("file").ToValidationProblemResult();
            }

            // Orphan-safe ordering: save blobs BEFORE the row exists; compensate on any later failure.
            string? storageKey = null;
            string? thumbnailKey = null;
            try
            {
                StoredFile stored;
                Func<Recipe, Result<RecipeAttachment>> addToRecipe;

                if (isImage)
                {
                    Result<ProcessedImage> processed;
                    await using (var upload = file.OpenReadStream())
                    {
                        processed = await imageProcessor.ProcessAsync(upload, ct);
                    }
                    if (processed.IsFailed) return processed.ToValidationProblem();

                    storageKey = await storage.SaveAsync(new MemoryStream(processed.Value.FullRes), ct);
                    thumbnailKey = await storage.SaveAsync(new MemoryStream(processed.Value.Thumbnail), ct);
                    stored = new StoredFile(
                        storageKey, thumbnailKey, processed.Value.ContentType,
                        file.FileName, processed.Value.FullResSizeBytes);
                    addToRecipe = recipe => recipe.AddAttachment(caption, stored);
                }
                else
                {
                    // Document path: store the raw PDF bytes, no processing, no thumbnail.
                    await using (var upload = file.OpenReadStream())
                    {
                        storageKey = await storage.SaveAsync(upload, ct);
                    }
                    stored = new StoredFile(storageKey, null, file.ContentType, file.FileName, file.Length);
                    addToRecipe = recipe => recipe.AddDocumentAttachment(caption, stored);
                }

                var outcome = await RankRetry.SaveWithRetryAsync(async () =>
                {
                    db.ChangeTracker.Clear();

                    var recipe = await db.Recipes
                        .Include(r => r.Attachments)
                        .FirstOrDefaultAsync(r => r.Id == recipeId && r.HouseholdId == householdId && r.IsActive, ct);
                    if (recipe is null)
                    {
                        return new CreateOutcome(null, NotFound: true, Problem: null);
                    }

                    var result = addToRecipe(recipe);
                    if (result.IsFailed)
                    {
                        return new CreateOutcome(null, NotFound: false, Problem: result.ToValidationProblem());
                    }

                    await db.SaveChangesAsync(ct);
                    return new CreateOutcome(RecipeAttachmentResponse.From(result.Value), NotFound: false, Problem: null);
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
                    $"/api/household/{householdId}/recipes/{recipeId}/attachments/{response.Id}", response);
            }
            catch
            {
                await CompensateAsync(storage, storageKey, thumbnailKey, logger);
                throw;
            }
```

Add `using Frigorino.Domain.Entities;` if not already present (needed for `Recipe`). The `CompensateAsync`/`DeleteQuietlyAsync`/`CreateOutcome` helpers below the method are unchanged.

- [ ] **Step 2: Build to verify**

Run: `dotnet build Application/Frigorino.sln`
Expected: PASS.

- [ ] **Step 3: Run the full unit suite (no regressions)**

Run: `dotnet test Application/Frigorino.Test`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Features/Recipes/Attachments/CreateRecipeAttachment.cs
git commit -m "feat(recipes): route attachment upload by content-type (image vs PDF)"
```

---

### Task 5: Regenerate the TypeScript client

**Files:**
- Modify (generated): `Application/Frigorino.Web/ClientApp/src/lib/api/**`, `src/lib/openapi.json`

- [ ] **Step 1: Regenerate**

Run (from `Application/Frigorino.Web/ClientApp/`):
```bash
npm run api
```
Expected: rebuilds the backend, emits `openapi.json`, regenerates `src/lib/api`. `RecipeAttachmentResponse` now has `type: "Image" | "Document"`.

- [ ] **Step 2: Verify the type union landed**

Run (from `ClientApp/`): `npm run tsc`
Expected: PASS. Confirm `grep -r "Document" src/lib/api/types.gen.ts` shows the `type` union on the attachment response.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/lib/api Application/Frigorino.Web/ClientApp/src/lib/openapi.json
git commit -m "chore(api): regenerate client for attachment Type"
```

---

## Phase C — Frontend

> Document detection on the wire is `attachment.type === "Document"`. On a freshly-picked `File`, it's `file.type === "application/pdf"`.

### Task 6: Enable the Document menu item + PDF picker

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentsSection.tsx`

- [ ] **Step 1: Add a document file-input ref and picker handler**

After `const fileInputRef = useRef<HTMLInputElement>(null);` (line 57) add:

```tsx
    const documentInputRef = useRef<HTMLInputElement>(null);
```

After the `openPicker` function (ends line 125) add:

```tsx
    const openDocumentPicker = () => {
        setMenuAnchor(null);
        documentInputRef.current?.click();
    };
```

- [ ] **Step 2: Enable the Document menu item**

Replace the disabled Document `MenuItem` (lines 245–256) with:

```tsx
                                            <MenuItem
                                                data-testid="recipe-attachment-document"
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

- [ ] **Step 3: Add the hidden PDF input**

After the existing hidden image `<input>` (lines 264–272) add:

```tsx
                    <input
                        ref={documentInputRef}
                        type="file"
                        accept="application/pdf"
                        hidden
                        onChange={handlePick}
                        data-testid="recipe-attachment-document-input"
                    />
```

`handlePick` is content-agnostic (it just sets `pendingFile`), so the PDF flows through the same preview → upload path.

- [ ] **Step 4: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentsSection.tsx
git commit -m "feat(recipes): enable document attachment picker"
```

---

### Task 7: Document branch in the preview sheet

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentPreviewSheet.tsx`

- [ ] **Step 1: Detect PDF and skip the image object URL; render an icon block**

Add `Description` and `Stack`, `Typography` to the MUI imports and `Description` to the icon import:

```tsx
import { Close, Description, Send } from "@mui/icons-material";
```
Add `Stack` and `Typography` to the `@mui/material` import list.

After `const [caption, setCaption] = useState("");` add:

```tsx
    const isDocument = file?.type === "application/pdf";
```

Change the object-URL effect guard so it never runs for documents — replace the `if (!file)` block (lines 44–47) with:

```tsx
        if (!file || isDocument) {
            // eslint-disable-next-line react-hooks/set-state-in-effect
            setPreviewUrl(null);
            return;
        }
```
and add `isDocument` to the effect dependency array: `}, [file, isDocument]);`

Change the dialog title (line 71) to pick the right label:

```tsx
                {isDocument
                    ? t("recipes.attachDocumentTitle")
                    : t("recipes.attachImageTitle")}
```

Replace the preview `{previewUrl ? (...) : null}` block (lines 81–94) with one that shows the icon + filename for documents:

```tsx
                {isDocument ? (
                    <Stack
                        direction="row"
                        spacing={1}
                        sx={{ alignItems: "center", mb: 2 }}
                        data-testid="recipe-attachment-document-preview"
                    >
                        <Description color="action" />
                        <Typography
                            variant="body2"
                            sx={{ wordBreak: "break-word" }}
                        >
                            {file?.name}
                        </Typography>
                    </Stack>
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

- [ ] **Step 2: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentPreviewSheet.tsx
git commit -m "feat(recipes): document preview in attachment picker sheet"
```

---

### Task 8: Document branch in the edit row

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentRow.tsx`

- [ ] **Step 1: Gate the thumbnail fetch and render an icon for documents**

Add `Description` to the icon import:

```tsx
import { BrokenImage, Delete, Description } from "@mui/icons-material";
```

After `const { t } = useTranslation();` add:

```tsx
    const isDocument = attachment.type === "Document";
```

Pass `enabled` to the image hook so documents never fetch — change the `useAttachmentImage` call (lines 29–33) to:

```tsx
    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(
        householdId,
        recipeId,
        attachment.id,
        "thumbnail",
        !isDocument,
    );
```

Inside the thumbnail `<Box>`, replace the `{isLoading ? ... : isError || !url ? ... : (...)}` content (lines 80–99) with a document-first branch:

```tsx
                    {isDocument ? (
                        <Description color="action" />
                    ) : isLoading ? (
                        <Skeleton
                            variant="rectangular"
                            width={THUMB_SIZE}
                            height={THUMB_SIZE}
                        />
                    ) : isError || !url ? (
                        <BrokenImage fontSize="small" color="disabled" />
                    ) : (
                        <Box
                            component="img"
                            src={url}
                            alt={attachment.caption ?? ""}
                            sx={{
                                width: "100%",
                                height: "100%",
                                objectFit: "cover",
                            }}
                        />
                    )}
```

Change the caption `<Typography>` text (line 114) so documents show the filename when there's no caption:

```tsx
                    {attachment.caption ||
                        (isDocument
                            ? attachment.originalFileName
                            : null) ||
                        t("recipes.attachmentCaptionPlaceholder")}
```

- [ ] **Step 2: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentRow.tsx
git commit -m "feat(recipes): document row rendering (icon + filename)"
```

---

### Task 9: Document branch in the caption sheet

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentCaptionSheet.tsx`

- [ ] **Step 1: Gate the thumbnail fetch and render an icon for documents**

Add `Description` to the icon import:

```tsx
import { BrokenImage, Close, Description, Save } from "@mui/icons-material";
```

After `const [caption, setCaption] = useState(attachment?.caption ?? "");` add:

```tsx
    const isDocument = attachment?.type === "Document";
```

Change the `useAttachmentImage` `enabled` argument (line 56) from `Boolean(attachment)` to:

```tsx
        Boolean(attachment) && !isDocument,
```

Inside the preview `<Box>` (lines 97–116), replace the `{isLoading ? ... : ...}` content with a document-first branch:

```tsx
                    {isDocument ? (
                        <Description color="action" fontSize="large" />
                    ) : isLoading ? (
                        <Skeleton
                            variant="rectangular"
                            width="100%"
                            height="100%"
                        />
                    ) : isError || !url ? (
                        <BrokenImage color="disabled" />
                    ) : (
                        <Box
                            component="img"
                            src={url}
                            alt=""
                            sx={{
                                width: "100%",
                                height: "100%",
                                objectFit: "contain",
                            }}
                        />
                    )}
```

- [ ] **Step 2: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeAttachmentCaptionSheet.tsx
git commit -m "feat(recipes): document icon in attachment caption sheet"
```

---

### Task 10: Open-in-tab hook + document tile on the view page

**Files:**
- Create: `Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/useOpenRecipeAttachmentFile.ts`
- Modify: `Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeViewAttachments.tsx`

- [ ] **Step 1: Create the open-in-tab hook**

Create `useOpenRecipeAttachmentFile.ts`:

```tsx
import { useCallback } from "react";
import { client } from "../../../lib/api/client.gen";

// Opens a recipe attachment's file in a new tab. The /file endpoint requires the Bearer token (injected
// by the fetch client), so a naked link/window.open(url) would 401. Instead we fetch the bytes as an
// authenticated blob and point a tab at the resulting object URL (the browser renders by MIME type).
// The tab is opened SYNCHRONOUSLY inside the click gesture, then navigated once the fetch resolves —
// opening after the await would be eaten by popup blockers.
export const useOpenRecipeAttachmentFile = (
    householdId: number,
    recipeId: number,
) =>
    useCallback(
        (attachmentId: number) => {
            const win = window.open("", "_blank");
            void (async () => {
                try {
                    const { data, error } = await client.get({
                        url: `/api/household/${householdId}/recipes/${recipeId}/attachments/${attachmentId}/file`,
                        parseAs: "blob",
                    });
                    if (error || !data || !win) {
                        win?.close();
                        return;
                    }
                    const objectUrl = URL.createObjectURL(data as Blob);
                    win.location.href = objectUrl;
                    // Revoke once the tab has loaded the blob.
                    setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
                } catch {
                    win?.close();
                }
            })();
        },
        [householdId, recipeId],
    );
```

- [ ] **Step 2: Render a document tile and wire open-in-tab**

In `RecipeViewAttachments.tsx`:

Add icon + hook imports:
```tsx
import { BrokenImage, Description } from "@mui/icons-material";
```
```tsx
import { useOpenRecipeAttachmentFile } from "../useOpenRecipeAttachmentFile";
```

Extend the `Tile` so it knows whether it's a document and skips the image fetch. Change the `Tile` props to add `isDocument` and replace the `useAttachmentImage` call + the inner render branch:

In `Tile`, after destructuring props add:
```tsx
    const isDocument = attachment.type === "Document";
```
Change the hook call to gate it:
```tsx
    const {
        data: url,
        isLoading,
        isError,
    } = useAttachmentImage(
        householdId,
        recipeId,
        attachment.id,
        "thumbnail",
        !isDocument,
    );
```
Replace the inner `{isLoading ? ... : ...}` of the tile box with a document-first branch:
```tsx
                {isDocument ? (
                    <Description color="action" fontSize="large" />
                ) : isLoading ? (
                    <Skeleton
                        variant="rectangular"
                        width="100%"
                        height="100%"
                    />
                ) : isError || !url ? (
                    <BrokenImage fontSize="small" color="disabled" />
                ) : (
                    <Box
                        component="img"
                        src={url}
                        alt={attachment.caption ?? ""}
                        sx={{
                            width: "100%",
                            height: "100%",
                            objectFit: "cover",
                        }}
                    />
                )}
```
Change the tile's `aria-label` (line 37) to be document-aware:
```tsx
                aria-label={isDocument ? t("recipes.openDocument") : "open image"}
```
For documents, show the filename under the tile when there's no caption — change the caption `<Typography>` block so the displayed text falls back to the filename for documents:
```tsx
            {attachment.caption || isDocument ? (
                <Typography
                    variant="caption"
                    color="text.secondary"
                    sx={{
                        display: "block",
                        mt: 0.5,
                        wordBreak: "break-word",
                    }}
                >
                    {attachment.caption ||
                        (isDocument ? attachment.originalFileName : "")}
                </Typography>
            ) : null}
```
(Note: `Tile` needs `t` — add `const { t } = useTranslation();` at the top of `Tile`, importing `useTranslation` is already done at file scope.)

In the parent `RecipeViewAttachments` component, after `const [openId, setOpenId] = useState<number | null>(null);` add:
```tsx
    const openFile = useOpenRecipeAttachmentFile(householdId, recipeId);
```
Change the tile's `onOpen` wiring (the `.map`, lines 135–143) so documents open in a tab and images open the lightbox:
```tsx
                {attachments.map((attachment) => (
                    <Tile
                        key={attachment.id}
                        householdId={householdId}
                        recipeId={recipeId}
                        attachment={attachment}
                        onOpen={() =>
                            attachment.type === "Document"
                                ? openFile(attachment.id)
                                : setOpenId(attachment.id)
                        }
                    />
                ))}
```
The lightbox stays as-is — `openId` is only ever set for images now.

- [ ] **Step 3: Type-check + lint**

Run (from `ClientApp/`): `npm run tsc && npm run lint`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/useOpenRecipeAttachmentFile.ts Application/Frigorino.Web/ClientApp/src/features/recipes/attachments/components/RecipeViewAttachments.tsx
git commit -m "feat(recipes): document tile opens PDF in a new tab"
```

---

### Task 11: i18n keys

**Files:**
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/en/translation.json`
- Modify: `Application/Frigorino.Web/ClientApp/public/locales/de/translation.json`

- [ ] **Step 1: Add the two new keys under `recipes` (en)**

In `en/translation.json`, next to `attachImageTitle` (line 391) add:

```json
        "attachDocumentTitle": "Attach document",
        "openDocument": "Open document",
```

- [ ] **Step 2: Add the German translations**

In `de/translation.json`, next to `attachImageTitle` (line 391) add:

```json
        "attachDocumentTitle": "Dokument anhängen",
        "openDocument": "Dokument öffnen",
```

- [ ] **Step 3: Verify JSON validity + format**

Run (from `ClientApp/`): `npm run prettier:check` (or `npm run prettier` to write). Expected: both files valid and formatted.

- [ ] **Step 4: Commit**

```bash
git add Application/Frigorino.Web/ClientApp/public/locales/en/translation.json Application/Frigorino.Web/ClientApp/public/locales/de/translation.json
git commit -m "feat(recipes): i18n for document attachments (en, de)"
```

---

## Phase D — Verification

### Task 12: Full verification gate, manual verification, tracking cleanup

**Files:**
- Modify: `IDEAS_Recipes.md`

- [ ] **Step 1: Frontend verification**

Run (from `ClientApp/`):
```bash
npm run tsc
npm run lint
npm run prettier:check
npm run build
```
Expected: all PASS. (`build` regenerates `routeTree.gen.ts` and refreshes `ClientApp/build`, needed before any manual run.)

- [ ] **Step 2: Backend verification (full solution)**

Run:
```bash
dotnet test Application/Frigorino.sln
```
Expected: PASS (Frigorino.Test + Frigorino.IntegrationTests). Note: this needs Docker running for Testcontainers — if the daemon is unreachable, ask the user to start Docker Desktop.

- [ ] **Step 3: Docker build (drift check)**

Run:
```bash
docker build -f Application/Dockerfile -t frigorino .
```
Expected: PASS. No new projects or native deps, so this is confirming nothing drifted.

- [ ] **Step 4: Manual verification (dev stack)**

Bring up the dev stack (`/dev-up`) and verify in the browser (this is the substitute for IT, mirroring how images shipped):
- Open a recipe's edit page → Attachments → Add → **Document** → pick a PDF → the preview sheet shows the doc icon + filename (no broken image) → add a caption → Send.
- The new row shows a document icon + the caption/filename; no thumbnail request 404s in the console.
- Add an **image** too → confirm the image path still works (thumbnail renders, lightbox opens) — i.e. routing didn't break images.
- Reorder so a document and an image interleave → order persists after refresh.
- Edit the document's caption via its row → persists.
- Delete the document → undo toast restores it.
- On the **view page**, the document tile shows an icon + filename; tapping it opens the PDF in a new browser tab; the image tile still opens the lightbox.
- Upload a non-PDF, non-image file is impossible via the picker (`accept` filters it); if forced, the server returns 400.

- [ ] **Step 5: Remove the shipped item from the ideas backlog**

In `IDEAS_Recipes.md`, delete the entire "## Document (non-image) attachments" section (lines 19–23) — its work has shipped (per the "delete tracking items when done" practice). Leave the "AI-generated cooking instructions" and later sections intact.

- [ ] **Step 6: Commit**

```bash
git add IDEAS_Recipes.md
git commit -m "docs(recipes): drop shipped document-attachments idea"
```

---

## Self-review notes

- **Spec coverage:** data model (Task 1), domain method + validation (Task 2), response `Type` (Task 3), single-endpoint routing (Task 4), client regen (Task 5), enable menu + picker (Task 6), preview/row/caption/view document branches (Tasks 7–10), open-in-tab hook (Task 10), i18n (Task 11), verification incl. manual (Task 12). `/file`, `/thumbnail`, `GetRecipeRevision`, `DeleteInactiveItems`, and `RecipeAttachmentBlobReferences` need no change (the reference source already filters null thumbnail keys) — intentionally no task.
- **Testing scope:** unit tests for the aggregate (Task 2); content-type routing + the full UI flow are covered by manual verification (Task 12), mirroring how images shipped. No new Reqnroll/Playwright IT this phase (deliberate, per spec).
- **Type consistency:** `AttachmentType` (`Image`/`Document`) is the C# enum; on the wire and in TS it's the string union `"Image" | "Document"`; freshly-picked files are detected by `file.type === "application/pdf"`. `useAttachmentImage(..., enabled)` is the existing 5th-arg gate reused to suppress the thumbnail fetch for documents.
```
