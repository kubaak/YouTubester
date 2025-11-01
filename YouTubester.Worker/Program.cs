using YouTubester.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWorkerCore(builder.Configuration, builder.Environment.ContentRootPath);
var host = builder.Build();
host.Run();
