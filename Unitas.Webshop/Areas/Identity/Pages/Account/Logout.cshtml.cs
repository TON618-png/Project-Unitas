using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Areas.Identity.Pages.Account;

[Authorize]
public sealed class LogoutModel(SignInManager<ShopUser> signIn, UserManager<ShopUser> users, ApplicationDbContext database)
    : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index", new { area = "" });

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = users.GetUserId(User);
        await signIn.SignOutAsync();
        await database.LogAsync("logout", userId);
        return RedirectToPage("/Index", new { area = "" });
    }
}
