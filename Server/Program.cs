using Microsoft.AspNetCore.Authentication.Cookies;
using Server;
using Server.Services;

var builder = WebApplication.CreateBuilder(args);

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
