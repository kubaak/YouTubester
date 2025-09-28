using Hangfire;
using YouTubester.Application.Jobs;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

var rootPath = builder.Environment.ContentRootPath;
builder.Services.AddDatabase(rootPath);

builder.Services.AddYoutubeServices(builder.Configuration);
builder.Services.AddAiClient(builder.Configuration);
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddHostedService<CommentScanWorker>();
builder.Services.AddScoped<PostApprovedCommentsJob>();
builder.Services.AddHangFireStorage(builder.Configuration, rootPath);
builder.Services.AddHangfireServer();

var host = builder.Build();
host.Run();