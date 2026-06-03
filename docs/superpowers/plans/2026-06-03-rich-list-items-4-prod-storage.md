# Rich list items #4 — Production GCS storage backend + orphan-blob cleanup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make image list items production-ready by adding a persistent Firebase Cloud Storage (GCS) backend behind the existing `IFileStorage` port, plus a maintenance task that reclaims orphaned blobs.

**Architecture:** Two cohesive Infrastructure pieces behind the existing seam: (1) `GcsFileStorage` — a GCS adapter selected by a `FileStorage:Provider` config switch, reusing the Firebase service-account credential, writing all objects under a fixed prefix; (2) `ReclaimOrphanBlobs` — a cold-start `IMaintenanceTask` that mark-and-sweeps blobs no `ListItems` row references and that are older than a grace period. The hot-path port is unchanged; listing is a segregated `IFileStorageMaintenance` capability.

**Tech Stack:** .NET 10, EF Core (Postgres), `Google.Cloud.Storage.V1` 4.14.0, xUnit + FakeItEasy. Spec: `docs/superpowers/specs/2026-06-03-rich-list-items-4-prod-storage-design.md`. Branch: `feat/rich-list-items-4-prod-storage` (already created, off `feat/rich-list-items`).

**Conventions to honor:** C# block-brace style always (even single-line `if`). NuGet exact-pinned versions. No Co-Authored-By trailers. Match existing file style (e.g. `IFileStorage.cs` has no explicit usings — Domain has ImplicitUsings enabled).

---

## File Structure

**Create:**
- `Application/Frigorino.Domain/Interfaces/IFileStorageMaintenance.cs` — listing capability + `StoredBlob` record.
- `Application/Frigorino.Infrastructure/Tasks/OrphanBlobSweep.cs` — pure reclamation decision.
- `Application/Frigorino.Infrastructure/Services/GcsObjectNaming.cs` — pure key⇄object-name mapping.
- `Application/Frigorino.Infrastructure/Services/GcsFileStorage.cs` — GCS adapter (both interfaces).
- `Application/Frigorino.Infrastructure/Tasks/ReclaimOrphanBlobs.cs` — the maintenance task.
- `Application/Frigorino.Test/Infrastructure/OrphanBlobSweepTests.cs`
- `Application/Frigorino.Test/Infrastructure/GcsObjectNamingTests.cs`
- `Application/Frigorino.Test/Infrastructure/ReclaimOrphanBlobsTests.cs`
- `Application/Frigorino.Test/Infrastructure/FileStorageDependencyInjectionTests.cs`

**Modify:**
- `Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs` — implement `IFileStorageMaintenance`.
- `Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs` — add `ListAsync` test.
- `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj` — add GCS package.
- `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs` — provider switch + register both interfaces.
- `Application/Frigorino.Infrastructure/Notifications/MaintenanceSettings.cs` — add `OrphanBlobGraceHours`.
- `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs` — register the task.
- `Application/Frigorino.Web/appsettings.json` — add `FileStorage` provider keys.
- `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs` — bind both storage interfaces to the same temp instance.

**No migration. No frontend/API changes (no `npm run api`).**

---

## Task 1: `IFileStorageMaintenance` interface + `StoredBlob` record

**Files:**
- Create: `Application/Frigorino.Domain/Interfaces/IFileStorageMaintenance.cs`

This is a pure declaration (no behavior) — verified by compile, not a unit test.

- [ ] **Step 1: Create the interface file**

Match `IFileStorage.cs` style (no explicit usings — Domain has ImplicitUsings):

