using src.Messaging;
using src.Application.Interfaces;
using src.Application.Services;
using src.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMessaging(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddControllers();

builder.Services.AddSingleton<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();