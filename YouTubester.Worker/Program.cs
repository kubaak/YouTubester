using Hangfire;
using YouTubester.Application;
using YouTubester.Application.Jobs;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Replies;
using YouTubester.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

var rootPath = builder.Environment.ContentRootPath;
builder.Services.AddDatabase(rootPath);

builder.Services.AddYoutubeServices(builder.Configuration);
builder.Services.AddAiClient(builder.Configuration);
builder.Services.AddScoped<IReplyRepository, ReplyRepository>();
builder.Services.AddScoped<IVideoTemplatingService, VideoTemplatingService>();
// builder.Services.AddHostedService<CommentScanWorker>();
builder.Services.AddScoped<PostApprovedRepliesJob>();
builder.Services.AddScoped<CopyVideoTemplateJob>();
builder.Services.AddHangFireStorage(builder.Configuration, rootPath);
builder.Services.AddHangfireServer(options => options.Queues = ["replies", "templating", "default"]);

var host = builder.Build();
host.Run();