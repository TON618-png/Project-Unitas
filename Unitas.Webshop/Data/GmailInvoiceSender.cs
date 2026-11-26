using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Unitas.Webshop.Data;

public sealed class GmailInvoiceOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }
    public string? From { get; set; }
}

public sealed class GmailInvoiceSender(
    IHttpClientFactory clients, IOptions<GmailInvoiceOptions> options, IMemoryCache cache, ILogger<GmailInvoiceSender> logger)
{
    private static readonly object AccessTokenKey = new();
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.ClientId);

    public async Task<bool> SendAsync(MailAddress recipient, string subject, InvoiceEmailContent content, CancellationToken cancellationToken)
    {
        var gmail = options.Value;
        if (string.IsNullOrWhiteSpace(gmail.ClientId) || string.IsNullOrWhiteSpace(gmail.ClientSecret) ||
            string.IsNullOrWhiteSpace(gmail.RefreshToken) || !MailAddress.TryCreate(gmail.From, out var from))
        {
            logger.LogWarning("De Gmail-koppeling mist OAuth-instellingen. Voer Configure-Gmail.ps1 uit.");
            return false;
        }

        using var client = clients.CreateClient("Gmail");
        var token = await GetAccessTokenAsync(client, gmail, cancellationToken);
        if (token is null)
            return false;

        var boundary = $"unitas-{Guid.NewGuid():N}";
        var mime = string.Join("\r\n",
            $"From: Unitas Bakery Systems <{from.Address}>",
            $"To: {recipient}",
            $"Subject: {subject}",
            $"Date: {DateTimeOffset.UtcNow.ToString("ddd, dd MMM yyyy HH:mm:ss '+0000'", CultureInfo.InvariantCulture)}",
            "MIME-Version: 1.0",
            $"Content-Type: multipart/alternative; boundary=\"{boundary}\"",
            "",
            $"--{boundary}",
            "Content-Type: text/plain; charset=UTF-8",
            "Content-Transfer-Encoding: base64",
            "",
            EncodePart(content.Text),
            $"--{boundary}",
            "Content-Type: text/html; charset=UTF-8",
            "Content-Transfer-Encoding: base64",
            "",
            EncodePart(content.Html),
            $"--{boundary}--",
            "");
        var raw = Convert.ToBase64String(Encoding.UTF8.GetBytes(mime)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://gmail.googleapis.com/gmail/v1/users/me/messages/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Content = JsonContent.Create(new { raw });
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
            return true;

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            cache.Remove(AccessTokenKey);
        logger.LogWarning("De Gmail API weigerde de verzending (HTTP {Status}).", (int)response.StatusCode);
        return false;
    }

    private static string EncodePart(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ReplaceLineEndings("\r\n")), Base64FormattingOptions.InsertLineBreaks);

    private async Task<string?> GetAccessTokenAsync(
        HttpClient client, GmailInvoiceOptions gmail, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue<string>(AccessTokenKey, out var cached))
            return cached;

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = gmail.ClientId!,
            ["client_secret"] = gmail.ClientSecret!,
            ["refresh_token"] = gmail.RefreshToken!,
            ["grant_type"] = "refresh_token"
        });
        using var response = await client.PostAsync("https://oauth2.googleapis.com/token", form, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google OAuth heeft het token niet vernieuwd (HTTP {Status}).", (int)response.StatusCode);
            return null;
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (string.IsNullOrWhiteSpace(token?.AccessToken))
            return null;
        if (token.ExpiresIn > 60)
            cache.Set(AccessTokenKey, token.AccessToken, TimeSpan.FromSeconds(token.ExpiresIn - 60));
        return token.AccessToken;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
