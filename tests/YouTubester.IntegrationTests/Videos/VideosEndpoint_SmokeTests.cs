using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;
using YouTubester.Application.Contracts;
using YouTubester.Application.Contracts.Videos;
using YouTubester.IntegrationTests.TestHost;

namespace YouTubester.IntegrationTests.Videos;

[Collection(nameof(TestCollection))]
public class VideosEndpoint_SmokeTests
{
    private readonly TestFixture _fixture;

    public VideosEndpoint_SmokeTests(TestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetVideos_EmptyDb_ReturnsEmptyList()
    {
        // Arrange
        await _fixture.ResetDbAsync();

        // Act
        var response = await _fixture.HttpClient.GetAsync("/api/videos?pageSize=5");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task GetVideos_WithInvalidVisibility_ReturnsBadRequest()
    {
        // Arrange
        await _fixture.ResetDbAsync();

        // Act
        var response = await _fixture.HttpClient.GetAsync("/api/videos?visibility=InvalidValue");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetVideos_WithValidVisibilityFilter_ReturnsOk()
    {
        // Arrange
        await _fixture.ResetDbAsync();

        // Act
        var response = await _fixture.HttpClient.GetAsync("/api/videos?visibility=Public&visibility=Unlisted");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty(); // Empty database
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task GetVideos_WithCommaSeparatedVisibility_ReturnsOk()
    {
        // Arrange
        await _fixture.ResetDbAsync();

        // Act
        var response = await _fixture.HttpClient.GetAsync("/api/videos?visibility=Public,Private");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty(); // Empty database
        result.NextPageToken.Should().BeNull();
    }

    [Fact]
    public async Task GetVideos_CaseInsensitiveVisibility_ReturnsOk()
    {
        // Arrange
        await _fixture.ResetDbAsync();

        // Act
        var response = await _fixture.HttpClient.GetAsync("/api/videos?visibility=public&visibility=UNLISTED");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<PagedResult<VideoListItemDto>>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty(); // Empty database
        result.NextPageToken.Should().BeNull();
    }
}