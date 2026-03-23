using CEA.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace CEA.Web.Pages
{
    [Authorize(Roles = "Admin")]
    public class RegisterModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public RegisterModel(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();
        public List<SelectListItem> RoleList { get; set; } = new();

        public class InputModel
        {
            [Required] public string FirstName { get; set; } = string.Empty;
            [Required] public string LastName { get; set; } = string.Empty;
            [Required][EmailAddress] public string Email { get; set; } = string.Empty;
            public string? Department { get; set; }
            [Required][StringLength(100, MinimumLength = 6)][DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
            [DataType(DataType.Password)][Compare("Password")] public string ConfirmPassword { get; set; } = string.Empty;
            public List<string> Roles { get; set; } = new();
        }

        public void OnGet()
        {
            RoleList = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                RoleList = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList();
                return Page();
            }

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                FirstName = Input.FirstName,
                LastName = Input.LastName,
                Department = Input.Department,
                IsActive = true,
                CreatedDate = DateTime.Now
            };

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (result.Succeeded)
            {
                if (Input.Roles?.Any() == true)
                    await _userManager.AddToRolesAsync(user, Input.Roles);
                return RedirectToPage("/Admin/Users/Index");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            RoleList = _roleManager.Roles.Select(r => new SelectListItem { Value = r.Name, Text = r.Name }).ToList();
            return Page();
        }
    }
}