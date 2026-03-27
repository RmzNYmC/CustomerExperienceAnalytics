using CEA.Core.Entities;
using CEA.Core.Enum;
using CEA.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CEA.Web.Data
{
    public static class SeedData
    {
        // MEVCUT: Roller ve Admin kullanıcı için (varsa)
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Rolleri oluştur
            string[] roles = { "Admin", "SurveyManager", "ComplaintManager" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Admin kullanıcı oluştur (eğer yoksa)
            var adminEmail = "admin@turkon.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FirstName = "System",
                    LastName = "Admin"
                };

                var result = await userManager.CreateAsync(adminUser, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, "Admin");
                }
            }
        }

        // YENİ: Settings için
        public static async Task InitializeSettings(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            if (!await context.Settings.AnyAsync())  // AnyAsync artık çalışacak
            {
                context.Settings.AddRange(
                    new Setting
                    {
                        Key = "SMTP_Host",
                        Value = "smtp.gmail.com",
                        Category = "SMTP",
                        Description = "SMTP Sunucu Adresi",
                        CreatedAt = DateTime.Now
                    },
                    new Setting
                    {
                        Key = "SMTP_Port",
                        Value = "587",
                        Category = "SMTP",
                        Description = "SMTP Port",
                        CreatedAt = DateTime.Now
                    },
                    new Setting
                    {
                        Key = "SMTP_From",
                        Value = "noreply@turkon.com",
                        Category = "SMTP",
                        Description = "Gönderici E-posta",
                        CreatedAt = DateTime.Now
                    },
                    new Setting
                    {
                        Key = "SMTP_FromName",
                        Value = "Turkon Lojistik",
                        Category = "SMTP",
                        Description = "Gönderici Adı",
                        CreatedAt = DateTime.Now
                    },
                    new Setting
                    {
                        Key = "SMTP_Username",
                        Value = "",
                        Category = "SMTP",
                        Description = "SMTP Kullanıcı adı",
                        CreatedAt = DateTime.Now
                    },
                    new Setting
                    {
                        Key = "SMTP_Password",
                        Value = "",
                        Category = "SMTP",
                        Description = "SMTP Şifre",
                        IsEncrypted = true,
                        CreatedAt = DateTime.Now
                    }
                );

                await context.SaveChangesAsync();
            }
        }
    }
}