using CEA.Core.Entities;
using Microsoft.AspNetCore.Identity;

namespace CEA.Web.Data
{
    public static class SeedData
    {
        public static async Task Initialize(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Tüm rolleri oluştur
            string[] roles = { "Admin", "SurveyManager", "ComplaintManager", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // Admin kullanıcısı
            var adminEmail = "admin@cea.com";
            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "System",
                    LastName = "Administrator",
                    EmailConfirmed = true,
                    IsActive = true,
                    CreatedDate = DateTime.Now
                };

                var result = await userManager.CreateAsync(admin, "Admin123!");
                if (result.Succeeded)
                {
                    // Tüm yetkili rolleri ata
                    await userManager.AddToRoleAsync(admin, "Admin");
                    await userManager.AddToRoleAsync(admin, "SurveyManager");
                    await userManager.AddToRoleAsync(admin, "ComplaintManager");
                }
            }
            else
            {
                // Mevcut admin'e rolleri kontrol et ve ekle
                var userRoles = await userManager.GetRolesAsync(admin);
                if (!userRoles.Contains("Admin"))
                    await userManager.AddToRoleAsync(admin, "Admin");
                if (!userRoles.Contains("SurveyManager"))
                    await userManager.AddToRoleAsync(admin, "SurveyManager");
                if (!userRoles.Contains("ComplaintManager"))
                    await userManager.AddToRoleAsync(admin, "ComplaintManager");
            }
        }
    }
}