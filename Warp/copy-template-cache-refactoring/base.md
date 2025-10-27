# WARP TASK: Refactor Copy Template flow to use cached **videoIds
** (+ cached playlists) with cross-host integration test

You are an AI code assistant working in this repository. Implement the following refactor for the `copy-template`
feature now that **playlists are cached in DB**:

- Accept **video IDs** end-to-end (no URLs).
- Use **cached entities** from the local DB (`Videos`, `Playlists`, `VideoPlaylists`) for all reads.
- Keep external interaction limited to a single **YouTube update** call for the target video.
- Add a **cross-host integration test** that enqueues via the API host and executes on the Worker host.

---

## Scope

### 1) Request contract change (URLs → IDs)

- Update `CopyVideoTemplateRequest`:
    - `SourceVideoUrl` → `SourceVideoId`
    - `TargetVideoUrl` → `TargetVideoId`
- **Controller**
    - Accept the updated IDs-based request (no URL parsing).
    - **Validation**:
        - `SourceVideoId` and `TargetVideoId` are **required**, **non-empty**, and **must differ**.

---

### 2) Service behavior (cached reads + YouTube write)

Refactor `VideoTemplatingService`:

- **Load** source and target **from DB** via `VideoRepository` by ID.
    - No YouTube GETs.
    - Playlists are available via `VideoPlaylists` → `Playlists` (already cached; use if needed for templating context).
- **Build effective metadata**:
    - Start from **source** video’s stored fields (title, description, tags, category, language, location, etc.).
    - If `AiSuggestionOptions != null`, call `IAiClient.SuggestMetadataAsync(...)` and **override** title/description (
      and tags if spec requires).
- **Push changes** to YouTube:
  ```csharp
  IYouTubeIntegration.UpdateVideoMetadataAsync(TargetVideoId, payload, ct);

Persist on success only:

After a successful YouTube update, call ApplyDetails(...) on the target Video and SaveChangesAsync().

Failure path:

If YouTube update fails, do not modify DB; return/propagate the error.

Rule: All DB mutations for the target video occur after a confirmed successful YouTube update.

3) Job & enqueue path
   CopyVideoTemplateJob.Run(CopyVideoTemplateRequest req, JobCancellationToken token) stays the same signature.

API controller enqueues the job using the updated request (IDs, not URLs).

The job uses the service; no external reads, only the final YouTube update.

Test: cross-host E2E (happy path)
Name: CopyTemplate_EnqueueViaApi_RunOnWorker_UsesCachedSource_UpdatesTargetAndPersists

Arrange
Use ApiTestWebAppFactory + SqliteCleaner to start from an empty DB.

Seed DB (Seeder helper OK):

Source video: rich metadata (title, description, tags, category, languages, location, etc.).

Target video: different metadata initially.

Optional realism: link one/both videos to cached playlists via VideoPlaylists.

Mocks:

IYouTubeIntegration.UpdateVideoMetadataAsync(TargetVideoId, …) → return success; capture payload.

IAiClient.SuggestMetadataAsync(...):

With AiSuggestionOptions != null → return enriched fields to override.

With AiSuggestionOptions == null → assert no AI call.

Act
Enqueue via API:

POST /api/videos/copy-template with JSON { SourceVideoId, TargetVideoId, AiSuggestionOptions? }.

Assert 200 OK and that ApiTestWebAppFactory.CapturingBackgroundJobClient captured exactly one CopyVideoTemplateJob with
expected request.

Execute the captured job against WorkerTestHostFactory (similar to
WorkerHost_SmokeTests.CopyVideoTemplateJob_Runs_Successfully).

Assert
IYouTubeIntegration.UpdateVideoMetadataAsync called once with:

The correct TargetVideoId.

A payload equal to the effective template (source fields + optional AI overrides).

Target Video row updated to match the effective metadata and UpdatedAt changed.

No DB changes happened before the YouTube call (i.e., only persisted after success).

Controller & route
Keep existing route: POST /api/videos/copy-template

Replace the request model with the IDs-based CopyVideoTemplateRequest.

Update Swagger/XML docs to reflect the new contract (IDs, not URLs).

Files to touch / add
Contracts

Update CopyVideoTemplateRequest → { string SourceVideoId, string TargetVideoId, AiSuggestionOptions? }.

API

Controller action: accept IDs-based request and enqueue job.

Application

VideoTemplatingService: use DB reads, build effective metadata, call YouTube, then persist.

Job

CopyVideoTemplateJob: unchanged signature; no external reads.

Integration tests

Add tests for both AiSuggestionOptions != null and AiSuggestionOptions == null.

Output
Create feature branch:

feature/copy-template-cache-refactoring

Commands:

bash
Copy code
dotnet build
dotnet test tests/YouTubester.IntegrationTests -c Release
Guardrails
Do not start Hangfire Server in tests.

Keep DB updates strictly after a successful YouTube update.

Use Moq & FluentAssertions.

Use ApiTestWebAppFactory to enqueue and WorkerTestHostFactory to execute the job.

Acceptance Criteria
✅ Request uses video IDs (no URLs).

✅ Service relies solely on cached DB reads (Videos/Playlists) prior to update.

✅ YouTube update occurs once; DB persistence only on success.

✅ Cross-host integration test passes (enqueue on API, execute on Worker).

✅ Swagger/docs updated to reflect the IDs-based request.