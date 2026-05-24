using System.Net.Http.Json;

namespace smash_dates.IntegrationTests.Infrastructure;

internal static class HttpExtensions
{
    public static string? GetSetCookieValue(this HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return null;
        }

        foreach (var sc in setCookies)
        {
            var firstSemi = sc.IndexOf(';');
            var pair = firstSemi >= 0 ? sc[..firstSemi] : sc;
            var eq = pair.IndexOf('=');
            if (eq <= 0) continue;
            var name = pair[..eq];
            if (string.Equals(name, cookieName, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(eq + 1)..]);
            }
        }

        return null;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string path, T body) =>
        client.PostAsJsonAsync(path, body);

    public static async Task LoginAsAsync(this HttpClient client, string email, string password, TestDataSeeder seeder)
    {
        await seeder.CreateUserAsync(email, password);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
    }

    public static async Task LoginAsSystemAdminAsync(this HttpClient client, string email, string password, TestDataSeeder seeder)
    {
        await seeder.CreateSystemAdminUserAsync(email, password);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
    }
}
