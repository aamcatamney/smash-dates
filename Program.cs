using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using smash_dates.Data;
using smash_dates.Endpoints.Auth;
using smash_dates.Endpoints.BlockedDates;
using smash_dates.Endpoints.ClubAdmins;
using smash_dates.Endpoints.Clubs;
using smash_dates.Endpoints.Memberships;
using smash_dates.Endpoints.Notifications;
using smash_dates.Endpoints.Divisions;
using smash_dates.Endpoints.SeasonEntries;
using smash_dates.Endpoints.Seasons;
using smash_dates.Endpoints.Teams;
using smash_dates.Endpoints.Venues;
using smash_dates.Endpoints.LeagueAdmins;
using smash_dates.Endpoints.Leagues;
using smash_dates.Endpoints.Matches;
using smash_dates.Endpoints.Users;
using smash_dates.Migrations;
using smash_dates.Repositories;
using smash_dates.Services.Auth;
using smash_dates.Services.DataProtection;

var builder = WebApplication.CreateBuilder(args);

Dapper.SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());

builder.Services.AddSingleton<IDbConnectionFactory, NpgsqlConnectionFactory>();
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILeagueRepository, LeagueRepository>();
builder.Services.AddScoped<IDivisionRepository, DivisionRepository>();
builder.Services.AddScoped<ILeagueAdminRepository, LeagueAdminRepository>();
builder.Services.AddScoped<IClubRepository, ClubRepository>();
builder.Services.AddScoped<IClubAdminRepository, ClubAdminRepository>();
builder.Services.AddScoped<IClubLeagueMembershipRepository, ClubLeagueMembershipRepository>();
builder.Services.AddScoped<ITeamRepository, TeamRepository>();
builder.Services.AddScoped<IVenueRepository, VenueRepository>();
builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<ISeasonEntryRepository, SeasonEntryRepository>();
builder.Services.AddScoped<IBlockedDateRepository, BlockedDateRepository>();
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddSingleton<smash_dates.Services.Scheduling.IScheduler, smash_dates.Services.Scheduling.Scheduler>();
builder.Services.AddScoped<smash_dates.Services.Scheduling.IScheduleGenerator, smash_dates.Services.Scheduling.ScheduleGenerator>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<smash_dates.Services.Notifications.INotificationService, smash_dates.Services.Notifications.NotificationService>();

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres missing");

DbMigrator.Apply(connectionString);

builder.Services
    .AddDataProtection()
    .SetApplicationName("smash-dates");

builder.Services.AddSingleton<PostgresXmlRepository>();
builder.Services.AddSingleton<IConfigureOptions<KeyManagementOptions>>(sp =>
    new ConfigureOptions<KeyManagementOptions>(o =>
        o.XmlRepository = sp.GetRequiredService<PostgresXmlRepository>()));

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromDays(14);
        options.SlidingExpiration = true;

        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api"))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };

        options.Events.OnValidatePrincipal = async ctx =>
        {
            var idClaim = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(idClaim, out var id))
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var users = ctx.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
            var user = await users.GetByIdAsync(id, ctx.HttpContext.RequestAborted);
            if (user is null || !user.IsActive)
            {
                ctx.RejectPrincipal();
                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthorizationPolicies.SystemAdmin, policy =>
        policy.RequireAuthenticatedUser()
              .RequireClaim(AuthorizationPolicies.SystemAdminClaim, "true"));
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = ".AspNetCore.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

var authPermitLimit = builder.Configuration.GetValue<int?>("RateLimit:Auth:PermitLimit") ?? 10;
var authWindowSeconds = builder.Configuration.GetValue<int?>("RateLimit:Auth:WindowSeconds") ?? 60;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(AuthEndpoints.RateLimitPolicy, http =>
        RateLimitPartition.GetFixedWindowLimiter(
            http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
});

// Behind a TLS-terminating reverse proxy/ingress, honour X-Forwarded-Proto/For so the
// app sees the original HTTPS scheme (Secure cookies, redirects). KnownProxies/Networks
// are cleared because the container is only reachable via its ingress, whose IP is dynamic.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

var clientAppPath = Path.Combine(builder.Environment.ContentRootPath, "ClientApp", "dist", "smash-dates", "browser");
if (Directory.Exists(clientAppPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientAppPath),
        RequestPath = "",
        OnPrepareResponse = ctx =>
        {
            if (ctx.File.Name == "ngsw-worker.js" || ctx.File.Name == "ngsw.json")
            {
                ctx.Context.Response.Headers.CacheControl = "no-cache";
            }
        }
    });
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapLeagueEndpoints();
app.MapDivisionEndpoints();
app.MapLeagueAdminEndpoints();
app.MapUserEndpoints();
app.MapClubEndpoints();
app.MapClubAdminEndpoints();
app.MapMembershipEndpoints();
app.MapTeamEndpoints();
app.MapVenueEndpoints();
app.MapSeasonEndpoints();
app.MapSeasonEntryEndpoints();
app.MapBlockedDateEndpoints();
app.MapMatchEndpoints();
app.MapMatchActionEndpoints();
app.MapNotificationEndpoints();

if (Directory.Exists(clientAppPath))
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(clientAppPath)
    });
}

app.Run();

public partial class Program;
