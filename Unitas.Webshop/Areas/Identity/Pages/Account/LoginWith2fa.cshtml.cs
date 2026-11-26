using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Areas.Identity.Pages.Account;

[AllowAnonymous, EnableRateLimiting("IdentityPost"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LoginWith2faModel(SignInManager<ShopUser> signIn, ApplicationDbContext database) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty, Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Gebruik zes cijfers.")]
    public string Code { get; set; } = "";

    public async Task<IActionResult> OnGetAsync() =>
        await signIn.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToPage("./Login", new { ReturnUrl }) : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await signIn.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
            return RedirectToPage("./Login", new { ReturnUrl });
        if (!ModelState.IsValid)
            return Page();

        var result = await signIn.TwoFactorAuthenticatorSignInAsync(Code, isPersistent: false, rememberClient: false);
        if (result.Succeeded)
        {
            await database.LogAsync("mfa_ok", user.Id);
            return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/portal");
        }

        await database.LogAsync(result.IsLockedOut ? "lockout" : "mfa_failed", user.Id);
        ModelState.AddModelError(nameof(Code), result.IsLockedOut
            ? "Te veel pogingen. Probeer het later opnieuw."
            : "Gebruik een geldige, nieuwe code.");
        return Page();
    }
}
