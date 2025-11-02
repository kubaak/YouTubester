using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using YouTubester.Application.Contracts.Replies;
using YouTubester.Domain;
using YouTubester.IntegrationTests.TestHost;
using YouTubester.Persistence;

namespace YouTubester.IntegrationTests;

[Collection(nameof(TestCollection))]
public class RepliesTests(TestFixture fixture)
{
    private readonly JsonSerializerOptions _serializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [Fact]
    public async Task GetReplies_EmptyDb_ReturnsEmptyList()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/replies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("[]");
    }

    [Fact]
    public async Task GetReplies_WithRepliesInDb_ReturnsRepliesForApproval()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var suggestedReply = Reply.Create(
            "comment1",
            "video1",
            "Test Video 1",
            "Original comment text",
            TestFixture.TestingDateTimeOffset);
        suggestedReply.SuggestText("Suggested reply text", TestFixture.TestingDateTimeOffset.AddMinutes(5));

        var pulledReply = Reply.Create(
            "comment2",
            "video1",
            "Test Video 1",
            "Another comment text",
            TestFixture.TestingDateTimeOffset);

        var postedReply = Reply.Create(
            "comment3",
            "video2",
            "Test Video 2",
            "Posted comment text",
            TestFixture.TestingDateTimeOffset);
        postedReply.SuggestText("Some reply", TestFixture.TestingDateTimeOffset.AddMinutes(5));
        postedReply.ApproveText("Final reply", TestFixture.TestingDateTimeOffset.AddMinutes(10));
        postedReply.Post(TestFixture.TestingDateTimeOffset.AddMinutes(15));

        using (var scope = fixture.ApiServices.CreateScope())
        {
            var databaseContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            databaseContext.Replies.AddRange(suggestedReply, pulledReply, postedReply);
            await databaseContext.SaveChangesAsync();
        }

        // Act
        var response = await fixture.HttpClient.GetAsync("/api/replies");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement[]>(content, _serializerOptions);

        result.Should().NotBeNull();
        result!.Should().HaveCount(1); // Only Suggested replies are returned for approval

        var comment1 = result.FirstOrDefault(r => r.GetProperty("commentId").GetString() == "comment1");
        comment1.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        comment1.GetProperty("status").GetInt32().Should().Be((int)ReplyStatus.Suggested);

        // comment2 (Pulled status) should not be returned for approval
        result.Should().NotContain(r => r.GetProperty("commentId").GetString() == "comment2");
        result.Should()
            .NotContain(r =>
                r.GetProperty("commentId").GetString() == "comment3"); // Posted replies should not be returned
    }

    [Fact]
    public async Task DeleteDraft_ExistingReply_ReturnsOkAndDeletesFromDb()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var reply = Reply.Create(
            "comment-to-delete",
            "video1",
            "Test Video",
            "Comment to be deleted",
            TestFixture.TestingDateTimeOffset);

        using (var scope = fixture.ApiServices.CreateScope())
        {
            var databaseContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            databaseContext.Replies.Add(reply);
            await databaseContext.SaveChangesAsync();
        }

        // Act
        var response = await fixture.HttpClient.DeleteAsync("/api/replies/comment-to-delete");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(content, _serializerOptions);

        result.ValueKind.Should().NotBe(JsonValueKind.Undefined);
        result.GetProperty("commentId").GetString().Should().Be("comment-to-delete");

        // Verify reply was deleted from database
        using var verificationScope = fixture.ApiServices.CreateScope();
        var verificationDatabaseContext = verificationScope.ServiceProvider.GetRequiredService<YouTubesterDb>();
        var deletedReply = await verificationDatabaseContext.Replies
            .FirstOrDefaultAsync(r => r.CommentId == "comment-to-delete");

        deletedReply.Should().BeNull();
    }

    [Fact]
    public async Task DeleteDraft_NonExistentReply_ReturnsNotFound()
    {
        // Arrange
        await fixture.ResetDbAsync();

        // Act
        var response = await fixture.HttpClient.DeleteAsync("/api/replies/non-existent-comment");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BatchApprove_ValidDecisions_ReturnsSuccessResult()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var reply1 = Reply.Create(
            "comment1",
            "video1",
            "Test Video",
            "First comment",
            TestFixture.TestingDateTimeOffset);
        reply1.SuggestText("Suggested text 1", TestFixture.TestingDateTimeOffset.AddMinutes(5));

        var reply2 = Reply.Create(
            "comment2",
            "video1",
            "Test Video",
            "Second comment",
            TestFixture.TestingDateTimeOffset);
        reply2.SuggestText("Suggested text 2", TestFixture.TestingDateTimeOffset.AddMinutes(5));

        using (var scope = fixture.ApiServices.CreateScope())
        {
            var databaseContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            databaseContext.Replies.AddRange(reply1, reply2);
            await databaseContext.SaveChangesAsync();
        }

        var decisions = new[]
        {
            new DraftDecisionDto("comment1", "Approved text 1"), new DraftDecisionDto("comment2", "Approved text 2")
        };

        var json = JsonSerializer.Serialize(decisions, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/replies/approve", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<BatchDecisionResultDto>(responseContent, _serializerOptions);

        result.Should().NotBeNull();
        result!.Total.Should().Be(2);
        result.Succeeded.Should().Be(2);
        result.Failed.Should().Be(0);
        result.Items.Should().HaveCount(2);

        // Verify replies were approved in database
        using var verificationScope = fixture.ApiServices.CreateScope();
        var verificationDatabaseContext = verificationScope.ServiceProvider.GetRequiredService<YouTubesterDb>();
        var approvedReplies = await verificationDatabaseContext.Replies
            .Where(r => r.CommentId == "comment1" || r.CommentId == "comment2")
            .ToListAsync();

        approvedReplies.Should().HaveCount(2);
        approvedReplies.Should().OnlyContain(r => r.Status == ReplyStatus.Approved);
        approvedReplies.Should().Contain(r => r.CommentId == "comment1" && r.FinalText == "Approved text 1");
        approvedReplies.Should().Contain(r => r.CommentId == "comment2" && r.FinalText == "Approved text 2");
    }

    [Fact]
    public async Task BatchApprove_EmptyDecisions_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var decisions = Array.Empty<DraftDecisionDto>();
        var json = JsonSerializer.Serialize(decisions, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/replies/approve", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BatchIgnore_ValidCommentIds_ReturnsSuccessResult()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var reply1 = Reply.Create(
            "comment1",
            "video1",
            "Test Video",
            "First comment",
            TestFixture.TestingDateTimeOffset);

        var reply2 = Reply.Create(
            "comment2",
            "video1",
            "Test Video",
            "Second comment",
            TestFixture.TestingDateTimeOffset);

        var postedReply = Reply.Create(
            "comment3",
            "video1",
            "Test Video",
            "Posted comment",
            TestFixture.TestingDateTimeOffset);
        postedReply.SuggestText("Some text", TestFixture.TestingDateTimeOffset.AddMinutes(5));
        postedReply.ApproveText("Final text", TestFixture.TestingDateTimeOffset.AddMinutes(10));
        postedReply.Post(TestFixture.TestingDateTimeOffset.AddMinutes(15));

        using (var scope = fixture.ApiServices.CreateScope())
        {
            var databaseContext = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();
            databaseContext.Replies.AddRange(reply1, reply2, postedReply);
            await databaseContext.SaveChangesAsync();
        }

        var commentIds = new[] { "comment1", "comment2", "comment3", "non-existent" };
        var json = JsonSerializer.Serialize(commentIds, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/replies/batch-ignore", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<BatchIgnoreResult>(responseContent, _serializerOptions);

        result.Should().NotBeNull();
        result!.Requested.Should().Be(4);
        result.Ignored.Should().Be(2); // comment1 and comment2
        result.AlreadyIgnored.Should().Be(0);
        result.SkippedPosted.Should().Be(1); // comment3
        result.NotFound.Should().Be(1); // non-existent
        result.IgnoredIds.Should().BeEquivalentTo("comment1", "comment2");
        result.SkippedPostedIds.Should().BeEquivalentTo("comment3");
        result.NotFoundIds.Should().BeEquivalentTo("non-existent");

        // Verify replies were ignored in database
        using var verificationScope = fixture.ApiServices.CreateScope();
        var verificationDatabaseContext = verificationScope.ServiceProvider.GetRequiredService<YouTubesterDb>();
        var ignoredReplies = await verificationDatabaseContext.Replies
            .Where(r => r.CommentId == "comment1" || r.CommentId == "comment2")
            .ToListAsync();

        ignoredReplies.Should().HaveCount(2);
        ignoredReplies.Should().OnlyContain(r => r.Status == ReplyStatus.Ignored);
    }

    [Fact]
    public async Task BatchIgnore_EmptyCommentIds_ReturnsBadRequest()
    {
        // Arrange
        await fixture.ResetDbAsync();

        var commentIds = Array.Empty<string>();
        var json = JsonSerializer.Serialize(commentIds, _serializerOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        var response = await fixture.HttpClient.PostAsync("/api/replies/batch-ignore", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var responseContent = await response.Content.ReadAsStringAsync();
        responseContent.Should().Contain("CommentIds cannot be empty");
    }
}