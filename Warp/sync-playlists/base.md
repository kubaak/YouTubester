# Playlists Sync — Design & Implementation

This document describes how we cache channel playlists and their memberships and how we support a **delta sync** while
minimizing YouTube API calls. It also ties playlists to channels (1:N) and keeps the **Video** table authoritative for
video rows.

---

## 🎯 Goals

- Cache which playlists each video belongs to.
- Delta sync: only fetch new/changed data since the last run.
- Minimize API calls by batching and avoiding redundant `videos.list`.
- Maintain **Channel → Playlists (1:N)** and **Videos ↔ Playlists (M:N)** relations.
- Ensure FK safety by inserting membership links only for videos present in `Videos`.

---

## 🧩 Data Model

### Tables

#### Channel

| Column                               | Description                |
|--------------------------------------|----------------------------|
| `ChannelId (PK)`                     | Primary key                |
| `Name`                               | Channel name               |
| `UploadsPlaylistId`                  | Playlist ID for uploads    |
| `UpdatedAt`                          | Timestamp                  |
| `LastUploadsCutoff (DateTimeOffset)` | Used for delta sync cutoff |

#### Playlist

| Column                            | Description                       |
|-----------------------------------|-----------------------------------|
| `PlaylistId (PK)`                 | Primary key                       |
| `ChannelId (FK → Channel)`        | Foreign key                       |
| `Title`                           | Playlist title                    |
| `UpdatedAt`                       | Timestamp                         |
| `LastMembershipSyncAt (nullable)` | Last time memberships were synced |

**Index:** `(ChannelId)`

#### Video

| Column               | Description                                |
|----------------------|--------------------------------------------|
| `VideoId (PK)`       | Primary key                                |
| *(existing columns)* | `Title`, `Visibility`, `PublishedAt`, etc. |

#### VideoPlaylists (link table)

Composite PK: `(VideoId, PlaylistId)`

**Foreign Keys:**

- `VideoId → Videos(VideoId)` (ON DELETE CASCADE)
- `PlaylistId → Playlists(PlaylistId)` (ON DELETE CASCADE)

**Index:** `(PlaylistId)`


> ❓ **Why not a comma-separated list?**  
> Hard to query, filter, index, and maintain integrity. The link table is correct and fast.

---

## 🔌 Integration Endpoints Used (YouTube)

- `playlists.list (mine=true, part=snippet)` → channel playlists and titles (few pages)
- `playlistItems.list (part=contentDetails)` → video IDs for a playlist (newest → oldest)
- `videos.list (part=snippet,contentDetails,status,recordingDetails)` → details for up to 50 IDs/batch
- **Uploads playlist** (`Channel.UploadsPlaylistId`) → authoritative full list of channel videos

---

## ⚙️ Delta Strategy (Minimizing Calls)

### A) Delta sync for all videos (via Uploads playlist)

1. Read `LastUploadsCutoff` for the channel (nullable).
2. Page `Uploads` playlist newest→older using `playlistItems.list(...)`.
3. **Early stop** per page when newest item ≤ cutoff.
4. Within a page, stop when item’s `PublishedAt` ≤ cutoff.
5. Collect only new video IDs (not in DB).
6. If any:
    - Batch `videos.list` (≤50 IDs/call); upsert into `Videos`.
    - Update `LastUploadsCutoff` (e.g., to now or max `PublishedAt`).

Keeps `Videos` complete and current with minimal calls.

---

### B) Delta sync for playlist membership

1. Fetch channel playlists: `playlists.list(mine=true)`.
2. Upsert into `Playlists (PlaylistId, ChannelId, Title, UpdatedAt=now)`.
3. For each playlist:
    - List video IDs via `playlistItems.list(contentDetails)`.
    - Load existing links from `VideoPlaylists`.
    - Compute diff:
        - `toInsert = newIds - existingIds`
        - `toDelete = existingIds - newIds`
    - Insert links only for videos present in `Videos`.
    - Accumulate missing video IDs from playlists.

4. Apply diff:
    - **Insert:** add `(VideoId, PlaylistId)` rows
    - **Delete:** remove obsolete links
    - Update `Playlist.LastMembershipSyncAt = now`

5. For missing video IDs:
    - Batch `videos.list` for union (≤50 IDs/call); upsert into `Videos`.

> ⚠️ **Order matters:**  
> Run Uploads delta (A) first to avoid FK violations.  
> If skipped, fetch missing video details (step 3) before inserting links.

---

## 🧱 Repository & Integration Contracts

### IYouTubeIntegration

```csharp
IAsyncEnumerable<(string Id, string? Title)> GetPlaylistsAsync(string channelId, CancellationToken ct);
IAsyncEnumerable<string> GetPlaylistVideoIdsAsync(string playlistId, CancellationToken ct);
Task<IReadOnlyList<Video>> GetVideosAsync(IEnumerable<string> videoIds, CancellationToken ct);
IAsyncEnumerable<string> GetUploadIdsNewerThanAsync(string channelId, DateTimeOffset? cutoff, CancellationToken ct);
