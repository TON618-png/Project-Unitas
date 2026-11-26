using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Pages.Machines;

public sealed class ProductModel(ApplicationDbContext database, UserManager<ShopUser> users,
    IAuthorizationService authorization) : PageModel
{
    public Product Machine { get; private set; } = null!;
    [BindProperty] public QuoteInput Input { get; set; } = new();

    public IActionResult OnGet(string id)
    {
        var machine = Catalog.Find(id);
        if (machine is null)
            return NotFound();
        Machine = machine;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        var machine = Catalog.Find(id);
        if (machine is null)
            return NotFound();
        Machine = machine;
        if (!(await authorization.AuthorizeAsync(User, "Mfa")).Succeeded)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Challenge();
        if (!ModelState.IsValid)
            return Page();

        var userId = users.GetUserId(User);
        if (userId is null)
            return Challenge();

        database.QuoteRequests.Add(new QuoteRequest
        {
            UserId = userId, ProductId = machine.Id, Quantity = Input.Quantity, Note = Input.Note?.Trim()
        });
        await database.SaveChangesAsync();
        TempData.ShowToast("Je offerteaanvraag is opgeslagen.");
        return RedirectToPage("/Portal");
    }

    public sealed class QuoteInput
    {
        [Range(1, 99, ErrorMessage = "Kies een aantal tussen 1 en 99."), Display(Name = "Aantal")]
        public int Quantity { get; set; } = 1;
        [StringLength(1000, ErrorMessage = "Gebruik maximaal 1000 tekens.")]
        [Display(Name = "Toelichting of vraag")]
        public string? Note { get; set; }
    }
}
