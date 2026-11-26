using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Unitas.Webshop.Data;

namespace Unitas.Webshop.Pages.Machines;

public sealed class ManualModel : PageModel
{
    public Product Machine { get; private set; } = null!;

    public IActionResult OnGet(string id)
    {
        var machine = Catalog.Find(id);
        if (machine is null)
            return NotFound();
        Machine = machine;
        return Page();
    }
}
