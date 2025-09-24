﻿namespace YouTubester.Integration.Dtos;

public sealed record CommentThreadDto(
    string ParentCommentId,
    string VideoId,
    string AuthorChannelId,
    string Text
);