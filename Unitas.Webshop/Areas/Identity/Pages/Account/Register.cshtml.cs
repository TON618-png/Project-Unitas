using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Areas.Identity.Pages.Account;

[AllowAnonymous, EnableRateLimiting("IdentityPost"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RegisterModel(UserManager<ShopUser> users, SignInManager<ShopUser> signIn, ApplicationDbContext database) : PageModel
{
    [BindProperty] public Registration Input { get; set; } = new();
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => User.Identity?.IsAuthenticated == true
        ? RedirectToPage("./Login", new { ReturnUrl })
        : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToPage("./Login", new { ReturnUrl });
        if (!ModelState.IsValid)
            return Page();

        await using var transaction = await database.Database.BeginTransactionAsync();
        var user = new ShopUser { UserName = Input.Username.Trim() };
        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Kies een andere gebruikersnaam of controleer de invoer.");
            return Page();
        }

        var authenticator = await users.ResetAuthenticatorKeyAsync(user);
        if (!authenticator.Succeeded)
            throw new InvalidOperationException("De authenticator kan niet worden voorbereid.");
        database.AuthenticatorStates.Add(new AuthenticatorState { UserId = user.Id });
        await database.LogAsync("registration", user.Id);
        await transaction.CommitAsync();
        await signIn.SignInAsync(user, isPersistent: false);
        return RedirectToPage("./EnableAuthenticator", new { ReturnUrl });
    }

    public sealed class Registration
    {
        [Required(ErrorMessage = "Vul je gebruikersnaam in.")]
        [RegularExpression(@"^[a-zA-Z0-9][a-zA-Z0-9_.-]{2,31}$", ErrorMessage = "Gebruik 3 tot 32 letters, cijfers, punten, underscores of streepjes.")]
        [Display(Name = "Gebruikersnaam")]
        public string Username { get; set; } = "";

        [Required(ErrorMessage = "Vul een wachtwoord in.")]
        [StringLength(128, MinimumLength = 8, ErrorMessage = "Gebruik 8 tot 128 tekens.")]
        [DataType(DataType.Password), Display(Name = "Wachtwoord")]
        public string Password { get; set; } = "";
    }
}
