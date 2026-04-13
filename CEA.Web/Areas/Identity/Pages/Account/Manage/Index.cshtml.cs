using CEA.Core.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // Görüntüleme için (Read-only)
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedDate { get; set; }

        [TempData]
        public string StatusMessage { get; set; } = string.Empty;

        [BindProperty]
        public ProfileInputModel ProfileInput { get; set; } = new();

        [BindProperty]
        public PasswordInputModel PasswordInput { get; set; } = new();

        // Profil güncelleme için model
        public class ProfileInputModel
        {
            [Required(ErrorMessage = "Ad alanı zorunludur.")]
            [Display(Name = "Ad")]
            public string FirstName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Soyad alanı zorunludur.")]
            [Display(Name = "Soyad")]
            public string LastName { get; set; } = string.Empty;

            [Display(Name = "Departman")]
            public string? Department { get; set; }

            [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz.")]
            [Display(Name = "Telefon Numarası")]
            public string? PhoneNumber { get; set; }
        }

        // Şifre değiştirme için model
        public class PasswordInputModel
        {
            [Required(ErrorMessage = "Mevcut şifre zorunludur.")]
            [DataType(DataType.Password)]
            [Display(Name = "Mevcut Şifre")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Yeni şifre zorunludur.")]
            [StringLength(100, ErrorMessage = "{0} en az {2} karakter uzunluğunda olmalıdır.", MinimumLength = 8)]
            [DataType(DataType.Password)]
            [Display(Name = "Yeni Şifre")]
            public string NewPassword { get; set; } = string.Empty;

            [DataType(DataType.Password)]
            [Display(Name = "Yeni Şifre Tekrar")]
            [Compare("NewPassword", ErrorMessage = "Yeni şifreler eşleşmiyor.")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;
            Email = user.Email ?? string.Empty;
            IsActive = user.IsActive;
            CreatedDate = user.CreatedDate;

            ProfileInput = new ProfileInputModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Department = user.Department,
                PhoneNumber = phoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kullanıcı yüklenemedi. ID: '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }

        // Profil bilgilerini güncelle
        public async Task<IActionResult> OnPostUpdateProfileAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kullanıcı yüklenemedi. ID: '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                await LoadAsync(user);
                return Page();
            }

            // Telefon numarası güncelleme
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (ProfileInput.PhoneNumber != phoneNumber)
            {
                var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, ProfileInput.PhoneNumber);
                if (!setPhoneResult.Succeeded)
                {
                    StatusMessage = "Hata: Telefon numarası kaydedilirken bir sorun oluştu.";
                    return RedirectToPage();
                }
            }

            // Profil bilgileri güncelleme
            if (user.FirstName != ProfileInput.FirstName ||
                user.LastName != ProfileInput.LastName ||
                user.Department != ProfileInput.Department)
            {
                user.FirstName = ProfileInput.FirstName;
                user.LastName = ProfileInput.LastName;
                user.Department = ProfileInput.Department;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    foreach (var error in updateResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    await LoadAsync(user);
                    return Page();
                }
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Profil bilgileriniz başarıyla güncellendi.";
            return RedirectToPage();
        }

        // Şifre değiştirme
        public async Task<IActionResult> OnPostChangePasswordAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Kullanıcı yüklenemedi. ID: '{_userManager.GetUserId(User)}'.");
            }

            // ModelState'i sadece PasswordInput için kontrol et
            if (!TryValidateModel(PasswordInput, nameof(PasswordInput)))
            {
                await LoadAsync(user);
                return Page();
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user,
                PasswordInput.CurrentPassword, PasswordInput.NewPassword);

            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await LoadAsync(user);
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Şifreniz başarıyla değiştirildi.";

            // PasswordInput'u temizle
            PasswordInput = new PasswordInputModel();

            return RedirectToPage();
        }
    }
}