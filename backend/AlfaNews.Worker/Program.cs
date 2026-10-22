using AlfaNews.Worker;
using AlfaNews.Core.Data;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAlfaNewsData(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Задайте ConnectionStrings__DefaultConnection."));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();