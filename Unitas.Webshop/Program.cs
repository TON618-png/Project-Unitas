using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Unitas.Webshop.Data;

var initializeOnly = args.Contains("--initialize-db");
var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--initialize-db").ToArray());
builder.Logging.ClearProviders().AddSimpleConsole();
var dataDirectory = builder.Configuration["Storage:Directory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data");
var connectionString = builder.Configuration.GetConnectionString("PostgreSql");
var usesPostgres = !string.IsNullOrWhiteSpace(connectionString);
if (!usesPostgres && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Stel ConnectionStrings:PostgreSql in voor de projectserver.");

var keyDirectory = new DirectoryInfo(builder.Configuration["Identity:KeyDirectory"]
    ?? Path.Combine(dataDirectory, "keys"));
keyDirectory.Create();
var protection = builder.Services.AddDataProtection()
    .SetApplicationName("Unitas.Webshop")
    .PersistKeysToFileSystem(keyDirectory);
var keyCertificate = builder.Configuration["Identity:KeyCertificate"];
if (!string.IsNullOrEmpty(keyCertificate))
{
    var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadPkcs12FromFile(
        keyCertificate, builder.Configuration["Identity:KeyCertificatePassword"]);
    protection.ProtectKeysWithCertificate(certificate);
}
if (bool.TryParse(builder.Configuration["Identity:ProtectKeysWithDpapi"], out var useDpapi) &&
    useDpapi && OperatingSystem.IsWindows())
    protection.ProtectKeysWithDpapi();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (usesPostgres)
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite($"Data Source={Path.Combine(dataDirectory, "unitas.db")}");
});
builder.Services.AddIdentity<ShopUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Lockout.MaxFailedAccessAttempts = 8;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<SingleUseAuthenticator>(TokenOptions.DefaultAuthenticatorProvider);
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/EnableAuthenticator";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.Events.OnSigningIn = context =>
    {
        if (context.Principal?.HasClaim("amr", "mfa") != true)
            context.Properties.ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5);
        return Task.CompletedTask;
    };
});
builder.Services.Configure<CookieAuthenticationOptions>(IdentityConstants.TwoFactorUserIdScheme,
    options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });
builder.Services.Configure<SecurityStampValidatorOptions>(options => options.OnRefreshingPrincipal = context =>
{
    if (context.CurrentPrincipal?.HasClaim("amr", "mfa") == true &&
        context.NewPrincipal?.Identity is System.Security.Claims.ClaimsIdentity identity)
        identity.AddClaim(new System.Security.Claims.Claim("amr", "mfa"));
    return Task.CompletedTask;
});
builder.Services.AddAuthorizationBuilder().AddPolicy("Mfa", policy =>
    policy.RequireAuthenticatedUser().RequireClaim("amr", "mfa"));
builder.Services.AddAntiforgery(options =>
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always);
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddMemoryCache();
builder.Services.Configure<InvoiceSmtpOptions>(builder.Configuration.GetSection("Smtp"));
builder.Services.Configure<GmailInvoiceOptions>(builder.Configuration.GetSection("Gmail"));
builder.Services.AddHttpClient("Gmail").ConfigurePrimaryHttpMessageHandler(
    () => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<GmailInvoiceSender>();
builder.Services.AddSingleton<InvoiceEmailSender>();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Unitas.Checkout";
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CheckoutCart>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("IdentityPost", context =>
        HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 45, Window = TimeSpan.FromMinutes(10) })
            : RateLimitPartition.GetNoLimiter("read"));
});

var app = builder.Build();
if (!usesPostgres || initializeOnly)
{
    await using var scope = app.Services.CreateAsyncScope();
    var database = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await database.Database.EnsureCreatedAsync();
    await database.Database.ExecuteSqlRawAsync(DatabaseSetup.OrderHistorySchema);
    if (initializeOnly)
    {
        if (usesPostgres)
            await database.Database.ExecuteSqlRawAsync(DatabaseSetup.ReportingViews);
        return;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'none'; style-src 'self'; script-src 'self'; img-src 'self' data:; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseSession();
app.MapRazorPages();
app.Run();