```csharp
namespace Frigorino.Domain.Interfaces
{
    // Listing capability for blob maintenance (orphan reclamation), kept separate from the lean
    // hot-path IFileStorage so upload/serve slices don't depend on enumeration. Each backend
    // enumerates only its OWN namespace, so a sweep can never see foreign objects in a shared bucket.
    public interface IFileStorageMaintenance
    {
        IAsyncEnumerable<StoredBlob> ListAsync(CancellationToken ct);
    }

    // One stored blob: its opaque key (bare, exactly as stored in the DB) and storage-side creation
    // time (used by the sweep's grace-period filter).
    public readonly record struct StoredBlob(string Key, DateTimeOffset CreatedAt);
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Domain`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Application/Frigorino.Domain/Interfaces/IFileStorageMaintenance.cs
git commit -m "feat(domain): add IFileStorageMaintenance + StoredBlob for blob enumeration"
```

---

## Task 2: `OrphanBlobSweep.SelectReclaimableKeys` pure function

**Files:**
- Create: `Application/Frigorino.Infrastructure/Tasks/OrphanBlobSweep.cs`
- Test: `Application/Frigorino.Test/Infrastructure/OrphanBlobSweepTests.cs`

Mirrors the `CheckedItemPurge.SelectExpiredItemIds` pure-decision pattern.

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Infrastructure/OrphanBlobSweepTests.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Tasks;

namespace Frigorino.Test.Infrastructure
{
    public class OrphanBlobSweepTests
    {
        private static readonly DateTimeOffset Now = new(2026, 6, 3, 0, 0, 0, TimeSpan.Zero);
        private static readonly TimeSpan Grace = TimeSpan.FromHours(24);

        [Fact]
        public void ReferencedKey_IsKept_EvenWhenOld()
        {
            var blobs = new[] { new StoredBlob("ref", Now.AddDays(-10)) };
            var referenced = new HashSet<string> { "ref" };

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Empty(result);
        }

        [Fact]
        public void UnreferencedAgedKey_IsReclaimed()
        {
            var blobs = new[] { new StoredBlob("orphan", Now.AddDays(-2)) };
            var referenced = new HashSet<string>();

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Equal(new[] { "orphan" }, result);
        }

        [Fact]
        public void UnreferencedFreshKey_IsKept_WithinGracePeriod()
        {
            var blobs = new[] { new StoredBlob("fresh", Now.AddHours(-1)) };
            var referenced = new HashSet<string>();

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Empty(result);
        }

        [Fact]
        public void MixedSet_ReclaimsOnlyUnreferencedAged()
        {
            var blobs = new[]
            {
                new StoredBlob("ref-full", Now.AddDays(-5)),
                new StoredBlob("ref-thumb", Now.AddDays(-5)),
                new StoredBlob("orphan-old", Now.AddDays(-5)),
                new StoredBlob("orphan-fresh", Now.AddMinutes(-5)),
            };
            var referenced = new HashSet<string> { "ref-full", "ref-thumb" };

            var result = OrphanBlobSweep.SelectReclaimableKeys(blobs, referenced, Now, Grace);

            Assert.Equal(new[] { "orphan-old" }, result);
        }

        [Fact]
        public void EmptyInputs_ReturnEmpty()
        {
            var result = OrphanBlobSweep.SelectReclaimableKeys(
                Array.Empty<StoredBlob>(), new HashSet<string>(), Now, Grace);

            Assert.Empty(result);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~OrphanBlobSweepTests"`
Expected: FAIL — `OrphanBlobSweep` does not exist (compile error).

- [ ] **Step 3: Write the pure function**

Create `Application/Frigorino.Infrastructure/Tasks/OrphanBlobSweep.cs`:

```csharp
using Frigorino.Domain.Interfaces;

namespace Frigorino.Infrastructure.Tasks
{
    // Pure orphan-reclamation decision: which blob keys are safe to delete. A blob is reclaimable
    // when no ListItems row references it AND it is older than the grace period (which protects an
    // in-flight upload whose row is not yet committed). Kept free of EF and IO so it is unit-testable
    // without a database or a storage backend.
    public static class OrphanBlobSweep
    {
        public static List<string> SelectReclaimableKeys(
            IEnumerable<StoredBlob> blobs,
            ISet<string> referencedKeys,
            DateTimeOffset now,
            TimeSpan gracePeriod)
        {
            var cutoff = now - gracePeriod;
            var reclaimable = new List<string>();
            foreach (var blob in blobs)
            {
                if (referencedKeys.Contains(blob.Key))
                {
                    continue;
                }

                if (blob.CreatedAt > cutoff)
                {
                    continue; // too fresh — may be an in-flight upload
                }

                reclaimable.Add(blob.Key);
            }

            return reclaimable;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~OrphanBlobSweepTests"`
Expected: PASS — 5 passed.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Tasks/OrphanBlobSweep.cs Application/Frigorino.Test/Infrastructure/OrphanBlobSweepTests.cs
git commit -m "feat(infra): add OrphanBlobSweep pure reclamation decision"
```

---

## Task 3: `LocalFileStorage` implements `IFileStorageMaintenance.ListAsync`

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs`
- Test: `Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs`. First add the using at the top of the file (after the existing `using Frigorino.Infrastructure.Services;`):

```csharp
using Frigorino.Domain.Interfaces;
```

Then add this test method inside the class:

```csharp
[Fact]
public async Task ListAsync_EnumeratesSavedBlobs_WithKeysAndRecentTimestamps()
{
    var storage = NewStorage();
    using var a = new MemoryStream(new byte[] { 1 });
    using var b = new MemoryStream(new byte[] { 2 });
    var keyA = await storage.SaveAsync(a, CancellationToken.None);
    var keyB = await storage.SaveAsync(b, CancellationToken.None);

    var listed = new List<StoredBlob>();
    await foreach (var blob in storage.ListAsync(CancellationToken.None))
    {
        listed.Add(blob);
    }

    Assert.Equal(2, listed.Count);
    Assert.Contains(listed, x => x.Key == keyA);
    Assert.Contains(listed, x => x.Key == keyB);
    Assert.All(listed, x => Assert.True(x.CreatedAt <= DateTimeOffset.UtcNow.AddSeconds(5)));
    Assert.All(listed, x => Assert.True(x.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-5)));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~LocalFileStorageTests.ListAsync_EnumeratesSavedBlobs_WithKeysAndRecentTimestamps"`
