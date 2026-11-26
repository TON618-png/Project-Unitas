using System.Data.Common;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Pages;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CheckoutModel(CheckoutCart cart, UserManager<ShopUser> users, ApplicationDbContext database,
    InvoiceEmailSender invoiceSender, ILogger<CheckoutModel> logger) : PageModel
{
    public static IReadOnlyList<PaymentOption> Options { get; } =
    [
        new("ideal", "iDEAL", "Via je bank"),
        new("card", "Creditcard", "Visa of Mastercard"),
        new("paypal", "PayPal", "Via je PayPal-account")
    ];

    public CheckoutCart Cart => cart;
    [BindProperty] public string? Method { get; set; } = "ideal";
    [BindProperty] public string? DiscountCode { get; set; }
    [BindProperty] public Guid CartVersion { get; set; }
    [BindProperty] public string? Email { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (cart.Lines.Count == 0)
            return RedirectToPage("/Cart");
        if (User.Identity?.IsAuthenticated == true)
            Email = (await users.GetUserAsync(User))?.Email;
        return Page();
    }

    public IActionResult OnPostApplyDiscount()
    {
        if (cart.Lines.Count == 0)
            return RedirectToPage("/Cart");

        ApplyDiscount();
        return Page();
    }

    public IActionResult OnPostRemoveDiscount()
    {
        if (cart.Lines.Count == 0)
            return RedirectToPage("/Cart");

        cart.RemoveDiscount();
        TempData.ShowToast("De kortingscode is verwijderd.");
        return Page();
    }

    public async Task<IActionResult> OnPostPlaceOrderAsync()
    {
        if (cart.Lines.Count == 0)
            return RedirectToPage(cart.Receipt is null ? "/Cart" : "/Confirmation");
        if (CartVersion != cart.Version)
        {
            ModelState.AddModelError(string.Empty, "Je winkelwagen is gewijzigd. Controleer het nieuwe overzicht en probeer opnieuw.");
            return Page();
        }

        var option = Options.FirstOrDefault(option => option.Id == Method);
        if (option is null)
            ModelState.AddModelError(nameof(Method), "Kies een beschikbare betaalmethode.");

        var email = Email?.Trim();
        if (email is null or "" || email.Length > 254 || email.Any(char.IsControl) ||
            !MailAddress.TryCreate(email, out var address) ||
            !string.Equals(email, address.Address, StringComparison.Ordinal) || address.DisplayName.Length > 0)
        {
            ModelState.AddModelError(nameof(Email), "Vul een geldig e-mailadres in voor je factuur.");
            email = null;
        }

        if (cart.DiscountCode is null && !string.IsNullOrWhiteSpace(DiscountCode))
        {
            ApplyDiscount();
            return Page();
        }

        if (!ModelState.IsValid || option is null || email is null)
            return Page();

        var userId = users.GetUserId(User);
        var receipt = cart.CreateReceipt(option.Name, email, userId);
        if (userId is not null)
        {
            try
            {
                var stored = await CustomerOrder.SaveAsync(database, receipt, userId);
                if (stored is null)
                {
                    ModelState.AddModelError(string.Empty, "De bestelling kon niet aan dit account worden gekoppeld. Open je winkelwagen en probeer opnieuw.");
                    return Page();
                }
                receipt = stored;
            }
            catch (DbException exception)
            {
                logger.LogError(exception, "Bestelling {OrderNumber} kon niet worden opgeslagen.", receipt.Number);
                ModelState.AddModelError(string.Empty, "Je bestelling kon niet worden opgeslagen. Probeer het opnieuw.");
                return Page();
            }
        }

        cart.Complete(receipt);
        cart.SaveReceipt(await invoiceSender.SendAsync(receipt));
        TempData.ShowToast("Je bestelling is geplaatst.");
        return RedirectToPage("/Confirmation");
    }

    private void ApplyDiscount()
    {
        if (string.IsNullOrWhiteSpace(DiscountCode))
            ModelState.AddModelError(nameof(DiscountCode), "Vul een kortingscode in.");
        else if (!cart.TryApplyDiscount(DiscountCode))
            ModelState.AddModelError(nameof(DiscountCode), "Deze kortingscode is ongeldig.");
        else
            TempData.ShowToast("Je kortingscode is toegepast. Je krijgt 10% korting.");
    }

    public sealed record PaymentOption(string Id, string Name, string Description);
}
