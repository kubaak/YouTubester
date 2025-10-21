# WARP.md

This file provides guidance to WARP (warp.dev) when working with code in this repository.

## Commands

- Restore and build
  ```powershell path=null start=null
  dotnet restore
  dotnet build --no-restore
  ```
- Run API (ASP.NET Core, Swagger in Development, Hangfire dashboard at /hangfire)
  ```powershell path=null start=null
  dotnet run --project YouTubester.Api
  ```
- Run background worker (Hangfire server queues: replies, templating, default)
  ```powershell path=null start=null
  dotnet run --project YouTubester.Worker
  ```
- EF Core migrations (SQLite at ./.data/youtubester.db)
  ```powershell path=null start=null
  # Add a migration
  dotnet ef migrations add <Name> --project YouTubester.Persistence
  # Apply migrations
  dotnet ef database update --project YouTubester.Persistence
  ```
- Lint/format (uses .editorconfig and Roslyn analyzers)
  ```powershell path=null start=null
  dotnet format
  dotnet format analyzers
  ```
- Tests
  - No test projects are present. If/when tests are added:
    ```powershell path=null start=null
    # Run all tests
    dotnet test
    # Run a single test by name
    dotnet test --filter FullyQualifiedName~Namespace.ClassName.TestMethod
    ```
- User secrets (required for YouTube OAuth and AI config)
  ```powershell path=null start=null
  # API: YouTube OAuth (used by Integration via YouTubeAuth section)
  dotnet user-secrets set "YouTubeAuth:ClientId"    "{{YOUTUBE_CLIENT_ID}}"    --project YouTubester.Api
  dotnet user-secrets set "YouTubeAuth:ClientSecret" "{{YOUTUBE_CLIENT_SECRET}}" --project YouTubester.Api
  # Worker: AI configuration (root-level "AI" section)
  dotnet user-secrets set "AI:Endpoint" "http://localhost:11434" --project YouTubester.Worker
  dotnet user-secrets set "AI:Model"    "gemma3:12b"              --project YouTubester.Worker
  ```

Notes
- The API registers AI via AddAiClient(configuration) which binds the root "AI" section. The API's appsettings.json nests AI under "YouTube:AI"; to affect the runtime AI client for the API, provide root-level "AI" settings via secrets or an override appsettings.
- Database file lives at ./.data/youtubester.db (created automatically). Delete this file to reset local data.

## High-level architecture

Projects (Clean, layered composition around YouTube + AI workflows):
- YouTubester.Domain: Core entities and value objects
  - Reply lifecycle: Pulled → Suggested → Approved → Posted/ Ignored (Reply methods enforce invariants and text sanitization)
  - Video aggregate with immutable update logic (ApplyDetails) and convenience URLs
  - Channel basic metadata
- YouTubester.Persistence: Data access and infrastructure
  - EF Core DbContext (YouTubesterDb) with SQLite, model configuration, and migrations; seeds demo Reply in Development
  - Repositories
    - Replies: load/approve/ignore/delete, batch status queries, ExecuteUpdate for bulk ignore
    - Videos: read-all, upsert with change detection (inserts/updates consolidated)
    - Channels: lookup by id/name
  - Hangfire storage configured against the same SQLite file
- YouTubester.Integration: External services
  - YouTubeIntegration wraps google APIs: playlist scans, video details/updates, unanswered comment threads, playlist membership helpers
  - Authentication via YouTubeAuth options and token store; service factory constructs YouTubeService instances
  - AiClient (HTTP to local Ollama-like provider) for metadata and reply suggestions (JSON contract)
- YouTubester.Application: Orchestration and jobs
  - ReplyService: lists drafts, deletes, batch-approve (schedules PostApprovedRepliesJob), batch-ignore with detailed outcome
  - VideoService: syncs channel uploads into DB in batches, maps privacy to domain visibility
  - Hangfire jobs: PostApprovedRepliesJob (posts approved replies), CopyVideoTemplateJob (delegates to templating service)
- YouTubester.Api: ASP.NET Core Web API
  - Controllers
    - Replies: GET drafts, DELETE draft, POST approve batch, POST batch-ignore
    - Videos: POST sync/{channelName} (uses persisted Channel.UploadsPlaylistId), POST copy-template (enqueues job)
  - Development: Swagger + Hangfire dashboard; optional database seeding guarded by appsettings.Development Seed.Enable
- YouTubester.Worker: Background host
  - Hosts Hangfire server with queues replies/templating/default, wires Application jobs and services
  - CommentScanWorker exists but is currently commented out; it drafts replies using AI for public videos up to a configured cap

Key flows
- Video sync: Api.VideosController → Application.VideoService → Integration.YouTubeIntegration → Persistence.VideoRepository (upsert)
- Reply drafting/approval/posting
  - Drafting (optional worker pass): Worker.CommentScanWorker → Integration (YouTube + AI) → Persistence.ReplyRepository
  - Approval: Api.RepliesController → Application.ReplyService → Hangfire enqueues PostApprovedRepliesJob
  - Posting: PostApprovedRepliesJob → Integration.ReplyAsync → Persistence.ReplyRepository update

Configuration
- OAuth and AI settings are supplied via user-secrets or appsettings.*; Worker expects root-level "AI"; Integration’s YouTubeAuth is read from "YouTubeAuth". API development seeding is toggled via appsettings.Development.json (Seed.Enable).
