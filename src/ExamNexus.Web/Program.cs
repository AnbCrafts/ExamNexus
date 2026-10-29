using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using ExamNexus.Domain.Entities;
using ExamNexus.Infrastructure;
using ExamNexus.Infrastructure.Data;
using ExamNexus.Web.Hubs;
using ExamNexus.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Infrastructure Services (EF Core, Multi-Tenant DbContext, Identity)
builder.Services.AddInfrastructure(builder.Configuration);

// 2. Add Real-Time SignalR Engine
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
});

// 3. Add Distributed Memory Cache and Session Handling
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2); // Retain live exam state
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".ExamNexus.Session";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// 4. Configure Application Cookie Authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".ExamNexus.Auth";
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// 5. Configure JWT Bearer for REST API Endpoints
var jwtSecret = builder.Configuration["JwtSettings:Secret"] ?? "ExamNexus_SuperSecretSaaSKey_2026_Minimum32CharsLong!@#$";
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "ExamNexus";
var jwtAudience = builder.Configuration["JwtSettings:Audience"] ?? "ExamNexus-ProctorApp";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services.AddAuthentication()
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = signingKey,
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// 6. RBAC & Policy-Based Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SystemAdminOnly", policy => policy.RequireRole("SystemAdmin"));
    options.AddPolicy("TenantAdminOnly", policy => policy.RequireRole("SystemAdmin", "TenantAdmin"));
    options.AddPolicy("InstructorOrAdmin", policy => policy.RequireRole("SystemAdmin", "TenantAdmin", "Instructor"));
    options.AddPolicy("ProctorOrAdmin", policy => policy.RequireRole("SystemAdmin", "TenantAdmin", "Instructor", "Proctor"));
    options.AddPolicy("CandidateOnly", policy => policy.RequireRole("Candidate"));
});

// 7. Anti-Forgery Protection
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = ".ExamNexus.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// 8. Controllers and Razor Views with Auto Anti-Forgery Validation
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

// 9. Auto-Migrate & Seed Database on Startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        await DbInitializer.SeedDatabaseAsync(context, userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error while seeding ExamNexus database.");
    }
}

// 10. HTTP Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSecurityHeaders();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseTenantResolution();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// Route Mappings
app.MapHub<ProctorHub>("/hubs/proctor");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
