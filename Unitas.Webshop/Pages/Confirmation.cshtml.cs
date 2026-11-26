using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Pages;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class ConfirmationModel(CheckoutCart cart, InvoiceEmailSender invoiceSender, UserManager<ShopUser> users) : PageModel
{
    public OrderReceipt Receipt { get; private set; } = null!;

    public IActionResult OnGet()
    {
        if (GetVisibleReceipt() is not { } receipt)
            return RedirectToPage("/Cart");

        Receipt = receipt;
        return Page();
    }

    public async Task<IActionResult> OnPostResendInvoiceAsync(Guid invoiceId)
    {
        if (GetVisibleReceipt() is not { Invoice: { } invoice } receipt)
            return RedirectToPage("/Cart");

        if (!receipt.InvoiceSent && invoice.Id == invoiceId)
        {
            var updated = await invoiceSender.SendAsync(receipt);
            cart.SaveReceipt(updated);
            if (updated.InvoiceSent)
                TempData.ShowToast("Je factuur is verzonden.");
        }

        return RedirectToPage();
    }

    private OrderReceipt? GetVisibleReceipt()
    {
        var receipt = cart.Receipt;
        return receipt?.UserId is null || receipt.UserId == users.GetUserId(User) ? receipt : null;
    }
}
