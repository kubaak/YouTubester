using Microsoft.Extensions.Options;
using YouTubester.Application;
using YouTubester.Integration;
using YouTubester.Integration.Configuration;
using YouTubester.Persistence;
using YouTubester.Worker;

var builder = Host.CreateApplicationBuilder(args);

// bind options
builder.Services.Configure<YouTubeAuthOptions>(builder.Configuration.GetSection("YouTubeAuth"));
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("AI"));

var contentRoot = builder.Environment.ContentRootPath;
builder.Services.AddDatabase(contentRoot);

builder.Services.AddYoutubeServices();
builder.Services.AddAiClient();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddHostedService<CommentScanWorker>();

var host = builder.Build();
host.Run();