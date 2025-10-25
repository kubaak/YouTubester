using AutoFixture;
using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.Domain;
using YouTubester.Integration.Dtos;
using YouTubester.IntegrationTests.TestHost;
using YouTubester.Persistence;

namespace YouTubester.IntegrationTests.Worker;

[Collection(nameof(TestCollection))]
public class WorkerHost_SmokeTests
{
    private readonly TestFixture _fixture;

    public WorkerHost_SmokeTests(TestFixture fixture)
    {
        _fixture = fixture;
        _fixture.WorkerFactory.MockYouTubeIntegration.Setup(m =>
                m.GetVideoDetailsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(_fixture.Auto
                .Create<VideoDetailsDto>());
    }

    private const string LongVideoUrlBase = "https://www.youtube.com/watch?v=";

    [Fact]
    public async Task CopyVideoTemplateJob_Runs_Successfully()
    {
        // Arrange
        await _fixture.ResetDbAsync();
        _fixture.WorkerFactory.MockYouTubeIntegration
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

        var dummySourceVideoId = _fixture.Auto.Create<string>()[..11];
        var dummyTargetVideoId = _fixture.Auto.Create<string>()[..11];

        // Create source and target videos in database
        using (var scope = _fixture.ApiServices.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();

            var sourceVideo = Video.Create(
                "ULTestPlaylist123",
                dummySourceVideoId,
                "Source Video Title",
                "Source Video Description",
                TestFixture.TestingDateTimeOffset.AddDays(-1),
                TimeSpan.FromMinutes(5),
                VideoVisibility.Public,
                new[] { "source", "template" },
                "22",
                "en",
                "en",
                new GeoLocation(37.7749, -122.4194),
                "San Francisco, CA",
                TestFixture.TestingDateTimeOffset,
                "etag-source",
                true
            );

            var targetVideo = Video.Create(
                "ULTestPlaylist456",
                dummyTargetVideoId,
                "Target Video Title",
                "Target Video Description",
                TestFixture.TestingDateTimeOffset.AddDays(-2),
                TimeSpan.FromMinutes(3),
                VideoVisibility.Private,
                new[] { "target" },
                "23",
                "fr",
                "fr",
                null,
                null,
                TestFixture.TestingDateTimeOffset,
                "etag-target",
                false
            );

            dbContext.Videos.AddRange(sourceVideo, targetVideo);
            await dbContext.SaveChangesAsync();
        }

        // Act & Assert - Worker host builds and services are available
        _fixture.WorkerServices.Should().NotBeNull();

        // Verify we can resolve job types from DI
        var copyTemplateJob = _fixture.WorkerServices.GetRequiredService<CopyVideoTemplateJob>();
        copyTemplateJob.Should().NotBeNull();

        var postRepliesJob = _fixture.WorkerServices.GetRequiredService<PostApprovedRepliesJob>();
        postRepliesJob.Should().NotBeNull();

        // Verify capturing client is registered
        var jobClient = _fixture.WorkerServices.GetRequiredService<IBackgroundJobClient>();
        jobClient.Should().BeOfType<CapturingBackgroundJobClient>();
        var capturingClient = (CapturingBackgroundJobClient)jobClient;

        var testRequest = _fixture.Auto.Build<CopyVideoTemplateRequest>()
            .With(p => p.SourceVideoId, dummySourceVideoId)
            .With(p => p.TargetVideoId, dummyTargetVideoId)
            .With(p => p.AiSuggestionOptions, (AiSuggestionOptions?)null)
            .Create();

        capturingClient.Enqueue<CopyVideoTemplateJob>(job =>
            job.Run(testRequest, new Mock<IJobCancellationToken>().Object));

        // Verify the job was captured
        var enqueuedJobs = capturingClient.GetEnqueued<CopyVideoTemplateJob>();
        enqueuedJobs.Should().HaveCount(1);
        var job = enqueuedJobs.Single();
        job.Job.Method.Name.Should().Be("Run");
        job.Job.Args.Should().HaveCount(2);
        job.Job.Args[0].Should().BeEquivalentTo(testRequest);

        await capturingClient.RunAll<CopyVideoTemplateJob>(_fixture.WorkerServices);
    }
}