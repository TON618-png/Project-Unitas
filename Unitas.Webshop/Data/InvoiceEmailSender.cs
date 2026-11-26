using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Unitas.Webshop.Data;

public sealed class InvoiceSmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string? From { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
}

public sealed class InvoiceEmailSender(
    GmailInvoiceSender gmail, IOptions<InvoiceSmtpOptions> options, IMemoryCache cache, ILogger<InvoiceEmailSender> logger)
{
    private readonly ConcurrentDictionary<Guid, Lazy<Task<string?>>> pending = new();

    public async Task<OrderReceipt> SendAsync(OrderReceipt receipt)
    {
        if (receipt.InvoiceSent || receipt.Invoice is not { } invoice)
            return receipt;

        var send = pending.GetOrAdd(invoice.Id, _ => new Lazy<Task<string?>>(() => SendOnceAsync(receipt, invoice)));
        try
        {
            var address = await send.Value;
            return receipt with { InvoiceSent = string.Equals(invoice.Email, address, StringComparison.Ordinal) };
        }
        finally
        {
            pending.TryRemove(new KeyValuePair<Guid, Lazy<Task<string?>>>(invoice.Id, send));
        }
    }

    private async Task<string?> SendOnceAsync(OrderReceipt receipt, InvoiceDetails invoice)
    {
        if (cache.TryGetValue<string>(invoice.Id, out var address))
            return address;

        try
        {
            var recipient = new MailAddress(invoice.Email);
            var subject = $"Factuur {receipt.Number} | Unitas Bakery Systems";
            var content = InvoiceEmailContent.Create(receipt, invoice);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var sent = gmail.IsConfigured
                ? await gmail.SendAsync(recipient, subject, content, timeout.Token)
                : await SendViaSmtpAsync(recipient, subject, content, timeout.Token);
            if (!sent)
                return null;
            cache.Set(invoice.Id, invoice.Email, TimeSpan.FromMinutes(30));
            return invoice.Email;
        }
        catch (SmtpException exception)
        {
            logger.LogWarning("SMTP weigerde factuur {OrderNumber} met status {Status}.",
                receipt.Number, exception.StatusCode);
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or OperationCanceledException)
        {
            logger.LogWarning("Factuur {OrderNumber} kon niet worden verzonden ({FailureType}).",
                receipt.Number, exception.GetType().Name);
            return null;
        }
    }

    private async Task<bool> SendViaSmtpAsync(
        MailAddress recipient, string subject, InvoiceEmailContent content, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        if (string.IsNullOrWhiteSpace(smtp.Host) || smtp.Port is < 1 or > 65535 ||
            !MailAddress.TryCreate(smtp.From, out var sender))
        {
            logger.LogWarning("Stel Gmail OAuth of een SMTP-server in om facturen te mailen.");
            return false;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(sender.Address, "Unitas Bakery Systems"),
            Subject = subject,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(recipient);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            content.Text, Encoding.UTF8, MediaTypeNames.Text.Plain));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            content.Html, Encoding.UTF8, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(smtp.Host, smtp.Port) { EnableSsl = smtp.EnableSsl };
        if (!string.IsNullOrWhiteSpace(smtp.Username))
            client.Credentials = new NetworkCredential(smtp.Username, smtp.Password);
        await client.SendMailAsync(message, cancellationToken);
        return true;
    }
}
