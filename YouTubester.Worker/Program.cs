using Hangfire;
using Microsoft.Extensions.Options;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Channels;
using YouTubester.Persistence.Replies;
using YouTubester.Persistence.Videos;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

var rootPath = builder.Environment.ContentRootPath;
builder.Services.AddDatabase(rootPath);

builder.Services.AddYoutubeServices(builder.Configuration);
builder.Services.AddAiClient(builder.Configuration);
builder.Services.AddScoped<IChannelRepository, ChannelRepository>();
builder.Services.AddScoped<IVideoRepository, VideoRepository>();
builder.Services.AddScoped<IReplyRepository, ReplyRepository>();
builder.Services.AddScoped<IVideoTemplatingService, VideoTemplatingService>();
builder.Services.AddScoped<PostApprovedRepliesJob>();
builder.Services.AddScoped<CopyVideoTemplateJob>();
builder.Services.AddScoped<CommentScanJob>();
builder.Services.AddHangFireStorage(builder.Configuration, rootPath);
builder.Services.AddHangfireServer(options => options.Queues = ["scanning", "replies", "templating", "default"]);

var host = builder.Build();

// Set up recurring jobs
using (var scope = host.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    var workerOptions = scope.ServiceProvider.GetRequiredService<IOptions<WorkerOptions>>();

    // Schedule comment scanning job to run at the configured interval
    var cronExpression = $"*/{workerOptions.Value.IntervalSeconds} * * * * *";
    recurringJobManager.AddOrUpdate<CommentScanJob>(
        "comment-scan",
        job => job.Run(JobCancellationToken.Null),
        cronExpression);
}

host.Run();
