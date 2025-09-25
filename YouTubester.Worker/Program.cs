using YouTubester.Application;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));

builder.Services.AddDatabase(builder.Environment.ContentRootPath);

builder.Services.AddYoutubeServices(builder.Configuration);
builder.Services.AddAiClient(builder.Configuration);
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddHostedService<CommentScanWorker>();

var host = builder.Build();
host.Run();