using Hangfire;
using Microsoft.EntityFrameworkCore;
using YouTubester.Application;
using YouTubester.Integration;
using YouTubester.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add your DI here
builder.Services.AddAiClient(builder.Configuration);
builder.Services.AddYoutubeServices(builder.Configuration);
builder.Services.AddScoped<IReplyRepository, ReplyRepository>();
builder.Services.AddSingleton<IAiClient, AiClient>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddSingleton<IYouTubeClientFactory, YouTubeClientFactory>();

var rootPath = builder.Environment.ContentRootPath;
builder.Services.AddDatabase(rootPath);
builder.Services.AddHangFireStorage(builder.Configuration, rootPath);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard();
    var cfg = app.Services.GetRequiredService<IConfiguration>();
    if (cfg.GetValue<bool>("Seed:Enable"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YouTubesterDb>();

        db.Database.Migrate();
        await DbSeeder.SeedAsync(db);
    }
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();