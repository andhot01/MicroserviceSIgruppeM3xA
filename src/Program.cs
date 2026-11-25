using Shared.Contracts.Events;
using Shared.Contracts.WorkspaceDeletion;
using src.Application.Handlers;
using src.Messaging;
using src.Application.Interfaces;
using src.Application.Services;
using src.Infrastructure.Repositories;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using src.Infrastructure;
using src.Application.Sagas;

var builder = WebApplication.CreateBuilder(args);

var signingKey = await VaultSecretReader.ReadAsync(
    builder.Configuration, "notification", "SigningKey");

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // behold "role" og "sub" som de er
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwt["Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddMessaging(builder.Configuration);

builder.Services.AddOpenApi();
builder.Services.AddControllers();


builder.Services.AddSingleton<INotificationRepository, NotificationRepository>();
builder.Services.AddSingleton<ISagaStateRepository, SagaStateRepository>();
builder.Services.AddSingleton<WorkspaceDeletionSagaOrchestrator>();
builder.Services.AddHostedService<WorkspaceDeletionSagaSubscription>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IMessageHandler<MessagePostedEvent>, MessagePostedHandler>();
builder.Services.AddScoped<IMessageHandler<SendWorkspaceDeletedNotification>,WorkspaceDeletedNotificationHandler>();
builder.Services.AddHostedService<MessagePostedSubscription>();
builder.Services.AddHostedService<WorkspaceDeletedNotificationSubscription>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();