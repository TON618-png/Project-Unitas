using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Unitas.Webshop.Data;

public sealed class CustomerOrder
{
    public required string Id { get; set; }
    public required string UserId { get; set; }
    public long PlacedAt { get; set; }
    public required string ReceiptJson { get; set; }

    public OrderReceipt ReadReceipt() => JsonSerializer.Deserialize<OrderReceipt>(ReceiptJson)!;

    public static async Task<OrderReceipt?> SaveAsync(ApplicationDbContext database, OrderReceipt receipt, string userId)
    {
        var invoice = receipt.Invoice ?? throw new ArgumentException("De bestelling heeft geen factuurgegevens.", nameof(receipt));
        var id = invoice.Id.ToString("N");
        var placedAt = invoice.IssuedAt.ToUnixTimeMilliseconds();
        var json = JsonSerializer.Serialize(receipt);

        var inserted = await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "CustomerOrders" ("Id", "UserId", "PlacedAt", "ReceiptJson")
            VALUES ({id}, {userId}, {placedAt}, {json})
            ON CONFLICT ("Id") DO NOTHING
            """);

        if (inserted == 1)
            return receipt;

        var existing = await database.CustomerOrders.AsNoTracking()
            .SingleOrDefaultAsync(order => order.Id == id && order.UserId == userId);
        return existing?.ReadReceipt();
    }
}
