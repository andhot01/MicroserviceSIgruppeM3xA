using System.Net;
using System.Text.Json;

namespace src.Infrastructure;

public static class VaultSecretReader
{
    public static async Task<string> ReadAsync(
        IConfiguration configuration, string secretName, string key)
    {
        var address = configuration["VAULT_ADDR"] ?? "http://localhost:8200";
        var token = configuration["VAULT_TOKEN"]
                    ?? throw new InvalidOperationException("Sæt VAULT_TOKEN til applikationstokenet.");

        using var client = new HttpClient
        {
            BaseAddress = new Uri(address.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.Add("X-Vault-Token", token);

        using var response = await client.GetAsync($"v1/secret/data/{secretName}");
        if (response.StatusCode == HttpStatusCode.Forbidden)
            throw new InvalidOperationException("Vault afviste adgang: tjek token, TTL og policy.");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var value = document.RootElement.GetProperty("data").GetProperty("data")
            .GetProperty(key).GetString();

        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Vault-secret '{key}' mangler eller er tom.");

        return value;
    }
}