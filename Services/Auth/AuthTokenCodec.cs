using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace smash_dates.Services.Auth;

// Random, URL-safe one-time tokens for password reset / email verification. Only the SHA-256
// hash is persisted; the raw token travels in the emailed link.
public static class AuthTokenCodec
{
    public static string NewToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
