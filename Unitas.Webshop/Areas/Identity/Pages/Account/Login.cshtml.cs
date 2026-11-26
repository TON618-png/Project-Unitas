using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Areas.Identity.Pages.Account;

[AllowAnonymous, EnableRateLimiting("IdentityPost"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LoginModel(UserManager<ShopUser> users, SignInManager<ShopUser> signIn, ApplicationDbContext database) : PageModel
{
    [BindProperty] public Credentials Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true ? GoAfterPassword() : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            return GoAfterPassword();
        if (!ModelState.IsValid)
            return Page();

        var username = Input.Username.Trim();
        var user = await users.FindByNameAsync(username);
        var result = await signIn.PasswordSignInAsync(username, Input.Password, isPersistent: false, lockoutOnFailure: true);
        if (!result.Succeeded && !result.RequiresTwoFactor)
        {
            await database.LogAsync(result.IsLockedOut ? "lockout" : "password_failed", user?.Id);
            ModelState.AddModelError(string.Empty, "Gebruikersnaam of wachtwoord onjuist, of probeer het later opnieuw.");
            return Page();
        }

        await database.LogAsync("password_ok", user?.Id);
        return RedirectToPage(result.RequiresTwoFactor ? "./LoginWith2fa" : "./EnableAuthenticator", new { ReturnUrl });
    }

    private IActionResult GoAfterPassword() => User.HasClaim("amr", "mfa")
        ? LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/portal")
        : RedirectToPage("./EnableAuthenticator", new { ReturnUrl });

    public sealed class Credentials
    {
        [Required(ErrorMessage = "Vul je gebruikersnaam in.")]
        [StringLength(32)]
        [Display(Name = "Gebruikersnaam")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Vul je wachtwoord in."), StringLength(128), DataType(DataType.Password)]
        [Display(Name = "Wachtwoord")]
        public string Password { get; set; } = "";
    }
}
