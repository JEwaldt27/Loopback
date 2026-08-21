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
builder.Services.AddSingleton<FeatureRequestStore>();

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

// The WASM payload under /_framework. Nearly everything here is FINGERPRINTED
// (Client.z4jdve7srd.wasm) and therefore safe to cache forever — a new build produces a
// new name. The exceptions are the entry scripts, which keep fixed names and carry the map
// of those fingerprinted files: cache blazor.webassembly.js or dotnet.js and the browser
// keeps loading the ENTIRE previous build, however fresh index.html is. That is what left
// deploys needing a Ctrl+F5 — the toolbar would show the old version number because the
// old Client.wasm was still being fetched.
// UseBlazorFrameworkFiles takes no StaticFileOptions, so the header is stamped on the way
// past instead. OnStarting runs after the static-file middleware has written its own
// headers, so this wins.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/_framework"))
    {
        var name = Path.GetFileName(context.Request.Path.Value) ?? "";
        var isEntryPoint = name is "blazor.webassembly.js" or "dotnet.js"
                           || name.EndsWith(".json") || name.EndsWith(".map");
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = isEntryPoint
                ? "no-cache"                          // revalidate every load; a 304 is cheap
                : "max-age=31536000, immutable";      // fingerprinted: the name changes instead
            return Task.CompletedTask;
        });
    }
    await next();
});

app.UseBlazorFrameworkFiles();
// Serve entry-point assets (html/css/js) as no-cache so browsers revalidate them on every
// load — with ETags that's a cheap 304 unless the file actually changed. Without this,
// deploys left clients on stale index.html/app.css until a hard refresh. Framework files
// under _framework keep their own hash-based caching (handled by Blazor's boot manifest).
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var name = ctx.File.Name;
        if (name.EndsWith(".html") || name.EndsWith(".css") || name.EndsWith(".js"))
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});
app.MapControllers();
// The root page ("/") is served by this fallback, not UseStaticFiles — same no-cache rule.
app.MapFallbackToFile("index.html", new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache"
});

app.Run();
