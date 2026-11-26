using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Pages;

[Authorize(Policy = "Mfa"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PortalModel(ApplicationDbContext database, UserManager<ShopUser> users) : PageModel
{
    public IReadOnlyList<QuoteRequest> Quotes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = users.GetUserId(User);
        if (userId is null)
            return Challenge();
        Quotes = await database.QuoteRequests.AsNoTracking().Where(item => item.UserId == userId)
            .OrderByDescending(item => item.CreatedAt).Take(100).ToListAsync();
        return Page();
    }
}
