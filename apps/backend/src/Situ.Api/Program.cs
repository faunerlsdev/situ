using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Microsoft.Extensions.FileProviders;
using Situ.Api.Administration;
using Situ.Api.Auth;
using Situ.Api.Configuration;
using Situ.Infrastructure.Reports;
using Situ.Api.Endpoints;
using Situ.Api.Guardianship;
using Situ.Api.Notifications;
using Situ.Api.Realtime;
using Situ.Api.Reports;
using Situ.Application.Features.MapRealtime;
using Situ.Api.Retention;
using Situ.Api.Health;
using Situ.Application;
using Situ.Application.Common.Abstractions;
using Situ.Infrastructure;
using Situ.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSituConfiguration(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.EnvironmentName);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi(options =>
{
    if (!builder.Environment.IsDevelopment())
    {
        options.AddDocumentTransformer((document, _, _) =>
        {
            foreach (var path in document.Paths.Keys.Where(key => key.Contains("/admin/", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                document.Paths.Remove(path);
            }

            return Task.CompletedTask;
        });
    }
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "sub",
            RoleClaimType = "role",
        };
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = async context =>
            {
                var path = context.HttpContext.Request.Path;
                var ticket = context.Request.Query["ticket"].ToString();
                if (MapHubAuth.ReadTicketFromQuery(path, ticket))
                {
                    var exchanged = await RealtimeTicketExchange.TryExchangeAsync(context.HttpContext.RequestServices, ticket, context.HttpContext.RequestAborted);
                    if (exchanged is not null)
                    {
                        context.Token = exchanged;
                    }

                    return;
                }

                var accessToken = context.Request.Query["access_token"].ToString();
                if (MapHubAuth.ReadAccessTokenFromQuery(path, accessToken))
                {
                    context.Token = accessToken;
                }
            },
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicies.Add);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
var allowDevelopmentOrigins = builder.Environment.IsDevelopment();
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicies.Clients, policy =>
        policy.SetIsOriginAllowed(origin => CorsPolicies.IsAllowedOrigin(origin, corsOrigins, allowDevelopmentOrigins))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddDataProtection();
builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITokenService>(serviceProvider =>
    new JwtTokenService(serviceProvider.GetRequiredService<IOptions<JwtOptions>>().Value));
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new { code = "auth.rate_limited", detail = "Demasiados intentos desde esta red. Espera un momento." },
            cancellationToken);
    };
    options.AddPolicy(AuthRateLimit.Policy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("catalog-read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("interaction-write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("notification-device", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("profile-write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("user-search", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("username-lookup", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("admin-login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            }));
    options.AddPolicy("admin-mfa", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown") + "|" + (httpContext.User.FindFirst("sub")?.Value ?? "anon"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0,
            }));
    options.AddPolicy("admin", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("admin-user-lookup", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("public-map", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Situ.Application.Features.Reports.GetPublicReportMap.PublicReadLimits.MapPermitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("share-public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Situ.Application.Features.Reports.GetPublicReportMap.PublicReadLimits.SharePermitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("takedown-public", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
    options.AddPolicy("reports-write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            (httpContext.User.FindFirst("sub")?.Value ?? "anon") + "|" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<MapSubscriptionBook>();
builder.Services.AddSingleton<IMapZonePublisher, SignalRMapZonePublisher>();
builder.Services.AddHostedService<MapZoneDispatchService>();
builder.Services.AddHostedService<GuardianConsentExpiryService>();
builder.Services.AddHostedService<ExpireReportsService>();
builder.Services.AddHostedService<PurgePendingReportMediaService>();
builder.Services.AddHostedService<RetainReportsService>();
builder.Services.AddHostedService<CompleteAccountDeletionsService>();
builder.Services.AddHostedService<NotificationDispatchService>();
builder.Services.AddHostedService<PurgeAlertLocationsService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

var app = builder.Build();

if (app.Environment.IsProduction() && !ProductionSecretRules.TryValidate(app.Configuration, out var productionError))
{
    throw new InvalidOperationException(productionError);
}

if (ConsoleCommands.IsRequested(args))
{
    return await ConsoleCommands.ExecuteAsync(app, args);
}

ForwardedClient.Use(app);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsPolicies.Clients);
app.UseStaticFiles();
var uploads = Path.Combine(app.Services.GetRequiredService<FileStorageOptions>().Root, "public");
Directory.CreateDirectory(uploads);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploads),
    RequestPath = "/uploads",
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = static _ => false,
    ResponseWriter = HealthResponses.WriteAsync,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponses.WriteAsync,
}).AllowAnonymous();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = static check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponses.WriteAsync,
}).AllowAnonymous();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapSituEndpoints();
app.MapHub<MapHub>(MapHubAuth.HubPath);

app.Run();
return 0;
