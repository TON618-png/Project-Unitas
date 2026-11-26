using System.Globalization;
using System.Text.Json;

namespace Unitas.Webshop.Data;

public sealed class CheckoutCart(IHttpContextAccessor httpContext)
{
    public const int MaxQuantity = 99;
    private const string PromotionCode = "UNITAS10";
    private const string CartKey = "checkout.cart";
    private const string ReceiptKey = "checkout.receipt";
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");

    private ISession Session => httpContext.HttpContext!.Session;
    private CartState? state;
    private CartState State => state ??= Read<CartState>(CartKey) ?? new([], null, Guid.Empty);

    public IReadOnlyList<CartLine> Lines => Catalog.Products
        .Where(product => State.Quantities.ContainsKey(product.Id))
        .Select(product => new CartLine(product, State.Quantities[product.Id]))
        .ToArray();

    public string? DiscountCode => State.DiscountCode;
    public Guid Version => State.Version;
    public decimal Subtotal => Lines.Sum(line => line.Total);
    public decimal Discount => DiscountCode == PromotionCode
        ? Math.Round(Subtotal * 0.10m, 2, MidpointRounding.AwayFromZero)
        : 0m;
    public decimal Total => Subtotal - Discount;
    public OrderReceipt? Receipt => Read<OrderReceipt>(ReceiptKey);

    public static string Money(decimal amount) => amount.ToString("C", Dutch);

    public int QuantityFor(string id) => State.Quantities.GetValueOrDefault(id);

    public void SetQuantity(string id, int quantity)
    {
        if (quantity == 0)
            State.Quantities.Remove(id);
        else
            State.Quantities[id] = quantity;

        Save(State.Quantities.Count == 0 ? null : DiscountCode);
    }

    public bool TryApplyDiscount(string? code)
    {
        if (!string.Equals(code?.Trim(), PromotionCode, StringComparison.OrdinalIgnoreCase))
            return false;

        if (DiscountCode != PromotionCode)
            Save(PromotionCode);
        return true;
    }

    public void RemoveDiscount() => Save(null);

    public OrderReceipt CreateReceipt(string method, string email, string? userId) =>
        new(
            $"UBS-{Version:N}"[..16].ToUpperInvariant(),
            method,
            Lines.Select(line => new ReceiptLine(line.Product.Name, line.Quantity, line.Total, line.Product.Price)).ToArray(),
            Subtotal, Discount, Total,
            new InvoiceDetails(Version, email, DateTimeOffset.UtcNow), UserId: userId);

    public void Complete(OrderReceipt receipt)
    {
        SaveReceipt(receipt);
        Session.Remove(CartKey);
        state = null;
    }

    public void SaveReceipt(OrderReceipt receipt) => Session.SetString(ReceiptKey, JsonSerializer.Serialize(receipt));

    private void Save(string? code)
    {
        state = new CartState(State.Quantities, code, Guid.NewGuid());
        Session.SetString(CartKey, JsonSerializer.Serialize(state));
    }

    private T? Read<T>(string key) => Session.GetString(key) is { } json
        ? JsonSerializer.Deserialize<T>(json)
        : default;

    private sealed record CartState(Dictionary<string, int> Quantities, string? DiscountCode, Guid Version);
}

public sealed record CartLine(Product Product, int Quantity)
{
    public decimal Total => Product.Price * Quantity;
}

public sealed record ReceiptLine(string Name, int Quantity, decimal Total, decimal UnitPrice = 0);

public sealed record OrderReceipt(
    string Number, string Method, IReadOnlyList<ReceiptLine> Lines, decimal Subtotal, decimal Discount, decimal Total,
    InvoiceDetails? Invoice = null, bool InvoiceSent = false, string? UserId = null);

public sealed record InvoiceDetails(Guid Id, string Email, DateTimeOffset IssuedAt);
