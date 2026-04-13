using CEA.Business.Services;
using CEA.Business.Validators;
using CEA.Core.Entities;
using CEA.Core.Enum;          // ComplaintStatus için (EmailService'te kullanılıyor)
using CEA.Web.Authorization;  // HangfireAuthorizationFilter için
using CEA.Web.BackgroundServices;
using CEA.Web.Data;
using CEA.Web.Hubs;
using CEA.Web.Middleware;
using CEA.Web.Services;
using DocumentFormat.OpenXml.Spreadsheet;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.SqlServer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ApplicationDbContext = CEA.Data.ApplicationDbContext;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/html", "text/css", "application/javascript" });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()

    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"),
        new SqlServerStorageOptions
        {
            CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
            SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
            QueuePollInterval = TimeSpan.Zero,
            UseRecommendedIsolationLevel = true,
            DisableGlobalLocks = true
        }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2; // Paralel işçi sayısı
    options.Queues = new[] { "critical", "default", "low" };
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
});


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
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/AccessDenied";
    options.LogoutPath = "/Identity/Account/Logout";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(2);
    // EKLEYİN: Cookie ismini değiştirin (eski cookieler geçersiz olsun)
    options.Cookie.Name = "CEA.Auth.v2";  // Eski ".AspNetCore.Identity.Application" yerine yeni isim
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;  // HTTPS için
});

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPdfReportService, PdfReportService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<IComplaintAutomationService, ComplaintAutomationService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
// SLA ve Atama Servisleri
builder.Services.AddScoped<ISlaCalculatorService, SlaCalculatorService>();
builder.Services.AddScoped<ISmartAssignmentService, SmartAssignmentService>();

// MemoryCache servisi
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ICacheService, CacheService>();

// Background Service (Her 15 dakikada SLA kontrolü)
builder.Services.AddHostedService<SlaMonitorBackgroundService>();

builder.Services.AddRazorPages();
builder.Services.AddControllers();

builder.Services.AddHttpContextAccessor(); // AuditService için gerekli
builder.Services.AddScoped<IAuditService, AuditService>();

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<ComplaintValidator>();

builder.Services.AddScoped<IComplaintAutomationService, ComplaintAutomationService>();

// SignalR
builder.Services.AddSignalR();

builder.Services.AddScoped<IRealTimeNotificationService, SignalRNotificationService>();

//// Real-time Notification Service
//builder.Services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(policyName: "surveyLimit", opt =>
    {
        opt.PermitLimit = 5; // 5 istek
        opt.Window = TimeSpan.FromMinutes(1); // 1 dakika içinde
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter(policyName: "loginLimit", opt =>
    {
        opt.PermitLimit = 3;
        opt.Window = TimeSpan.FromMinutes(5);
    });
});

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

// MIDDLEWARE PIPELINE (UseRouting'ten ÖNCE)
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
// SignalR Hub endpoint
app.MapHub<NotificationHub>("/notificationHub");
app.UseRateLimiter();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "CEA Background Jobs"
});

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
using (var scope = app.Services.CreateScope())
{
    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    // SLA Monitoring - 5 dakikada bir (daha sık kontrol)
    recurringJobManager.AddOrUpdate<ISlaCalculatorService>(
        "sla-monitor",
        service => service.CheckAndMarkBreachedSlasAsync(),
        "*/5 * * * *", // Her 5 dakika
        new RecurringJobOptions { QueueName = "critical" });

    // Günlük rapor gönderimi - Her gece 09:00'da
    recurringJobManager.AddOrUpdate<IEmailService>(
        "daily-report",
        service => service.SendDailySummaryAsync(), // EmailService'e eklenecek metod
        "0 9 * * *", // Her gün 09:00
        new RecurringJobOptions { QueueName = "low" });
}
app.Run();