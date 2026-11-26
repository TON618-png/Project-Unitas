using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Pages;

[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class CartModel(CheckoutCart cart) : PageModel
{
    public CheckoutCart Cart => cart;

    public IActionResult OnPostAdd(string id)
    {
        if (Catalog.Find(id) is not { } product)
            return NotFound();

        var quantity = cart.QuantityFor(id);
        if (quantity >= CheckoutCart.MaxQuantity)
            TempData.ShowToast($"Je kunt maximaal {CheckoutCart.MaxQuantity} stuks per machine toevoegen.", isError: true);
        else
        {
            cart.SetQuantity(id, quantity + 1);
            TempData.ShowToast($"{product.Name} is toegevoegd aan je winkelwagen.");
        }
        return RedirectToPage();
    }

    public IActionResult OnPostUpdate(string? id, int quantity)
    {
        if (id is null || cart.QuantityFor(id) == 0)
            return RedirectToPage();

        if (!ModelState.IsValid || quantity is < 1 or > CheckoutCart.MaxQuantity)
            TempData.ShowToast($"Kies een aantal tussen 1 en {CheckoutCart.MaxQuantity}.", isError: true);
        else
        {
            cart.SetQuantity(id, quantity);
            TempData.ShowToast("Het aantal in je winkelwagen is bijgewerkt.");
        }
        return RedirectToPage();
    }

    public IActionResult OnPostRemove(string? id)
    {
        if (id is not null && Catalog.Find(id) is { } product && cart.QuantityFor(id) > 0)
        {
            cart.SetQuantity(id, 0);
            TempData.ShowToast($"{product.Name} is uit je winkelwagen verwijderd.");
        }
        return RedirectToPage();
    }
}
