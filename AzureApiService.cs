using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

internal static class AzureApiService
{
    // API address in Azure (already configured with the correct URL)
    private const string BaseUrl =
        "clickguard-admin-api-htenhdf7erh5e3am.westus3-01.azurewebsites.net";

    // Your Tenant ID
    private static readonly string TenantId = ReadTenantId();

    private static string ReadTenantId()
    {
        // 1. Windows Registry — configured using the company's .reg file
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\LinkAction");

            var value = key?.GetValue("TenantId")?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                Log($"TenantId loaded from Registry: {value}");
                return value;
            }
        }
        catch (Exception ex)
        {
            Log($"[AzureApiService] Error reading Record: {ex.Message}");
        }

        // 2. Environment variable — fallback for manual configuration
        var env = Environment.GetEnvironmentVariable("CLICKGUARD_TENANT_ID");
        if (!string.IsNullOrWhiteSpace(env))
        {
            Log($"TenantId loaded from the environment variable: {env}");
            return env;
        }

        // 3.No configuration found
        Log(" WARNING: TenantId not configured! The ClickGuard API will not be consulted.");
        Log(" Solution: Run the company's .reg file on this machine.");
        return string.Empty;
    }

    // Time the list is saved in memory before searching again (5 minutes)
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    // The "messenger" that makes HTTP calls to the API.
    private static readonly HttpClient Http = new(
        new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        })
    {
        BaseAddress = new Uri(BaseUrl),
        Timeout = TimeSpan.FromSeconds(8)
    };

    static AzureApiService()
    {
        Http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    // List of trusted domains saved in memory
    private static HashSet<string> _cachedDomains = new(StringComparer.OrdinalIgnoreCase);
    private static DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Check if the domain is on ClickGuard's list of trusted sites.
    /// If the API is down, it returns false without crashing the program.
    /// </summary>
    public static async Task<bool> IsAllowedDomainAsync(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;

        try
        {
            var domains = await GetCachedDomainsAsync();
            host = host.ToLowerInvariant();

            foreach (var pattern in domains)
            {
                if (host == pattern || host.EndsWith("." + pattern, StringComparison.Ordinal))
                    return true;
            }
        }
        catch (Exception ex) { LogError("IsAllowedDomainAsync", ex); }

        return false;
    }

    private static async Task<HashSet<string>> GetCachedDomainsAsync()
    {
        if (DateTime.UtcNow < _cacheExpiry)
            return _cachedDomains;

        await _lock.WaitAsync();
        try
        {
            if (DateTime.UtcNow < _cacheExpiry)
                return _cachedDomains;

            var domains = await FetchAllowlistAsync();
            _cachedDomains = domains;
            _cacheExpiry = DateTime.UtcNow + CacheTtl;
            Log($"Updated list: {domains.Count} domain(s).");
            return _cachedDomains;
        }
        finally { _lock.Release(); }
    }

    private static async Task<HashSet<string>> FetchAllowlistAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var url = $"/api/agent/allowlist?tenantId={Uri.EscapeDataString(TenantId)}";
            var response = await Http.GetAsync(url, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                LogError("FetchAllowlistAsync", new Exception($"HTTP {(int)response.StatusCode}"));
                return _cachedDomains;
            }

            var body = await response.Content.ReadAsStringAsync(cts.Token);
            var result = JsonSerializer.Deserialize<AgentAllowlistResponse>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (result?.Domains != null)
                foreach (var d in result.Domains)
                    if (!string.IsNullOrWhiteSpace(d))
                        set.Add(d.Trim().ToLowerInvariant());

            return set;
        }
        catch (Exception ex)
        {
            LogError("FetchAllowlistAsync", ex);
            return _cachedDomains;
        }
    }

    private sealed class AgentAllowlistResponse
    {
        public List<string>? Domains { get; set; }
    }

    private static void LogError(string method, Exception ex) =>
        Log($"[AzureApiService.{method}] ERRO: {ex.Message}");

    private static void Log(string msg)
    {
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LinkAction");
        System.IO.Directory.CreateDirectory(dir);
        System.IO.File.AppendAllText(
            System.IO.Path.Combine(dir, "log.txt"),
            $"{DateTime.Now:O} {msg}{Environment.NewLine}");
    }
}