using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Cookies;
using Server;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Optional local HTTPS: if a cert is present in <app-dir>/certs, use it as Kestrel's
// default certificate. This is how the fully-local prod server serves https. The dev
// box is fronted by Cloudflare over plain http and has no cert, so this is skipped there
// — the cert must NOT be required, or a certless server crashes on startup. Combined with
// ASPNETCORE_URLS=https://... on prod, this serves https; with http:// it's simply unused.
var certPath = Path.Combine(builder.Environment.ContentRootPath, "certs", "server.crt");
var keyPath = Path.Combine(builder.Environment.ContentRootPath, "certs", "server.key");
if (File.Exists(certPath) && File.Exists(keyPath))
{
    // Re-import via PFX so the private key is usable by Kestrel on all platforms.
    using var pem = X509Certificate2.CreateFromPemFile(certPath, keyPath);
    var cert = X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), null);
    builder.WebHost.ConfigureKestrel(options =>
        options.ConfigureHttpsDefaults(https => https.ServerCertificate = cert));
}

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<UserStore>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Cookie.Name = "LineFlowAuth";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseHsts();
}

app.UseRouting();

app.UseAuthentication();

// Gate: everything except /login and /api/auth/* requires an authenticated cookie.
// This runs before the static file / Blazor framework file middleware below, so an
// unauthenticated visitor can't even download the WASM app shell before signing in.
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    var isAllowed = path.StartsWithSegments("/login") || path.StartsWithSegments("/api/auth");

    if (!isAllowed && !(context.User.Identity?.IsAuthenticated ?? false))
    {
        if (path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        context.Response.Redirect("/login");
        return;
    }

    await next();
});

app.UseAuthorization();

app.MapGet("/login", () => Results.Content(LoginPage.Html, "text/html"));

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
