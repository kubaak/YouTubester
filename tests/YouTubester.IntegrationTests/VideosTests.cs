using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using YouTubester.Application;
using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Videos;
using YouTubester.Domain;
using YouTubester.IntegrationTests.TestHost;
using YouTubester.Persistence;

namespace YouTubester.IntegrationTests;

[Collection(nameof(TestCollection))]
public class VideosTests(TestFixture fixture)
{
    private readonly JsonSerializerOptions _serializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task GetVideos_EmptyDb_ReturnsEmptyList()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/videos?pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, _serializerOptions);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task GetVideos_WithInvalidVisibility_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/videos?visibility=InvalidValue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetVideos_WithValidVisibilityFilter_ReturnsOk()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/videos?visibility=Public&visibility=Unlisted");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, _serializerOptions);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task GetVideos_CaseInsensitiveVisibility_ReturnsOk()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/videos?visibility=public&visibility=UNLISTED");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, _serializerOptions);

        result.Should().NotBeNull();
        result.Items.Should().BeEmpty();
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task CopyTemplate_ValidRequest_ReturnsJobId()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var request = new CopyVideoTemplateRequest(
            "sourceVideoId123",
            "targetVideoId456",
            true,
            false,
            true,
            false,
            true,
            null
        );

        var json = JsonSerializer.Serialize(request, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/videos/copy-template", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().NotBeNullOrWhiteSpace();

        // Verify job was enqueued
        var enqueuedJobs = fixture.CapturingJobClient.GetEnqueued<Application.Jobs.CopyVideoTemplateJob>();
        enqueuedJobs.Should().HaveCount(1);
    }

    [Fact]
    public async Task CopyTemplate_EmptySourceVideoId_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var request = new CopyVideoTemplateRequest(
            "",
            "targetVideoId456"
        );

        var json = JsonSerializer.Serialize(request, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/videos/copy-template", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("SourceVideoId is required");
    }

    [Fact]
    public async Task CopyTemplate_EmptyTargetVideoId_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var request = new CopyVideoTemplateRequest(
            "sourceVideoId123",
            ""
        );

        var json = JsonSerializer.Serialize(request, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/videos/copy-template", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("TargetVideoId is required");
    }

    [Fact]
    public async Task CopyTemplate_SameSourceAndTargetVideoId_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var request = new CopyVideoTemplateRequest(
            "sameVideoId123",
            "sameVideoId123"
        );

        var json = JsonSerializer.Serialize(request, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/videos/copy-template", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("SourceVideoId and TargetVideoId must be different");
    }

    [Fact]
    public async Task GetVideos_WithVideosInDb_ReturnsFilteredAndPagedResults()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var testUploadsPlaylistId = "PLTestUploads123";

        var video1 = Video.Create(
            testUploadsPlaylistId,
            "video1",
            "Cooking Tutorial",
            "Learn how to cook",
            TestFixture.TestingDateTimeOffset,
            TimeSpan.FromMinutes(10),
            VideoVisibility.Public,
            new[] { "cooking", "tutorial" },
            "22",
            "en",
            "en",
            null,
            null,
            TestFixture.TestingDateTimeOffset,
            "etag1"
        );

        var video2 = Video.Create(
            testUploadsPlaylistId,
            "video2",
            "Gaming Highlights",
            "Best gaming moments",
            TestFixture.TestingDateTimeOffset.AddDays(1),
            TimeSpan.FromMinutes(15),
            VideoVisibility.Unlisted,
            new[] { "gaming", "highlights" },
            "23",
            "en",
            "en",
            null,
            null,
            TestFixture.TestingDateTimeOffset,
            "etag2"
        );

        var video3 = Video.Create(
            testUploadsPlaylistId,
            "video3",
            "Private Video",
            "This is private",
            TestFixture.TestingDateTimeOffset.AddDays(2),
            TimeSpan.FromMinutes(5),
            VideoVisibility.Private,
            new[] { "private" },
            "24",
            "en",
            "en",
            null,
            null,
            TestFixture.TestingDateTimeOffset,
            "etag3"
        );

        using (var scope = fixture.ApiServices.CreateScope())
        {
            var databaseContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            databaseContext.Videos.AddRange(video1, video2, video3);
            await databaseContext.SaveChangesAsync();
        }

        // Act - Filter by title
        var titleResponse = await fixture.HttpClient.GetAsync("/api/videos?title=cooking");

        // Assert - Title filter
        titleResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var titleContent = await titleResponse.Content.ReadAsStringAsync();
        var titleResult = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(titleContent, _serializerOptions);

        titleResult.Should().NotBeNull();
        titleResult!.Items.Should().HaveCount(1);
        titleResult.Items.First().Title.Should().Be("Cooking Tutorial");

        // Act - Filter by visibility
        var visibilityResponse = await fixture.HttpClient.GetAsync("/api/videos?visibility=Public&visibility=Unlisted");

        // Assert - Visibility filter
        visibilityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var visibilityContent = await visibilityResponse.Content.ReadAsStringAsync();
        var visibilityResult =
            JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(visibilityContent, _serializerOptions);

        visibilityResult.Should().NotBeNull();
        visibilityResult!.Items.Should().HaveCount(2);
        visibilityResult.Items.Should().NotContain(v => v.Title == "Private Video");

        // Act - Test pagination
        var paginationResponse = await fixture.HttpClient.GetAsync("/api/videos?pageSize=2");

        // Assert - Pagination
        paginationResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var paginationContent = await paginationResponse.Content.ReadAsStringAsync();
        var paginationResult =
            JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(paginationContent, _serializerOptions);

        paginationResult.Should().NotBeNull();
        paginationResult!.Items.Should().HaveCount(2);
        paginationResult.NextPageToken.Should().NotBeNull();
    }

    [Fact]
    public async Task GetVideos_WithInvalidPageSize_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/videos?pageSize=150"); // Exceeds maximum

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("error");
    }

    [Fact]
    public async Task GetVideos_WithInvalidPageToken_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/videos?pageToken=invalid-token");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("error");
    }

    [Fact]
    public async Task GetVideos_WithNumericVisibilityValues_ReturnsOk()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act - Using numeric values for visibility (Public=0, Unlisted=1)
        var response = await fixture.HttpClient.GetAsync("/api/videos?visibility=0&visibility=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, _serializerOptions);

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty(); // No videos in DB, but request should be valid
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