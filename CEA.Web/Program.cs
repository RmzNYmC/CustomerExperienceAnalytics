using CEA.Business.Services;
using CEA.Core.Entities;
using CEA.Web.Data;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ApplicationDbContext = CEA.Data.ApplicationDbContext;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// DÜZELTİLDİ: Authorization Policies eklendi
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CanCreateSurvey", policy =>
        policy.RequireRole("Admin", "SurveyManager"));

    options.AddPolicy("CanViewReports", policy =>
        policy.RequireRole("Admin", "SurveyManager", "ComplaintManager"));

    options.AddPolicy("CanManageUsers", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("CanHandleComplaints", policy =>
        policy.RequireRole("Admin", "ComplaintManager"));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
});

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IComplaintAutomationService, ComplaintAutomationService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();

builder.Services.AddRazorPages();
builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Seed verileri
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        
        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        await context.Database.MigrateAsync();

        // Mevcut Initialize (Roller/Admin için)
        await SeedData.Initialize(services);
        // Yeni InitializeSettings (SMTP ayarları için)
        await SeedData.InitializeSettings(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Migration/Seed hatası");
    }
}

app.Run();