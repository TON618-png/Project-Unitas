using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Pages;

[Authorize(Policy = "Mfa"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class OrdersModel(ApplicationDbContext database, UserManager<ShopUser> users) : PageModel
{
    private const int PageSize = 12;
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");
    private static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");

    public IReadOnlyList<OrderReceipt> Orders { get; private set; } = [];
    public int PageNumber { get; private set; }
    public int PageCount { get; private set; }

    public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
    {
        var userId = users.GetUserId(User);
        if (userId is null)
            return Challenge();

        var query = database.CustomerOrders.AsNoTracking().Where(order => order.UserId == userId);
        var count = await query.CountAsync();
        PageCount = count == 0 ? 1 : (count - 1) / PageSize + 1;
        PageNumber = Math.Clamp(pageNumber, 1, PageCount);
        var orders = await query.OrderByDescending(order => order.PlacedAt).ThenByDescending(order => order.Id)
            .Skip((PageNumber - 1) * PageSize).Take(PageSize).ToListAsync();
        Orders = orders.Select(order => order.ReadReceipt()).ToArray();
        return Page();
    }

    public static string DisplayDate(DateTimeOffset date) =>
        TimeZoneInfo.ConvertTime(date, Amsterdam).ToString("d MMMM yyyy", Dutch);
}
