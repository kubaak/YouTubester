using System.Net.Http.Json;
using AutoFixture;
using FluentAssertions;
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
            await dbContext.Channels.AddAsync(Channel.Create(channelId, "Channel A", "UploadPlaylistId",
                TestFixture.TestingDateTimeOffset));
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
        fixture.WorkerServices.Should().NotBeNull();

        // Verify we can resolve job types from DI
        var copyTemplateJob = fixture.WorkerServices.GetRequiredService<CopyVideoTemplateJob>();
        copyTemplateJob.Should().NotBeNull();

        // Verify capturing client is registered
        var jobClient = fixture.WorkerServices.GetRequiredService<IBackgroundJobClient>();
        jobClient.Should().BeOfType<CapturingBackgroundJobClient>();
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
            playlistA.PlaylistId, targetVideo.VideoId, It.IsAny<CancellationToken>()), Times.Once);

        using (var scope = fixture.WorkerServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            var updatedTarget = await db.Videos.FindAsync(targetVideo.VideoId);
            updatedTarget.Should().NotBeNull();
            updatedTarget.Title.Should().Be(sourceVideo.Title);
            updatedTarget.Description.Should().Be(sourceVideo.Description);
            updatedTarget.Tags.Should()
                .BeEquivalentTo(sourceVideo.Tags, opts => opts.WithStrictOrdering());
            updatedTarget.CategoryId.Should().Be(sourceVideo.CategoryId);
            updatedTarget.DefaultLanguage.Should().Be(sourceVideo.DefaultLanguage);
            updatedTarget.DefaultAudioLanguage.Should().Be(sourceVideo.DefaultAudioLanguage);
            updatedTarget.Location.Should().Be(sourceVideo.Location);
            updatedTarget.LocationDescription.Should().Be(sourceVideo.LocationDescription);

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
            srcMemberships.Should().BeEquivalentTo(["PL-A"], o => o.WithoutStrictOrdering());

            // Target remains only in B (copy-template job does not change memberships)
            tgtMemberships.Should().BeEquivalentTo(["PL-A"], o => o.WithoutStrictOrdering());
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
            await dbContext.Channels.AddAsync(Channel.Create(channelId, "Channel A", "UploadPlaylistId",
                TestFixture.TestingDateTimeOffset));
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
        fixture.WorkerServices.Should().NotBeNull();

        // Verify we can resolve job types from DI
        var copyTemplateJob = fixture.WorkerServices.GetRequiredService<CopyVideoTemplateJob>();
        copyTemplateJob.Should().NotBeNull();

        // Verify capturing client is registered
        var jobClient = fixture.WorkerServices.GetRequiredService<IBackgroundJobClient>();
        jobClient.Should().BeOfType<CapturingBackgroundJobClient>();
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
            playlistA.PlaylistId, targetVideo.VideoId, It.IsAny<CancellationToken>()), Times.Once);

        using (var scope = fixture.WorkerServices.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            var updatedTarget = await db.Videos.FindAsync(targetVideo.VideoId);
            updatedTarget.Should().NotBeNull();
            updatedTarget.Title.Should().Be(suggestedMetadata.Title);
            updatedTarget.Description.Should().Be(suggestedMetadata.Description);
            updatedTarget.Tags.Should()
                .BeEquivalentTo(suggestedMetadata.Tags, opts => opts.WithStrictOrdering());
            updatedTarget.CategoryId.Should().Be(sourceVideo.CategoryId);
            updatedTarget.DefaultLanguage.Should().Be(sourceVideo.DefaultLanguage);
            updatedTarget.DefaultAudioLanguage.Should().Be(sourceVideo.DefaultAudioLanguage);
            updatedTarget.Location.Should().Be(sourceVideo.Location);
            updatedTarget.LocationDescription.Should().Be(sourceVideo.LocationDescription);

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
            srcMemberships.Should().BeEquivalentTo(["PL-A"], o => o.WithoutStrictOrdering());

            // Target remains only in B (copy-template job does not change memberships)
            tgtMemberships.Should().BeEquivalentTo(["PL-A"], o => o.WithoutStrictOrdering());
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