Expected: FAIL — `LocalFileStorage` does not contain `ListAsync` (compile error).

- [ ] **Step 3: Implement `ListAsync` on `LocalFileStorage`**

In `Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs`:

1. Add usings at the top:

```csharp
using System.Runtime.CompilerServices;
using Frigorino.Domain.Interfaces;
```

2. Change the class declaration from:

```csharp
    public sealed class LocalFileStorage : IFileStorage
```

to:

```csharp
    public sealed class LocalFileStorage : IFileStorage, IFileStorageMaintenance
```

3. Add this method inside the class (e.g. after `DeleteAsync`):

```csharp
        // Enumerates this backend's own namespace (the files directly under _root). Blobs are
        // write-once (content-addressable GUID, never updated), so last-write time equals creation
        // time and is portable across OSes (unlike creation time on Linux).
        public async IAsyncEnumerable<StoredBlob> ListAsync(
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var path in Directory.EnumerateFiles(_root))
            {
                ct.ThrowIfCancellationRequested();
                var key = Path.GetFileName(path);
                yield return new StoredBlob(
                    key, new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero));
            }

            await Task.CompletedTask;
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~LocalFileStorageTests"`
Expected: PASS — all `LocalFileStorageTests` pass (the new one + the existing 6).

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/LocalFileStorage.cs Application/Frigorino.Test/Infrastructure/LocalFileStorageTests.cs
git commit -m "feat(infra): LocalFileStorage implements IFileStorageMaintenance.ListAsync"
```

---

## Task 4: `GcsObjectNaming` pure key⇄object-name mapping

**Files:**
- Create: `Application/Frigorino.Infrastructure/Services/GcsObjectNaming.cs`
- Test: `Application/Frigorino.Test/Infrastructure/GcsObjectNamingTests.cs`

Extracting the prefix logic keeps the only non-trivial GCS logic unit-testable without a live client.

- [ ] **Step 1: Write the failing tests**

Create `Application/Frigorino.Test/Infrastructure/GcsObjectNamingTests.cs`:

```csharp
using Frigorino.Infrastructure.Services;

namespace Frigorino.Test.Infrastructure
{
    public class GcsObjectNamingTests
    {
        [Fact]
        public void ToObjectName_PrependsPrefix()
        {
            Assert.Equal("list-items/abc123", GcsObjectNaming.ToObjectName("list-items", "abc123"));
        }

        [Fact]
        public void ToKey_StripsPrefix()
        {
            Assert.Equal("abc123", GcsObjectNaming.ToKey("list-items", "list-items/abc123"));
        }

        [Fact]
        public void ToKey_LeavesNonPrefixedNameUnchanged()
        {
            Assert.Equal("other/abc123", GcsObjectNaming.ToKey("list-items", "other/abc123"));
        }

