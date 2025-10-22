using FluentAssertions;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.IntegrationTests.TestHost;

namespace YouTubester.IntegrationTests.Worker;

[Collection(nameof(TestCollection))]
public class WorkerHost_SmokeTests
{
    private readonly TestFixture _fixture;

    public WorkerHost_SmokeTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Worker_BootsAndCanResolveJobs_WithoutServer()
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

        // Simulate enqueueing a job (like the API would do)
        var testRequest = new CopyVideoTemplateRequest(
            "https://youtube.com/watch?v=source123",
            "https://youtube.com/watch?v=target456"
        );

        capturingClient.Enqueue<CopyVideoTemplateJob>(job => job.Run(testRequest, JobCancellationToken.Null));

        // Verify the job was captured
        var enqueuedJobs = capturingClient.GetEnqueued<CopyVideoTemplateJob>();
        enqueuedJobs.Should().HaveCount(1);
        enqueuedJobs.First().Job.Method.Name.Should().Be("Run");
        enqueuedJobs.First().Job.Args.Should().HaveCount(2);
        enqueuedJobs.First().Job.Args[0].Should().BeEquivalentTo(testRequest);

        // Verify we can execute the job (though it may fail due to mocked dependencies)
        // This tests that the DI container is properly configured
        var act = async () => await capturingClient.RunAll<CopyVideoTemplateJob>(_fixture.WorkerServices, CancellationToken.None);

        // We expect this to either succeed or fail gracefully with a recognizable exception
        // (not a DI resolution error), since we have mocked dependencies that aren't set up for this test
        try
        {
            await act();
            // If it succeeds, great! The infrastructure is working
        }
        catch (Exception ex)
        {
            // If it fails, it should be due to business logic/mocked dependencies, not DI issues
            ex.Should().NotBeOfType<InvalidOperationException>();
            ex.Message.Should().NotContain("Unable to resolve service");
        }
    }

    [Fact]
    public void Worker_CanClearCapturedJobs()
    {
        // Arrange
        var capturingClient = _fixture.WorkerFactory.CapturingJobClient;
        capturingClient.Clear(); // Clear any jobs from previous tests
        
        var testRequest = new CopyVideoTemplateRequest(
            "https://youtube.com/watch?v=source123",
            "https://youtube.com/watch?v=target456"
        );

        // Enqueue some jobs
        capturingClient.Enqueue<CopyVideoTemplateJob>(job => job.Run(testRequest, JobCancellationToken.Null));
        capturingClient.Enqueue<PostApprovedRepliesJob>(job => job.Run("comment123", JobCancellationToken.Null));

        // Verify jobs are captured
        capturingClient.GetEnqueued<CopyVideoTemplateJob>().Should().HaveCount(1);

        // Act
        capturingClient.Clear();

        // Assert
        capturingClient.GetEnqueued<CopyVideoTemplateJob>().Should().BeEmpty();
        capturingClient.GetEnqueued<PostApprovedRepliesJob>().Should().BeEmpty();
    }

    [Fact]
    public void Worker_GeneratesDeterministicJobIds()
    {
        // Arrange
        var capturingClient = _fixture.WorkerFactory.CapturingJobClient;
        capturingClient.Clear(); // Clear any jobs from previous tests
        var testRequest = new CopyVideoTemplateRequest(
            "https://youtube.com/watch?v=source123",
            "https://youtube.com/watch?v=target456"
        );

        // Act
        var jobId1 = capturingClient.Enqueue<CopyVideoTemplateJob>(job => job.Run(testRequest, JobCancellationToken.Null));
        var jobId2 = capturingClient.Enqueue<CopyVideoTemplateJob>(job => job.Run(testRequest, JobCancellationToken.Null));

        // Assert
        jobId1.Should().Be("1");
        jobId2.Should().Be("2");
        
        var enqueuedJobs = capturingClient.GetEnqueued<CopyVideoTemplateJob>();
        enqueuedJobs.Should().HaveCount(2);
        enqueuedJobs[0].JobId.Should().Be("1");
        enqueuedJobs[1].JobId.Should().Be("2");
    }
}