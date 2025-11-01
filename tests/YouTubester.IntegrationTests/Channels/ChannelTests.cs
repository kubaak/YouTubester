using System.Net;
using System.Text.Json;
using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using YouTubester.Application.Channels;
using YouTubester.Domain;
using YouTubester.Integration.Dtos;
using YouTubester.IntegrationTests.TestHost;
using YouTubester.Persistence;

namespace YouTubester.IntegrationTests.Channels;

[Collection(nameof(TestCollection))]
public sealed class ChannelTests(TestFixture fixture)
{
    [Fact]
    public async Task Sync_WithDummyChannelAndMockedYouTubeData_UpdatesDatabaseCorrectly()
    {
        // Arrange
        await fixture.ResetDbAsync();

        const string testChannelId = "UCTestChannel123456789";
        const string testChannelName = "TestChannelName";
        const string testUploadsPlaylistId = "PLTestUploads123456789";

        // Insert dummy channel into database
        var dummyChannel = Channel.Create(
            testChannelId,
            testChannelName,
            testUploadsPlaylistId,
            TestFixture.TestingDateTimeOffset
        );

        using (var scope = fixture.ApiServices.CreateScope())
        {
            var databaseContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            databaseContext.Channels.Add(dummyChannel);
            await databaseContext.SaveChangesAsync();
        }

        // Create mock video DTOs
        var mockVideos = new List<VideoDto>
        {
            new(
                "video123",
                "Test Video 1",
                "Test Description 1",
                ["tag1", "tag2"],
                TimeSpan.FromMinutes(5),
                "public",
                false,
                new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero),
                "22",
                "en",
                "en",
                null,
                null,
                "etag-video123",
                null
            ),
            new(
                "video456",
                "Test Video 2",
                "Test Description 2",
                ["tag3", "tag4"],
                TimeSpan.FromMinutes(10),
                "unlisted",
                false,
                new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero),
                "23",
                "en",
                "en",
                null,
                null,
                "etag-video456",
                null
            )
        };

        var mockPlaylistData = new List<PlaylistDto>
        {
            new("playlist123", "Test Playlist 1", "etag-playlist123"),
            new("playlist456", "Test Playlist 2", "etag-playlist456")
        };

        var mockPlaylistVideoIds = new Dictionary<string, List<string>>
        {
            ["playlist123"] = ["video123", "video456"],
            ["playlist456"] = ["video456"]
        };

        // Setup MockYouTubeIntegration
        fixture.ApiFactory.MockYouTubeIntegration
            .Setup(x => x.GetAllVideosAsync(testUploadsPlaylistId, It.IsAny<DateTimeOffset?>(),
                It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockVideos));

        fixture.ApiFactory.MockYouTubeIntegration
            .Setup(x => x.GetPlaylistsAsync(testChannelId, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockPlaylistData));

        fixture.ApiFactory.MockYouTubeIntegration
            .Setup(x => x.GetPlaylistVideoIdsAsync("playlist123", It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockPlaylistVideoIds["playlist123"]));

        fixture.ApiFactory.MockYouTubeIntegration
            .Setup(x => x.GetPlaylistVideoIdsAsync("playlist456", It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockPlaylistVideoIds["playlist456"]));

        fixture.ApiFactory.MockYouTubeIntegration
            .Setup(x => x.GetVideosAsync(It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockVideos.ToList().AsReadOnly());

        // Act
        var response = await fixture.HttpClient.PostAsync($"/api/channels/sync/{testChannelName}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var syncResult = JsonSerializer.Deserialize<ChannelSyncResult>(responseContent,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        syncResult.Should().NotBeNull();
        syncResult.VideosInserted.Should().Be(2);
        syncResult.VideosUpdated.Should().Be(0);
        syncResult.PlaylistsInserted.Should().Be(2);
        syncResult.PlaylistsUpdated.Should().Be(0);
        syncResult.MembershipsAdded.Should().Be(3);

        // Assert database state
        using var verificationScope = fixture.ApiServices.CreateScope();
        var verificationDatabaseContext = verificationScope.ServiceProvider.GetRequiredService<YouTubesterDb>();

        // Verify videos were created
        var createdVideos = await verificationDatabaseContext.Videos
            .AsNoTracking()
            .Where(v => v.UploadsPlaylistId == testUploadsPlaylistId)
            .OrderBy(v => v.VideoId)
            .ToListAsync();

        createdVideos.Should().HaveCount(2);

        var firstVideo = createdVideos.First(v => v.VideoId == "video123");
        firstVideo.Title.Should().Be("Test Video 1");
        firstVideo.Duration.Should().Be(TimeSpan.FromMinutes(5));
        firstVideo.Visibility.Should().Be(VideoVisibility.Public);
        firstVideo.PublishedAt.Should().Be(new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero));

        var secondVideo = createdVideos.First(v => v.VideoId == "video456");
        secondVideo.Title.Should().Be("Test Video 2");
        secondVideo.Duration.Should().Be(TimeSpan.FromMinutes(10));
        secondVideo.Visibility.Should().Be(VideoVisibility.Unlisted);
        secondVideo.PublishedAt.Should().Be(new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero));

        // Verify playlists were created
        var createdPlaylists = await verificationDatabaseContext.Playlists
            .AsNoTracking()
            .Where(p => p.ChannelId == testChannelId)
            .OrderBy(p => p.PlaylistId)
            .ToListAsync();

        createdPlaylists.Should().HaveCount(2);
        createdPlaylists[0].PlaylistId.Should().Be("playlist123");
        createdPlaylists[0].Title.Should().Be("Test Playlist 1");
        createdPlaylists[1].PlaylistId.Should().Be("playlist456");
        createdPlaylists[1].Title.Should().Be("Test Playlist 2");

        // Verify playlist memberships were created
        var memberships = await verificationDatabaseContext.VideoPlaylists
            .AsNoTracking()
            .OrderBy(vp => vp.PlaylistId)
            .ThenBy(vp => vp.VideoId)
            .ToListAsync();

        memberships.Should().HaveCount(3);
        memberships.Should().Contain(vp => vp.PlaylistId == "playlist123" && vp.VideoId == "video123");
        memberships.Should().Contain(vp => vp.PlaylistId == "playlist123" && vp.VideoId == "video456");
        memberships.Should().Contain(vp => vp.PlaylistId == "playlist456" && vp.VideoId == "video456");

        // Verify channel uploads cutoff was updated
        var updatedChannel = await verificationDatabaseContext.Channels
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ChannelId == testChannelId);

        updatedChannel.Should().NotBeNull();
        updatedChannel.LastUploadsCutoff.Should().NotBeNull();
        updatedChannel.LastUploadsCutoff.Should().Be(new DateTimeOffset(2024, 1, 2, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Sync_CalledTwice_IsIdempotentAndUpdatesExistingData()
    {
        // Arrange
        await fixture.ResetDbAsync();

        const string testChannelId = "UCTestChannel987654321";
        const string testChannelName = "TestChannelIdempotent";
        const string testUploadsPlaylistId = "PLTestUploads987654321";

        // Insert dummy channel into database
        var dummyChannel = Channel.Create(
            testChannelId,
            testChannelName,
            testUploadsPlaylistId,
            TestFixture.TestingDateTimeOffset
        );

        using (var scope = fixture.ApiServices.CreateScope())
        {
            var databaseContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            databaseContext.Channels.Add(dummyChannel);
            await databaseContext.SaveChangesAsync();
        }

        // Create mock video DTOs
        var mockVideosFirstCall = new List<VideoDto>
        {
            new(
                "video789",
                "Original Title",
                "Original Description",
                ["tag1"],
                TimeSpan.FromMinutes(3),
                "public",
                false,
                new DateTimeOffset(2024, 2, 1, 12, 0, 0, TimeSpan.Zero),
                "22",
                "en",
                "en",
                null,
                null,
                "etag-video789-v1",
                null
            )
        };

        var mockVideosSecondCall = new List<VideoDto>
        {
            new(
                "video789",
                "Updated Title",
                "Updated Description",
                ["tag1"],
                TimeSpan.FromMinutes(3),
                "public",
                false,
                new DateTimeOffset(2024, 2, 1, 12, 0, 0, TimeSpan.Zero),
                "22",
                "en",
                "en",
                null,
                null,
                "etag-video789-v2",
                null
            )
        };

        var mockPlaylistData = new List<PlaylistDto> { new("playlist789", "Test Playlist", "etag-video789-v1") };

        var mockPlaylistVideoIds = new Dictionary<string, List<string>> { ["playlist789"] = ["video789"] };

        // Setup MockYouTubeIntegration for first call
        fixture.ApiFactory.MockYouTubeIntegration
            .SetupSequence(x =>
                x.GetAllVideosAsync(testUploadsPlaylistId, It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockVideosFirstCall))
            .Returns(CreateAsyncEnumerable(mockVideosSecondCall));

        fixture.ApiFactory.MockYouTubeIntegration
            .Setup(x => x.GetPlaylistsAsync(testChannelId, It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockPlaylistData));

        fixture.ApiFactory.MockYouTubeIntegration
            .Setup(x => x.GetPlaylistVideoIdsAsync("playlist789", It.IsAny<CancellationToken>()))
            .Returns(CreateAsyncEnumerable(mockPlaylistVideoIds["playlist789"]));

        fixture.ApiFactory.MockYouTubeIntegration
            .SetupSequence(x =>
                x.GetVideosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockVideosFirstCall.ToList().AsReadOnly())
            .ReturnsAsync(mockVideosSecondCall.ToList().AsReadOnly());

        // Act - First call
        var firstResponse = await fixture.HttpClient.PostAsync($"/api/channels/sync/{testChannelName}", null);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Second call
        var secondResponse = await fixture.HttpClient.PostAsync($"/api/channels/sync/{testChannelName}", null);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var secondResponseContent = await secondResponse.Content.ReadAsStringAsync();
        var secondSyncResult = JsonSerializer.Deserialize<ChannelSyncResult>(secondResponseContent,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Assert
        secondSyncResult.Should().NotBeNull();
        secondSyncResult.VideosInserted.Should().Be(0);
        secondSyncResult.VideosUpdated.Should().Be(1);

        // Verify only one video exists and it was updated
        using var verificationScope = fixture.ApiServices.CreateScope();
        var verificationDatabaseContext = verificationScope.ServiceProvider.GetRequiredService<YouTubesterDb>();

        var videos = await verificationDatabaseContext.Videos
            .AsNoTracking()
            .Where(v => v.UploadsPlaylistId == testUploadsPlaylistId)
            .ToListAsync();

        videos.Should().HaveCount(1);
        videos[0].VideoId.Should().Be("video789");
        videos[0].Title.Should().Be("Updated Title");
    }

    private static async IAsyncEnumerable<T> CreateAsyncEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}