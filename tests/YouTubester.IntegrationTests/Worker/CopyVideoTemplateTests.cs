using System.Net.Http.Json;
using AutoFixture;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.Domain;
using YouTubester.IntegrationTests.TestHost;
using YouTubester.Persistence;

namespace YouTubester.IntegrationTests.Worker;

[Collection(nameof(TestCollection))]
public class CopyVideoTemplateTests(TestFixture fixture)
{
    [Fact]
    public async Task CopyVideoTemplateJob_Runs_Successfully()
    {
        // Arrange
        await fixture.ResetDbAsync();
        MockUpdateVideoAsync();
        RefactorAddVideoToPlaylistAsync();

        var sourceVideo = GetSourceVideo();
        var targetVideo = GetTargetVideo();

        const string channelId = "Channel-XYZ";
        const string userId = MockAuthenticationExtensions.TestSub;
        var playlistA =
            Playlist.Create("PL-A", channelId, "Playlist A", TestFixture.TestingDateTimeOffset, "etag-pl-a");
        var playlistB =
            Playlist.Create("PL-B", channelId, "Playlist B", TestFixture.TestingDateTimeOffset, "etag-pl-b");

        var srcInA = VideoPlaylist.Create(sourceVideo.VideoId, playlistA.PlaylistId);
        var tgtInB = VideoPlaylist.Create(targetVideo.VideoId, playlistB.PlaylistId); // target only in B
        // Create source and target videos in database
        using (var scope = fixture.ApiServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            var user = User.Create(
                userId,
                MockAuthenticationExtensions.TestEmail,
                MockAuthenticationExtensions.TestName,
                MockAuthenticationExtensions.TestPicture,
                TestFixture.TestingDateTimeOffset);
            await dbContext.Users.AddAsync(user);
            await dbContext.Channels.AddAsync(Channel.Create(channelId, userId, "Channel A",
                targetVideo.UploadsPlaylistId, TestFixture.TestingDateTimeOffset));
            dbContext.Videos.AddRange(sourceVideo, targetVideo);
            dbContext.Playlists.AddRange(playlistA, playlistB);
            dbContext.VideoPlaylists.AddRange(srcInA, tgtInB);
            await dbContext.SaveChangesAsync();
        }

        var testRequest = new CopyVideoTemplateRequest(sourceVideo.VideoId, targetVideo.VideoId);

        var response = await fixture.HttpClient.PostAsJsonAsync("/api/videos/copy-template", testRequest,
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        // Act & Assert - Worker host builds and services are available
        Assert.NotNull(fixture.WorkerServices);

        // Verify we can resolve job types from DI
        var copyTemplateJob = fixture.WorkerServices.GetRequiredService<CopyVideoTemplateJob>();
        Assert.NotNull(copyTemplateJob);

        // Verify capturing client is registered
        var jobClient = fixture.WorkerServices.GetRequiredService<IBackgroundJobClient>();
        Assert.IsType<CapturingBackgroundJobClient>(jobClient);
        var capturingClient = (CapturingBackgroundJobClient)jobClient;

        await capturingClient.RunAllAsync<CopyVideoTemplateJob>(fixture.WorkerServices);

        fixture.WorkerFactory.MockYouTubeIntegration.Verify(m => m.UpdateVideoAsync(
                targetVideo.VideoId,
                sourceVideo.Title!,
                sourceVideo.Description!,
                sourceVideo.Tags,
                sourceVideo.CategoryId,
                sourceVideo.DefaultLanguage,
                sourceVideo.DefaultAudioLanguage,
                It.Is<(double lat, double lng)?>(v => v.HasValue && v.Value.lat == sourceVideo.Location!.Latitude
                                                                 && v.Value.lng == sourceVideo.Location.Longitude),
                sourceVideo.LocationDescription,
                It.IsAny<CancellationToken>()),
            Times.Once);

        fixture.WorkerFactory.MockYouTubeIntegration.Verify(m => m.AddVideoToPlaylistAsync(
            playlistA.PlaylistId, targetVideo.VideoId, It.IsAny<CancellationToken>()),
            Times.Once);

        using (var scope = fixture.WorkerServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            var updatedTarget = await db.Videos.FindAsync(targetVideo.VideoId);
            Assert.NotNull(updatedTarget);
            Assert.Equal(sourceVideo.Title, updatedTarget.Title);
            Assert.Equal(sourceVideo.Description, updatedTarget.Description);
            Assert.Equal(sourceVideo.Tags, updatedTarget.Tags);
            Assert.Equal(sourceVideo.CategoryId, updatedTarget.CategoryId);
            Assert.Equal(sourceVideo.DefaultLanguage, updatedTarget.DefaultLanguage);
            Assert.Equal(sourceVideo.DefaultAudioLanguage, updatedTarget.DefaultAudioLanguage);
            Assert.Equal(sourceVideo.Location, updatedTarget.Location);
            Assert.Equal(sourceVideo.LocationDescription, updatedTarget.LocationDescription);

            var srcMemberships = await db.VideoPlaylists
                .Where(vp => vp.VideoId == sourceVideo.VideoId)
                .Select(vp => vp.PlaylistId)
                .OrderBy(x => x)
                .ToListAsync();

            var tgtMemberships = await db.VideoPlaylists
                .Where(vp => vp.VideoId == targetVideo.VideoId)
                .Select(vp => vp.PlaylistId)
                .OrderBy(x => x)
                .ToListAsync();

            // Source remains in A & B
            Assert.Equal(["PL-A"], srcMemberships);

            // Target remains only in B (copy-template job does not change memberships)
            Assert.Equal(["PL-A"], tgtMemberships);
        }
    }

    [Fact]
    public async Task CopyVideoTemplateJob_With_Ai_Runs_Successfully()
    {
        // Arrange
        await fixture.ResetDbAsync();
        MockUpdateVideoAsync();
        RefactorAddVideoToPlaylistAsync();

        var suggestedMetadata = (Title: "AI Title", Description: "AI Description", Tags: new[] { "AI tag B" });
        fixture.WorkerFactory.MockAiClient
            .Setup(m => m.SuggestMetadataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(suggestedMetadata);

        var sourceVideo = GetSourceVideo();
        var targetVideo = GetTargetVideo();

        const string channelId = "Channel-XYZ";
        const string userId = MockAuthenticationExtensions.TestSub;
        var playlistA =
            Playlist.Create("PL-A", channelId, "Playlist A", TestFixture.TestingDateTimeOffset, "etag-pl-a");
        var playlistB =
            Playlist.Create("PL-B", channelId, "Playlist B", TestFixture.TestingDateTimeOffset, "etag-pl-b");

        var srcInA = VideoPlaylist.Create(sourceVideo.VideoId, playlistA.PlaylistId);
        var tgtInB = VideoPlaylist.Create(targetVideo.VideoId, playlistB.PlaylistId); // target only in B
        // Create source and target videos in database
        using (var scope = fixture.ApiServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            var user = User.Create(
                userId,
                MockAuthenticationExtensions.TestEmail,
                MockAuthenticationExtensions.TestName,
                MockAuthenticationExtensions.TestPicture,
                TestFixture.TestingDateTimeOffset);
            await dbContext.Users.AddAsync(user);
            await dbContext.Channels.AddAsync(Channel.Create(channelId, userId, "Channel A",
                targetVideo.UploadsPlaylistId, TestFixture.TestingDateTimeOffset));
            dbContext.Videos.AddRange(sourceVideo, targetVideo);
            dbContext.Playlists.AddRange(playlistA, playlistB);
            dbContext.VideoPlaylists.AddRange(srcInA, tgtInB);
            await dbContext.SaveChangesAsync();
        }

        var testRequest = new CopyVideoTemplateRequest(sourceVideo.VideoId, targetVideo.VideoId,
            AiSuggestionOptions: new AiSuggestionOptions("promptEnrichment"));

        var response = await fixture.HttpClient.PostAsJsonAsync("/api/videos/copy-template", testRequest,
            CancellationToken.None);
        response.EnsureSuccessStatusCode();
        // Act & Assert - Worker host builds and services are available
        Assert.NotNull(fixture.WorkerServices);

        // Verify we can resolve job types from DI
        var copyTemplateJob = fixture.WorkerServices.GetRequiredService<CopyVideoTemplateJob>();
        Assert.NotNull(copyTemplateJob);

        // Verify capturing client is registered
        var jobClient = fixture.WorkerServices.GetRequiredService<IBackgroundJobClient>();
        Assert.IsType<CapturingBackgroundJobClient>(jobClient);
        var capturingClient = (CapturingBackgroundJobClient)jobClient;

        await capturingClient.RunAllAsync<CopyVideoTemplateJob>(fixture.WorkerServices);

        fixture.WorkerFactory.MockYouTubeIntegration.Verify(m => m.UpdateVideoAsync(
                targetVideo.VideoId,
                suggestedMetadata.Title,
                suggestedMetadata.Description,
                suggestedMetadata.Tags,
                sourceVideo.CategoryId,
                sourceVideo.DefaultLanguage,
                sourceVideo.DefaultAudioLanguage,
                It.Is<(double lat, double lng)?>(v => v.HasValue && v.Value.lat == sourceVideo.Location!.Latitude
                                                                 && v.Value.lng == sourceVideo.Location.Longitude),
                sourceVideo.LocationDescription,
                It.IsAny<CancellationToken>()),
            Times.Once);

        fixture.WorkerFactory.MockYouTubeIntegration.Verify(m => m.AddVideoToPlaylistAsync(
            playlistA.PlaylistId, targetVideo.VideoId, It.IsAny<CancellationToken>()),
            Times.Once);

        using (var scope = fixture.WorkerServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            var updatedTarget = await db.Videos.FindAsync(targetVideo.VideoId);
            Assert.NotNull(updatedTarget);
            Assert.Equal(suggestedMetadata.Title, updatedTarget.Title);
            Assert.Equal(suggestedMetadata.Description, updatedTarget.Description);
            Assert.Equal(suggestedMetadata.Tags, updatedTarget.Tags);
            Assert.Equal(sourceVideo.CategoryId, updatedTarget.CategoryId);
            Assert.Equal(sourceVideo.DefaultLanguage, updatedTarget.DefaultLanguage);
            Assert.Equal(sourceVideo.DefaultAudioLanguage, updatedTarget.DefaultAudioLanguage);
            Assert.Equal(sourceVideo.Location, updatedTarget.Location);
            Assert.Equal(sourceVideo.LocationDescription, updatedTarget.LocationDescription);

            var srcMemberships = await db.VideoPlaylists
                .Where(vp => vp.VideoId == sourceVideo.VideoId)
                .Select(vp => vp.PlaylistId)
                .OrderBy(x => x)
                .ToListAsync();

            var tgtMemberships = await db.VideoPlaylists
                .Where(vp => vp.VideoId == targetVideo.VideoId)
                .Select(vp => vp.PlaylistId)
                .OrderBy(x => x)
                .ToListAsync();

            // Source remains in A & B
            Assert.Equal(["PL-A"], srcMemberships);

            // Target remains only in B (copy-template job does not change memberships)
            Assert.Equal(["PL-A"], tgtMemberships);
        }
    }

    private void RefactorAddVideoToPlaylistAsync()
    {
        fixture.WorkerFactory.MockYouTubeIntegration
            .Setup(m => m.AddVideoToPlaylistAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void MockUpdateVideoAsync()
    {
        fixture.WorkerFactory.MockYouTubeIntegration
            .Setup(m => m.UpdateVideoAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<(double lat, double lng)?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private Video GetTargetVideo()
    {
        return Video.Create(
            "ULTestPlaylist456",
            $"target{fixture.Auto.Create<string>()}"[..11],
            "Target Video Title",
            "Target Video Description",
            TestFixture.TestingDateTimeOffset.AddDays(-2),
            TimeSpan.FromMinutes(3),
            VideoVisibility.Private,
            ["target"],
            "23",
            "fr",
            "fr",
            null,
            null,
            TestFixture.TestingDateTimeOffset,
            "etag-target",
            false
        );
    }

    private Video GetSourceVideo()
    {
        return Video.Create(
            "ULTestPlaylist123",
            $"source{fixture.Auto.Create<string>()}"[..11],
            "Source Video Title",
            "Source Video Description",
            TestFixture.TestingDateTimeOffset.AddDays(-1),
            TimeSpan.FromMinutes(5),
            VideoVisibility.Public,
            ["source", "template"],
            "22",
            "en",
            "en",
            new GeoLocation(37.7749, -122.4194),
            "San Francisco, CA",
            TestFixture.TestingDateTimeOffset,
            "etag-source",
            true
        );
    }
}