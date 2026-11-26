using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;

namespace Unitas.Webshop.Data;

public sealed record InvoiceEmailContent(string Text, string Html)
{
    private static readonly CultureInfo Dutch = CultureInfo.GetCultureInfo("nl-NL");
    private static readonly TimeZoneInfo Amsterdam = TimeZoneInfo.FindSystemTimeZoneById("Europe/Amsterdam");
    private const string MobileStyles = """
        @media only screen and (max-width:480px) {
            .invoice-inset { padding-left:14px !important; padding-right:14px !important; }
        }
        """;

    public static InvoiceEmailContent Create(OrderReceipt receipt, InvoiceDetails invoice)
    {
        var date = TimeZoneInfo.ConvertTime(invoice.IssuedAt, Amsterdam).ToString("d MMMM yyyy", Dutch);
        var text = new StringBuilder()
            .AppendLine("Unitas Bakery Systems B.V.")
            .AppendLine()
            .AppendLine($"Factuur {receipt.Number}")
            .AppendLine($"Factuurdatum: {date}")
            .AppendLine($"E-mailadres: {invoice.Email}")
            .AppendLine($"Gekozen betaalmethode: {receipt.Method}")
            .AppendLine()
            .AppendLine("Machine | Aantal | Stukprijs | Bedrag");

        var rows = new StringBuilder();
        foreach (var line in receipt.Lines)
        {
            text.AppendLine($"{line.Name} | {line.Quantity} | {CheckoutCart.Money(line.UnitPrice)} | {CheckoutCart.Money(line.Total)}");
            rows.AppendLine($"""
                <tr>
                  <th scope="row" style="border-bottom:1px solid #d6dcd5;padding:12px 8px;text-align:left;font-weight:bold;">
                    {Escape(line.Name)}<br><span style="font-size:13px;font-weight:normal;color:#4d5a53;">{Money(line.UnitPrice)} per stuk</span>
                  </th>
                  <td align="center" style="border-bottom:1px solid #d6dcd5;padding:12px 8px;">{line.Quantity}</td>
                  <td align="right" style="border-bottom:1px solid #d6dcd5;padding:12px 8px;white-space:nowrap;">{Money(line.Total)}</td>
                </tr>
                """);
        }

        text.AppendLine().AppendLine($"Subtotaal: {CheckoutCart.Money(receipt.Subtotal)}");
        var discount = "";
        if (receipt.Discount > 0)
        {
            text.AppendLine($"Korting: -{CheckoutCart.Money(receipt.Discount)}");
            discount = $"""
                <tr>
                  <th scope="row" style="padding:5px 8px;text-align:left;font-weight:normal;color:#214a3d;">Korting</th>
                  <td align="right" style="padding:5px 8px;white-space:nowrap;color:#214a3d;">−{Money(receipt.Discount)}</td>
                </tr>
                """;
        }
        text.AppendLine($"Totaal incl. btw: {CheckoutCart.Money(receipt.Total)}")
            .AppendLine().AppendLine("Bedankt voor je bestelling bij Unitas Bakery Systems.");

        var html = $"""
            <!doctype html>
            <html lang="nl">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Factuur {Escape(receipt.Number)}</title>
              <style>{MobileStyles}</style>
            </head>
            <body style="margin:0;padding:0;background:#f5f5f0;color:#192520;font-family:Arial,sans-serif;font-size:15px;line-height:1.55;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" bgcolor="#f5f5f0">
                <tr><td align="center" style="padding:24px 12px;">
                  <!--[if mso]><table role="presentation" width="620"><tr><td><![endif]-->
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:620px;background:#ffffff;">
                    <tr><td class="invoice-inset" bgcolor="#214a3d" style="padding:20px 24px;color:#ffffff;">
                      <strong style="font-size:20px;letter-spacing:2px;">UNITAS</strong><br>
                      <span style="font-size:12px;letter-spacing:1px;color:#edc56d;">BAKERY SYSTEMS</span>
                    </td></tr>
                    <tr><td class="invoice-inset" style="padding:22px 24px 10px;">
                      <h1 style="margin:0 0 6px;font-size:28px;line-height:1.2;color:#192520;">Factuur</h1>
                      <p style="margin:0 0 18px;color:#4d5a53;">Bedankt voor je bestelling.</p>
                      <p style="margin:0 0 4px;"><strong>Bestelnummer</strong> {Escape(receipt.Number)}</p>
                      <p style="margin:0 0 4px;"><strong>Factuurdatum</strong> {Escape(date)}</p>
                      <p style="margin:0 0 4px;overflow-wrap:anywhere;"><strong>E-mailadres</strong> {Escape(invoice.Email)}</p>
                      <p style="margin:0 0 18px;"><strong>Gekozen betaalmethode</strong> {Escape(receipt.Method)}</p>
                      <table aria-label="Bestelde machines" width="100%" cellspacing="0" cellpadding="0" style="font-size:14px;border-collapse:collapse;">
                        <thead><tr bgcolor="#edf1ed">
                          <th scope="col" style="padding:8px;text-align:left;">Machine</th>
                          <th scope="col" style="padding:8px;text-align:center;">Aantal</th>
                          <th scope="col" style="padding:8px;text-align:right;">Bedrag</th>
                        </tr></thead>
                        <tbody>{rows}</tbody>
                      </table>
                      <table aria-label="Factuurtotaal" width="100%" cellspacing="0" cellpadding="0" style="margin-top:16px;border-collapse:collapse;">
                        <tr>
                          <th scope="row" style="padding:5px 8px;text-align:left;font-weight:normal;">Subtotaal</th>
                          <td align="right" style="padding:5px 8px;white-space:nowrap;">{Money(receipt.Subtotal)}</td>
                        </tr>
                        {discount}
                        <tr bgcolor="#edf1ed">
                          <th scope="row" style="padding:10px 8px;text-align:left;color:#214a3d;">Totaal incl. btw</th>
                          <td align="right" style="padding:10px 8px;white-space:nowrap;color:#214a3d;"><strong>{Money(receipt.Total)}</strong></td>
                        </tr>
                      </table>
                    </td></tr>
                    <tr><td class="invoice-inset" style="padding:16px 24px 22px;color:#4d5a53;font-size:13px;">Unitas Bakery Systems B.V.</td></tr>
                  </table>
                  <!--[if mso]></td></tr></table><![endif]-->
                </td></tr>
              </table>
            </body>
            </html>
            """;

        return new InvoiceEmailContent(text.ToString(), html);
    }

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
    private static string Money(decimal amount) => Escape(CheckoutCart.Money(amount));
}
