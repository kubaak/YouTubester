using AutoFixture;
using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.Integration.Dtos;
using YouTubester.IntegrationTests.TestHost;

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

        var dummySourceUrl = _fixture.Auto.Create<string>()[..11];
        var dummyTargetUrl = _fixture.Auto.Create<string>()[..11];

        var testRequest = _fixture.Auto.Build<CopyVideoTemplateRequest>()
            .With(p => p.SourceUrl, $"{LongVideoUrlBase}{dummySourceUrl}")
            .With(p => p.TargetUrl, $"{LongVideoUrlBase}{dummyTargetUrl}")
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