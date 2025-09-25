using Google.Apis.YouTube.v3;
using Microsoft.EntityFrameworkCore;
using YouTubester.Application;
using YouTubester.Integration;
using YouTubester.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add your DI here
builder.Services.AddAiClient();
builder.Services.AddYoutubeServices();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();
builder.Services.AddSingleton<IAiClient, AiClient>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddSingleton<IYouTubeClientFactory, YouTubeClientFactory>();

var contentRoot = builder.Environment.ContentRootPath; // Api project folder
builder.Services.AddDatabase(contentRoot);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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