var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<NewsParserWorker>();

var host = builder.Build();
host.Run();