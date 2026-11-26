using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Unitas.Webshop.Data;

// Identity calls this provider during both enrollment and two-factor sign-in.
public sealed class SingleUseAuthenticator(ApplicationDbContext database) : AuthenticatorTokenProvider<ShopUser>
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public override async Task<bool> ValidateAsync(string purpose, string token, UserManager<ShopUser> manager, ShopUser user)
    {
        if (token.Length != 6 || token.Any(c => !char.IsAsciiDigit(c)))
            return false;

        var secret = await manager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(secret))
            return false;

        var key = DecodeBase32(secret);
        var step = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        var code = Encoding.ASCII.GetBytes(token);
        foreach (var candidate in new[] { step, step - 1, step + 1 })
        {
            if (!CryptographicOperations.FixedTimeEquals(code, ComputeCode(key, candidate)))
                continue;

            var updated = await database.AuthenticatorStates.Where(state => state.UserId == user.Id &&
                state.LastStep < candidate).ExecuteUpdateAsync(setters => setters.SetProperty(state => state.LastStep, candidate));
            return updated == 1;
        }

        return false;
    }

    private static byte[] ComputeCode(byte[] key, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var hash = HMACSHA1.HashData(key, counter);
        var offset = hash[^1] & 0x0f;
        var number = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset, 4)) & int.MaxValue) % 1_000_000;
        return Encoding.ASCII.GetBytes(number.ToString("D6", CultureInfo.InvariantCulture));
    }

    private static byte[] DecodeBase32(string value)
    {
        var output = new byte[value.Length * 5 / 8];
        var count = 0;
        var bits = 0;
        var buffer = 0;
        foreach (var character in value)
        {
            buffer = (buffer << 5) | Alphabet.IndexOf(char.ToUpperInvariant(character));
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                output[count++] = (byte)(buffer >> bits);
            }
        }
        return output;
    }
}
