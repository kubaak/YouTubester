using Hangfire;
using Microsoft.EntityFrameworkCore;
using YouTubester.Api.Extensions;
using YouTubester.Application;
using YouTubester.Application.Channels;
using YouTubester.Integration;
using YouTubester.Persistence;
using YouTubester.Persistence.Channels;
using YouTubester.Persistence.Playlists;
using YouTubester.Persistence.Replies;
using YouTubester.Persistence.Videos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add JWT Bearer authentication
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddAiClient(builder.Configuration);
builder.Services.AddYoutubeServices(builder.Configuration);
builder.Services.AddScoped<IReplyRepository, ReplyRepository>();
builder.Services.AddScoped<IChannelRepository, ChannelRepository>();
builder.Services.AddScoped<IVideoRepository, VideoRepository>();
builder.Services.AddScoped<IPlaylistRepository, PlaylistRepository>();
builder.Services.AddSingleton<IAiClient, AiClient>();
builder.Services.AddScoped<IReplyService, ReplyService>();
builder.Services.AddScoped<IVideoService, VideoService>();
builder.Services.AddScoped<IChannelSyncService, ChannelSyncService>();
builder.Services.AddSingleton<IYouTubeClientFactory, YouTubeClientFactory>();

builder.Services.AddVideoListingOptions(builder.Configuration);
builder.Services.AddReplyListingOptions(builder.Configuration);

builder.Services.AddGoogleAuthentication(builder.Configuration, builder.Environment);

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

// Make Program class accessible for integration testing
namespace YouTubester.Api
{
    public partial class Program { }
}
