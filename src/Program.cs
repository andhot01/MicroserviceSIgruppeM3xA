using src.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMessaging(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.Run();