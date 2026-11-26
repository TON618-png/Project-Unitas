using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Unitas.Webshop.Data;

public sealed class ShopUser : IdentityUser
{
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}

public sealed class AuthenticatorState
{
    public required string UserId { get; set; }
    public long LastStep { get; set; } = -1;
}

public sealed class QuoteRequest
{
    public int Id { get; set; }
    public required string UserId { get; set; }
    public required string ProductId { get; set; }
    public int Quantity { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AuthEvent
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public required string Event { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IDataProtectionProvider protection)
    : IdentityDbContext<ShopUser>(options)
{
    public DbSet<QuoteRequest> QuoteRequests => Set<QuoteRequest>();
    public DbSet<AuthEvent> AuthEvents => Set<AuthEvent>();
    public DbSet<AuthenticatorState> AuthenticatorStates => Set<AuthenticatorState>();
    public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        var tokenProtector = protection.CreateProtector("Unitas.Webshop.Identity.UserToken.v1");
        model.Entity<IdentityUserToken<string>>().Property(token => token.Value)
            .HasConversion(value => tokenProtector.Protect(value!), value => tokenProtector.Unprotect(value!));
        model.Entity<AuthenticatorState>().HasKey(item => item.UserId);
        model.Entity<AuthenticatorState>().HasOne<ShopUser>().WithOne().HasForeignKey<AuthenticatorState>(item => item.UserId);
        model.Entity<QuoteRequest>().HasOne<ShopUser>().WithMany().HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<QuoteRequest>().Property(item => item.ProductId).HasMaxLength(64);
        model.Entity<QuoteRequest>().Property(item => item.Note).HasMaxLength(1000);
        model.Entity<QuoteRequest>().ToTable(table => table.HasCheckConstraint("CK_QuoteRequests_Quantity",
            "\"Quantity\" BETWEEN 1 AND 99"));
        model.Entity<AuthEvent>().Property(item => item.Event).HasMaxLength(30);
        model.Entity<AuthEvent>().HasIndex(item => item.CreatedAt);
        model.Entity<CustomerOrder>().HasOne<ShopUser>().WithMany().HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        model.Entity<CustomerOrder>().HasIndex(item => new { item.UserId, item.PlacedAt, item.Id })
            .IsDescending(false, true, true);
    }

    public async Task LogAsync(string activity, string? userId = null)
    {
        AuthEvents.Add(new AuthEvent { Event = activity, UserId = userId });
        await SaveChangesAsync();
    }
}

public static class DatabaseSetup
{
    public const string OrderHistorySchema = """
        CREATE TABLE IF NOT EXISTS "CustomerOrders" (
            "Id" text NOT NULL PRIMARY KEY,
            "UserId" text NOT NULL,
            "PlacedAt" bigint NOT NULL,
            "ReceiptJson" text NOT NULL,
            CONSTRAINT "FK_CustomerOrders_AspNetUsers_UserId"
                FOREIGN KEY ("UserId") REFERENCES "AspNetUsers" ("Id") ON DELETE RESTRICT
        );
        CREATE INDEX IF NOT EXISTS "IX_CustomerOrders_UserId_PlacedAt_Id"
            ON "CustomerOrders" ("UserId", "PlacedAt" DESC, "Id" DESC);
        """;

    public const string ReportingViews = """
        CREATE OR REPLACE VIEW public.webshop_report AS
        SELECT "ProductId" AS product, count(*) AS requests, sum("Quantity") AS units
        FROM public."QuoteRequests" GROUP BY "ProductId";

        CREATE OR REPLACE VIEW public.auth_report AS
        SELECT ("CreatedAt" AT TIME ZONE 'UTC')::date AS day, "Event" AS event, count(*) AS total
        FROM public."AuthEvents" GROUP BY 1, 2;
        """;
}
