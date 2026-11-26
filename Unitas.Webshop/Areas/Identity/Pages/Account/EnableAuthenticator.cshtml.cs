using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using QRCoder;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Areas.Identity.Pages.Account;

[Authorize, EnableRateLimiting("IdentityPost"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EnableAuthenticatorModel(UserManager<ShopUser> users, SignInManager<ShopUser> signIn,
    ApplicationDbContext database) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }
    [BindProperty, Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "Gebruik zes cijfers.")]
    public string Code { get; set; } = "";
    public string Secret { get; private set; } = "";
    public string QrImage { get; private set; } = "";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge();
        if (await users.GetTwoFactorEnabledAsync(user))
            return await HandleExistingEnrollmentAsync();
        await LoadSecretAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await users.GetUserAsync(User);
        if (user is null)
            return Challenge();
        if (await users.GetTwoFactorEnabledAsync(user))
            return await HandleExistingEnrollmentAsync();
        if (await users.IsLockedOutAsync(user))
        {
            await signIn.SignOutAsync();
            return RedirectToPage("./Login", new { ReturnUrl });
        }
        if (ModelState.IsValid)
        {
            if (await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, Code))
            {
                var result = await users.SetTwoFactorEnabledAsync(user, true);
                if (!result.Succeeded)
                {
                    ModelState.AddModelError(string.Empty, "MFA kon niet worden geactiveerd. Probeer opnieuw.");
                    await LoadSecretAsync(user);
                    return Page();
                }
                await users.ResetAccessFailedCountAsync(user);
                await database.LogAsync("mfa_enabled", user.Id);
                await signIn.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("amr", "mfa")]);
                return LocalRedirect(Destination());
            }

            await users.AccessFailedAsync(user);
            await database.LogAsync("mfa_failed", user.Id);
            if (await users.IsLockedOutAsync(user))
            {
                await signIn.SignOutAsync();
                return RedirectToPage("./Login", new { ReturnUrl });
            }
            ModelState.AddModelError(nameof(Code), "Gebruik een geldige, nieuwe code.");
        }

        await LoadSecretAsync(user);
        return Page();
    }

    private async Task<IActionResult> HandleExistingEnrollmentAsync()
    {
        if (User.HasClaim("amr", "mfa"))
            return LocalRedirect(Destination());
        await signIn.SignOutAsync();
        return RedirectToPage("./Login", new { ReturnUrl });
    }

    private string Destination() => Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/portal";

    private async Task LoadSecretAsync(ShopUser user)
    {
        Secret = await users.GetAuthenticatorKeyAsync(user)
            ?? throw new InvalidOperationException("De authenticator heeft geen sleutel.");
        const string issuer = "Unitas Bakery Systems";
        var label = Uri.EscapeDataString(issuer) + ":" + Uri.EscapeDataString(user.UserName!);
        var uri = $"otpauth://totp/{label}?secret={Secret}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        using var data = QRCodeGenerator.GenerateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        using var qr = new PngByteQRCode(data);
        QrImage = "data:image/png;base64," + Convert.ToBase64String(qr.GetGraphic(5));
    }
}
