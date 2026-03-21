using Backify.Api.Configuration;
using Backify.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var appConfig = builder.Configuration.GetSection("App").Get<AppConfig>() ?? new AppConfig();
builder.Services.AddSingleton(appConfig);

// HttpClient
builder.Services.AddHttpClient<LastFmService>();
builder.Services.AddHttpClient<SpotifyService>();

// Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<LastFmService>();
builder.Services.AddScoped<SpotifyService>();
builder.Services.AddScoped<TracksOrchestrator>();
builder.Services.AddScoped<AlbumsOrchestrator>();

// Session
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddStackExchangeRedisCache(options => options.Configuration = redisConn);
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSession(options =>
{
    options.Cookie.Name = "backify_session";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsProduction()
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.None;
    options.IdleTimeout = TimeSpan.FromHours(24);
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(appConfig.FrontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseCors();
app.UseSession();
app.MapControllers();
app.MapGet("/health", () => new { status = "ok" });
app.MapFallbackToFile("index.html");

app.Run();