        [Fact]
        public void RoundTrips()
        {
            var name = GcsObjectNaming.ToObjectName("list-items", "deadbeef");
            Assert.Equal("deadbeef", GcsObjectNaming.ToKey("list-items", name));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~GcsObjectNamingTests"`
Expected: FAIL — `GcsObjectNaming` does not exist (compile error).

- [ ] **Step 3: Write the helper**

Create `Application/Frigorino.Infrastructure/Services/GcsObjectNaming.cs`:

```csharp
namespace Frigorino.Infrastructure.Services
{
    // Maps between bare storage keys (what the DB stores) and prefixed GCS object names. The prefix
    // namespaces all Frigorino objects so the orphan sweep only ever enumerates/deletes our own
    // objects, even in a shared bucket.
    public static class GcsObjectNaming
    {
        public static string ToObjectName(string prefix, string key)
        {
            return $"{prefix}/{key}";
        }

        public static string ToKey(string prefix, string objectName)
        {
            var head = prefix + "/";
            if (objectName.StartsWith(head, StringComparison.Ordinal))
            {
                return objectName[head.Length..];
            }

            return objectName;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~GcsObjectNamingTests"`
Expected: PASS — 4 passed.

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/GcsObjectNaming.cs Application/Frigorino.Test/Infrastructure/GcsObjectNamingTests.cs
git commit -m "feat(infra): add GcsObjectNaming prefix mapping"
```

---

## Task 5: `GcsFileStorage` adapter + GCS NuGet package

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`
- Create: `Application/Frigorino.Infrastructure/Services/GcsFileStorage.cs`

This is a thin vendor adapter over `StorageClient` — the only non-trivial logic (naming) is already TDD'd in Task 4, so this task is verified by compile (no live-GCS unit test, consistent with how vendor adapters are handled in this codebase; no GCS calls run in CI).

- [ ] **Step 1: Add the GCS package (exact-pinned)**

In `Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj`, add to the package `ItemGroup` (keep alphabetical-ish ordering near the other Google/Microsoft entries):

```xml
    <PackageReference Include="Google.Cloud.Storage.V1" Version="4.14.0" />
```

- [ ] **Step 2: Restore to verify it resolves**

Run: `dotnet restore Application/Frigorino.sln`
Expected: Restore succeeded (no version-conflict errors with the existing `Google.Apis.Auth` from `FirebaseAdmin`).

- [ ] **Step 3: Write the adapter**

Create `Application/Frigorino.Infrastructure/Services/GcsFileStorage.cs`:

```csharp
using System.Net;
using System.Runtime.CompilerServices;
using Frigorino.Domain.Interfaces;
using Google;
using Google.Cloud.Storage.V1;

namespace Frigorino.Infrastructure.Services
{
    // Production blob backend on Firebase Cloud Storage (GCS). Hot path mirrors LocalFileStorage's
    // contract: bare GUID keys, and the served content-type lives in the DB row (so the stored
    // object's content-type is irrelevant). All objects are written under a fixed prefix so the
    // orphan sweep is bucket-safe. StorageClient is thread-safe → registered as a singleton.
    public sealed class GcsFileStorage : IFileStorage, IFileStorageMaintenance
    {
        private readonly StorageClient _client;
        private readonly string _bucket;
        private readonly string _prefix;

        public GcsFileStorage(StorageClient client, string bucket, string prefix)
        {
            _client = client;
            _bucket = bucket;
            _prefix = prefix;
        }

        public async Task<string> SaveAsync(Stream content, CancellationToken ct)
        {
            var key = Guid.NewGuid().ToString("N");
            await _client.UploadObjectAsync(
                _bucket,
                GcsObjectNaming.ToObjectName(_prefix, key),
                contentType: "application/octet-stream",
                source: content,
                options: null,
                cancellationToken: ct);
            return key;
        }

        public async Task<Stream?> OpenAsync(string key, CancellationToken ct)
        {
            var ms = new MemoryStream();
            try
            {
                await _client.DownloadObjectAsync(
                    _bucket,
                    GcsObjectNaming.ToObjectName(_prefix, key),
                    ms,
                    options: null,
                    cancellationToken: ct);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                await ms.DisposeAsync();
                return null;
            }

            ms.Position = 0;
            return ms;
        }

        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            try
            {
                await _client.DeleteObjectAsync(
                    _bucket,
                    GcsObjectNaming.ToObjectName(_prefix, key),
                    options: null,
                    cancellationToken: ct);
            }
            catch (GoogleApiException ex) when (ex.HttpStatusCode == HttpStatusCode.NotFound)
            {
                // idempotent — already gone
            }
        }

        public async IAsyncEnumerable<StoredBlob> ListAsync([EnumeratorCancellation] CancellationToken ct)
        {
            await foreach (var obj in _client.ListObjectsAsync(_bucket, _prefix).WithCancellation(ct))
            {
                // TimeCreatedDateTimeOffset is the non-deprecated timestamp on Google.Apis.Storage
                // 1.60+; falls back to MinValue (always past the grace cutoff → eligible) if absent.
                yield return new StoredBlob(
                    GcsObjectNaming.ToKey(_prefix, obj.Name),
                    obj.TimeCreatedDateTimeOffset ?? DateTimeOffset.MinValue);
            }
        }
    }
}
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: Build succeeded, 0 errors. (If `TimeCreatedDateTimeOffset` is unavailable in the resolved `Google.Apis.Storage.v1`, fall back to `obj.TimeCreated.HasValue ? new DateTimeOffset(obj.TimeCreated.Value, TimeSpan.Zero) : DateTimeOffset.MinValue`.)

- [ ] **Step 5: Commit**

```bash
git add Application/Frigorino.Infrastructure/Frigorino.Infrastructure.csproj Application/Frigorino.Infrastructure/Services/GcsFileStorage.cs Application/*/packages.lock.json
git commit -m "feat(infra): add GcsFileStorage backend + Google.Cloud.Storage.V1"
```

---

## Task 6: `AddFileStorage` provider switch + config + IT harness coherence

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs`
- Modify: `Application/Frigorino.Web/appsettings.json`
- Modify: `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`
- Test: `Application/Frigorino.Test/Infrastructure/FileStorageDependencyInjectionTests.cs`

After this task both `IFileStorage` and `IFileStorageMaintenance` are always registered (defaulting to `Local`), so the container stays resolvable before any consumer exists.

- [ ] **Step 1: Write the failing DI test**

Create `Application/Frigorino.Test/Infrastructure/FileStorageDependencyInjectionTests.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Frigorino.Test.Infrastructure
{
    public class FileStorageDependencyInjectionTests
    {
        [Fact]
        public void LocalProvider_RegistersSameInstance_ForBothInterfaces()
        {
            var localPath = Path.Combine(
                Path.GetTempPath(), "frigorino-di-test-" + Guid.NewGuid().ToString("N"));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:Provider"] = "Local",
                    ["FileStorage:LocalPath"] = localPath,
                })
                .Build();

            var services = new ServiceCollection();
            services.AddFileStorage(config);
            using var sp = services.BuildServiceProvider();

            var hotPath = sp.GetRequiredService<IFileStorage>();
            var maintenance = sp.GetRequiredService<IFileStorageMaintenance>();

            Assert.IsType<LocalFileStorage>(hotPath);
            Assert.Same(hotPath, maintenance);
        }

        [Fact]
        public void DefaultProvider_IsLocal_WhenUnset()
        {
            var localPath = Path.Combine(
                Path.GetTempPath(), "frigorino-di-test-" + Guid.NewGuid().ToString("N"));
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FileStorage:LocalPath"] = localPath,
                })
                .Build();

            var services = new ServiceCollection();
            services.AddFileStorage(config);
            using var sp = services.BuildServiceProvider();

            Assert.IsType<LocalFileStorage>(sp.GetRequiredService<IFileStorage>());
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FileStorageDependencyInjectionTests"`
Expected: FAIL — `IFileStorageMaintenance` is not registered (resolution throws) and/or the current registration uses the lambda-only shape.

- [ ] **Step 3: Rewrite `FileStorageDependencyInjection`**

Replace the entire contents of `Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs` with:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Frigorino.Infrastructure.Services
{
    public static class FileStorageDependencyInjection
    {
        // Selects the blob backend by FileStorage:Provider ("Local" default for dev/test/CI, "Gcs"
        // for prod). Both backends register the same singleton instance under IFileStorage (hot path)
        // and IFileStorageMaintenance (sweep listing). Construction is deferred in the factory lambdas
        // so DI build / build-time OpenAPI generation never touches the filesystem or a GCS client.
        public static IServiceCollection AddFileStorage(
            this IServiceCollection services, IConfiguration configuration)
        {
            var provider = configuration["FileStorage:Provider"];
            if (string.Equals(provider, "Gcs", StringComparison.OrdinalIgnoreCase))
            {
                AddGcs(services, configuration);
            }
            else
            {
                AddLocal(services, configuration);
            }

            return services;
        }

        private static void AddLocal(IServiceCollection services, IConfiguration configuration)
        {
            // When FileStorage:LocalPath is unset we fall back to a "blobs" directory under the content
            // root — fine for dev/test. In a container this path is ephemeral (lost on restart) unless
            // it points at a mounted volume; production should use the Gcs provider.
            var configured = configuration["FileStorage:LocalPath"];
            services.AddSingleton<LocalFileStorage>(sp =>
            {
                var root = string.IsNullOrWhiteSpace(configured)
                    ? Path.Combine(sp.GetRequiredService<IHostEnvironment>().ContentRootPath, "blobs")
                    : configured;
                return new LocalFileStorage(root);
            });
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<LocalFileStorage>());
            services.AddSingleton<IFileStorageMaintenance>(sp => sp.GetRequiredService<LocalFileStorage>());
        }

        private static void AddGcs(IServiceCollection services, IConfiguration configuration)
        {
            var bucket = configuration["FileStorage:Bucket"];
            var prefix = configuration["FileStorage:KeyPrefix"];
            var accessJson = configuration
                .GetSection(FirebaseSettings.SECTION_NAME)
                .Get<FirebaseSettings>()?.AccessJson;

            services.AddSingleton<GcsFileStorage>(sp =>
            {
                if (string.IsNullOrWhiteSpace(bucket))
                {
                    throw new InvalidOperationException(
                        "FileStorage:Bucket is required when FileStorage:Provider is 'Gcs'.");
                }

                if (string.IsNullOrWhiteSpace(accessJson))
                {
                    throw new InvalidOperationException(
                        "FirebaseSettings:AccessJson is required for the GCS file storage backend.");
                }

                var credential = CredentialFactory
                    .FromJson<ServiceAccountCredential>(accessJson)
                    .ToGoogleCredential();
                var client = StorageClient.Create(credential);
                var effectivePrefix = string.IsNullOrWhiteSpace(prefix) ? "list-items" : prefix;
                return new GcsFileStorage(client, bucket, effectivePrefix);
            });
            services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<GcsFileStorage>());
            services.AddSingleton<IFileStorageMaintenance>(sp => sp.GetRequiredService<GcsFileStorage>());
        }
    }
}
```

- [ ] **Step 4: Run the DI tests to verify they pass**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~FileStorageDependencyInjectionTests"`
Expected: PASS — 2 passed.

- [ ] **Step 5: Add the config keys**

In `Application/Frigorino.Web/appsettings.json`, replace the `FileStorage` block:

```json
  "FileStorage": {
    "LocalPath": ""
  },
```

with:

```json
  "FileStorage": {
    "Provider": "",
    "LocalPath": "",
    "Bucket": "",
    "KeyPrefix": ""
  },
```

- [ ] **Step 6: Make the IT harness coherent (bind both interfaces to the temp instance)**

In `Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs`, find this block at the end of `ConfigureServices`:

```csharp
            services.RemoveAll<IFileStorage>();
            var blobRoot = Path.Combine(Path.GetTempPath(), "frigorino-it-blobs", Guid.NewGuid().ToString("N"));
            services.AddSingleton<IFileStorage>(new LocalFileStorage(blobRoot));
```

Replace it with:

```csharp
            // Real blob storage bound to a unique temp dir per factory instance, registered under BOTH
            // storage interfaces (one shared instance) so the startup orphan-sweep operates on the temp
            // dir, never a real path. Only the AI classifiers stay stubbed; IImageProcessor stays real.
            services.RemoveAll<IFileStorage>();
            services.RemoveAll<IFileStorageMaintenance>();
            var blobRoot = Path.Combine(Path.GetTempPath(), "frigorino-it-blobs", Guid.NewGuid().ToString("N"));
            var blobStorage = new LocalFileStorage(blobRoot);
            services.AddSingleton<IFileStorage>(blobStorage);
            services.AddSingleton<IFileStorageMaintenance>(blobStorage);
```

- [ ] **Step 7: Build the IntegrationTests project to verify it compiles**

Run: `dotnet build Application/Frigorino.IntegrationTests`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Infrastructure/Services/FileStorageDependencyInjection.cs Application/Frigorino.Web/appsettings.json Application/Frigorino.IntegrationTests/Infrastructure/TestWebApplicationFactory.cs Application/Frigorino.Test/Infrastructure/FileStorageDependencyInjectionTests.cs
git commit -m "feat(infra): FileStorage provider switch (Local|Gcs) + register IFileStorageMaintenance"
```

---

## Task 7: `ReclaimOrphanBlobs` maintenance task + grace-period setting + registration

**Files:**
- Modify: `Application/Frigorino.Infrastructure/Notifications/MaintenanceSettings.cs`
- Create: `Application/Frigorino.Infrastructure/Tasks/ReclaimOrphanBlobs.cs`
- Modify: `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs`
- Test: `Application/Frigorino.Test/Infrastructure/ReclaimOrphanBlobsTests.cs`

- [ ] **Step 1: Add the grace-period setting**

In `Application/Frigorino.Infrastructure/Notifications/MaintenanceSettings.cs`, add this property inside the class (after `OverdueGraceDays`):

```csharp
        // Blobs younger than this are never reclaimed by the orphan sweep — protects an in-flight
        // upload whose ListItems row is not yet committed.
        public int OrphanBlobGraceHours { get; set; } = 24;
```

- [ ] **Step 2: Write the failing test**

Create `Application/Frigorino.Test/Infrastructure/ReclaimOrphanBlobsTests.cs`:

```csharp
using System.Runtime.CompilerServices;
using FakeItEasy;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Frigorino.Infrastructure.Tasks;
using Frigorino.Test.TestInfrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Frigorino.Test.Infrastructure
{
    public class ReclaimOrphanBlobsTests
    {
        // Returns a fixed set of blobs as an async stream (FakeItEasy is awkward for IAsyncEnumerable).
        private sealed class StubMaintenance : IFileStorageMaintenance
        {
            private readonly IReadOnlyList<StoredBlob> _blobs;

            public StubMaintenance(IReadOnlyList<StoredBlob> blobs)
            {
                _blobs = blobs;
            }

            public async IAsyncEnumerable<StoredBlob> ListAsync([EnumeratorCancellation] CancellationToken ct)
            {
                foreach (var blob in _blobs)
                {
                    yield return blob;
                }

                await Task.CompletedTask;
            }
        }

        private static TestApplicationDbContext NewContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new TestApplicationDbContext(options);
        }

        [Fact]
        public async Task Run_ReclaimsOnly_UnreferencedAgedBlobs()
        {
            var dbName = Guid.NewGuid().ToString();
            using (var db = NewContext(dbName))
            {
                // Active image item referencing full-res + thumbnail.
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "",
                    Type = ListItemType.Image,
                    StorageKey = "ref-full",
                    ThumbnailStorageKey = "ref-thumb",
                    IsActive = true,
                });
                // Soft-deleted item still references its blob (kept for undo) — must NOT be reclaimed.
                db.ListItems.Add(new ListItem
                {
                    ListId = 1,
                    Text = "",
                    Type = ListItemType.Image,
                    StorageKey = "ref-deleted",
                    ThumbnailStorageKey = null,
                    IsActive = false,
                });
                await db.SaveChangesAsync();
            }

            var old = DateTimeOffset.UtcNow.AddDays(-2);
            var fresh = DateTimeOffset.UtcNow.AddMinutes(-5);
            var maintenance = new StubMaintenance(new[]
            {
                new StoredBlob("ref-full", old),
                new StoredBlob("ref-thumb", old),
                new StoredBlob("ref-deleted", old),
                new StoredBlob("orphan-old", old),
                new StoredBlob("orphan-fresh", fresh),
            });

            var storage = A.Fake<IFileStorage>();
            var settings = Options.Create(new MaintenanceSettings { OrphanBlobGraceHours = 24 });

            using var runDb = NewContext(dbName);
            var task = new ReclaimOrphanBlobs(
                runDb, storage, maintenance, settings, NullLogger<ReclaimOrphanBlobs>.Instance);

            await task.Run();

            A.CallTo(() => storage.DeleteAsync("orphan-old", A<CancellationToken>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => storage.DeleteAsync("orphan-fresh", A<CancellationToken>._))
                .MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync("ref-full", A<CancellationToken>._))
                .MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync("ref-thumb", A<CancellationToken>._))
                .MustNotHaveHappened();
            A.CallTo(() => storage.DeleteAsync("ref-deleted", A<CancellationToken>._))
                .MustNotHaveHappened();
        }
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ReclaimOrphanBlobsTests"`
Expected: FAIL — `ReclaimOrphanBlobs` does not exist (compile error).

- [ ] **Step 4: Write the maintenance task**

Create `Application/Frigorino.Infrastructure/Tasks/ReclaimOrphanBlobs.cs`:

```csharp
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Frigorino.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Frigorino.Infrastructure.Tasks
{
    // Mark-and-sweep reclamation of orphaned blobs: deletes stored blobs that no ListItems row
    // references (active or soft-deleted) and that are older than the grace period. Robust to every
    // leak path — the bulk purge, DB cascade deletes, and a crash between blob save and DB commit —
    // without tracing FK relationships. Order-independent with DeleteInactiveItems.
    public sealed class ReclaimOrphanBlobs : IMaintenanceTask
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IFileStorage _storage;
        private readonly IFileStorageMaintenance _maintenance;
        private readonly TimeSpan _gracePeriod;
        private readonly ILogger<ReclaimOrphanBlobs> _logger;

        public ReclaimOrphanBlobs(
            ApplicationDbContext dbContext,
            IFileStorage storage,
            IFileStorageMaintenance maintenance,
            IOptions<MaintenanceSettings> settings,
            ILogger<ReclaimOrphanBlobs> logger)
        {
            _dbContext = dbContext;
            _storage = storage;
            _maintenance = maintenance;
            _gracePeriod = TimeSpan.FromHours(settings.Value.OrphanBlobGraceHours);
            _logger = logger;
        }

        public async Task Run(CancellationToken cancellationToken = default)
        {
            // Referenced set: every full-res + thumbnail key across ALL ListItems (active AND
            // soft-deleted — soft-deleted items keep their blob for undo until they are purged).
            var keyPairs = await _dbContext.ListItems
                .Where(li => li.StorageKey != null)
                .Select(li => new { li.StorageKey, li.ThumbnailStorageKey })
                .ToListAsync(cancellationToken);

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in keyPairs)
            {
                if (pair.StorageKey is not null)
                {
                    referenced.Add(pair.StorageKey);
                }

                if (pair.ThumbnailStorageKey is not null)
                {
                    referenced.Add(pair.ThumbnailStorageKey);
                }
            }

            var blobs = new List<StoredBlob>();
            await foreach (var blob in _maintenance.ListAsync(cancellationToken))
            {
                blobs.Add(blob);
            }

            var reclaimable = OrphanBlobSweep.SelectReclaimableKeys(
                blobs, referenced, DateTimeOffset.UtcNow, _gracePeriod);

            var deleted = 0;
            foreach (var key in reclaimable)
            {
                try
                {
                    await _storage.DeleteAsync(key, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reclaim orphan blob {Key}", key);
                }
            }

            if (deleted > 0)
            {
                _logger.LogInformation("Reclaimed {Count} orphan blob(s).", deleted);
            }
        }
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test Application/Frigorino.Test --filter "FullyQualifiedName~ReclaimOrphanBlobsTests"`
Expected: PASS — 1 passed.

- [ ] **Step 6: Register the task in the maintenance batch**

In `Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs`, add this line immediately after the existing `services.AddScoped<IMaintenanceTask, DeleteInactiveItems>();`:

```csharp
            services.AddScoped<IMaintenanceTask, ReclaimOrphanBlobs>();
```

- [ ] **Step 7: Build to verify registration compiles**

Run: `dotnet build Application/Frigorino.Infrastructure`
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add Application/Frigorino.Infrastructure/Notifications/MaintenanceSettings.cs Application/Frigorino.Infrastructure/Tasks/ReclaimOrphanBlobs.cs Application/Frigorino.Infrastructure/Services/MaintenanceDependencyInjection.cs Application/Frigorino.Test/Infrastructure/ReclaimOrphanBlobsTests.cs
git commit -m "feat(infra): add ReclaimOrphanBlobs maintenance task (orphan-blob sweep)"
```

---

## Task 8: Full verification gate

**Files:** none (verification only).

- [ ] **Step 1: Ensure Docker Desktop is running**

The full solution test runs `Frigorino.IntegrationTests` (Postgres Testcontainers) and the docker build needs the daemon. If either errors with a daemon-unreachable message, ask the user to start Docker Desktop before retrying (do not skip).

- [ ] **Step 2: Run the full solution test suite**

Run: `dotnet test Application/Frigorino.sln`
Expected: PASS — all tests green (unit + integration). The integration suite's app startup now also resolves and runs `ReclaimOrphanBlobs` against real Postgres + the temp `LocalFileStorage`, exercising the wiring end-to-end. Confirm pass/fail from the printed summary lines (not a piped tail exit code).

- [ ] **Step 3: Docker build to catch any drift**

Run: `docker build -f Application/Dockerfile -t frigorino .`
Expected: Build succeeds (no new project was added, so the Dockerfile needs no change — this confirms it).

- [ ] **Step 4: Commit (only if Step 2/3 surfaced a needed fix)**

If a fix was required, commit it:

```bash
git add -A
git commit -m "chore: fixes from full-suite + docker verification for #4 prod storage"
```

If no changes were needed, skip this step — verification is complete.

---

## Operator follow-up (not code — out of plan scope, record for handoff)

These are deployment actions the user performs; the spec lists them under "Configuration & deployment":

- Confirm the GCP project is on the **Blaze** plan (Cloud Storage requirement; stays $0 under the legacy free tier).
- Set on the Railway Web service (stage **and** prod): `FileStorage__Provider=Gcs`, `FileStorage__Bucket=<firebase-bucket>` (e.g. `<project-id>.appspot.com` or `<project-id>.firebasestorage.app`), optional `FileStorage__KeyPrefix`. `FirebaseSettings__AccessJson` is already present for auth.

---

## Self-Review (completed by plan author)

**Spec coverage:**
- GCS backend behind `IFileStorage` (decisions 1–3, 9) → Task 5 + Task 6.
- Reuse `FirebaseSettings:AccessJson` credential (decision 2) → Task 6 `AddGcs`.
- `FileStorage:Provider` switch + `Bucket`/`KeyPrefix` config (decision 3) → Task 6.
- Prefix-scoped objects ⟐ A (decision 4) → Task 4 (naming) + Task 5 (`ListAsync` lists under prefix).
- `IFileStorageMaintenance` segregation ⟐ B (decision 5) → Task 1 + Tasks 3/5 implement it.
- Reconciliation sweep (decision 6) → Task 2 (pure) + Task 7 (task).
- Dedicated `ReclaimOrphanBlobs` task with per-task isolation (decision 7) → Task 7 (registered next to `DeleteInactiveItems`; `MaintenanceHostedService` already isolates per task).
- 24h grace period from config (decision 8) → Task 7 (`MaintenanceSettings.OrphanBlobGraceHours`).
- Referenced set includes soft-deleted items' keys → Task 7 query + Task 7 test (`ref-deleted` case).
- No migration / Dockerfile unchanged / NuGet only → Task 8.
- **Deliberate deviation:** the spec's "integration test" is covered by the unit suite (pure function + InMemory task test + real `LocalFileStorage.ListAsync` + DI resolution) **plus** the existing IT suite's app-startup now exercising `ReclaimOrphanBlobs` against real Postgres (Task 6 harness change). A standalone Reqnroll/Playwright scenario is omitted because a cold-start maintenance task has no HTTP endpoint or UI to drive, and the EF query is trivial and Postgres-safe. This is a conscious scope call, logged here rather than silently dropped.

**Placeholder scan:** none — every code step shows full content; every run step shows the command + expected result.

**Type consistency:** `StoredBlob(string Key, DateTimeOffset CreatedAt)` used identically in Tasks 1/2/3/5/7. `OrphanBlobSweep.SelectReclaimableKeys(IEnumerable<StoredBlob>, ISet<string>, DateTimeOffset, TimeSpan)` defined in Task 2, called in Task 7. `IFileStorageMaintenance.ListAsync(CancellationToken)` defined in Task 1, implemented in Tasks 3/5, consumed in Task 7. `GcsObjectNaming.ToObjectName/ToKey(string, string)` defined in Task 4, used in Task 5. `MaintenanceSettings.OrphanBlobGraceHours` added in Task 7, consumed in the same task.
