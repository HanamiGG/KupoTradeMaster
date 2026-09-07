using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KupoTradeMaster.Services;

public sealed class UniversalisClient : IDisposable
{
    private const string BaseUrl = "https://universalis.app/api/v2/";
    private const int MaxIdsPerBatch = 40;
    private const int MaxConcurrent = 2;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly SemaphoreSlim _gate = new(MaxConcurrent, MaxConcurrent);
    private Action<string, Exception?>? _diag;

    public void AttachDiagnostics(Action<string, Exception?> sink) => _diag = sink;

    public UniversalisClient(string userAgent)
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        };
        _http = new HttpClient(handler, disposeHandler: true) { BaseAddress = new Uri(BaseUrl) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<Dictionary<string, int>?> FetchTaxRatesAsync(string worldName, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var url = $"tax-rates?world={Uri.EscapeDataString(worldName)}";
            return await GetJsonAsync<Dictionary<string, int>>(url, ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    public async Task<Dictionary<uint, UniversalisItem>> FetchAsync(
        string scope,
        IReadOnlyCollection<uint> itemIds,
        CancellationToken ct = default)
    {
        var results = new Dictionary<uint, UniversalisItem>();
        if (itemIds.Count == 0) return results;

        // Dispatch all batches in parallel; the semaphore caps in-flight requests at MaxConcurrent.
        // Awaiting each _gate.WaitAsync inline in a foreach makes the semaphore inert — batches ran
        // strictly serially, doubling wall-clock time on 240-item valuation fetches.
        var tasks = new List<Task<KeyValuePair<uint, UniversalisItem>[]>>();
        foreach (var batch in Chunk(itemIds, MaxIdsPerBatch))
        {
            ct.ThrowIfCancellationRequested();
            tasks.Add(FetchBatchAsync(scope, batch, ct));
        }

        foreach (var t in tasks)
        {
            var pairs = await t.ConfigureAwait(false);
            foreach (var kv in pairs) results[kv.Key] = kv.Value;
        }

        return results;
    }

    private async Task<KeyValuePair<uint, UniversalisItem>[]> FetchBatchAsync(
        string scope, IReadOnlyList<uint> batch, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var url = $"{Uri.EscapeDataString(scope)}/{string.Join(",", batch)}?listings=5&entries=5";
            try
            {
                if (batch.Count == 1)
                {
                    var single = await GetJsonAsync<UniversalisItem>(url, ct).ConfigureAwait(false);
                    if (single != null && single.ItemId != 0)
                        return new[] { new KeyValuePair<uint, UniversalisItem>(single.ItemId, single) };
                    _diag?.Invoke($"Universalis single-item ItemId=0 (id={batch[0]}, scope={scope})", null);
                    return Array.Empty<KeyValuePair<uint, UniversalisItem>>();
                }

                var multi = await GetJsonAsync<UniversalisMultiItemResponse>(url, ct).ConfigureAwait(false);
                if (multi?.Items == null || multi.Items.Count == 0)
                {
                    _diag?.Invoke($"Universalis multi-item empty (batch={batch.Count}, scope={scope})", null);
                    return Array.Empty<KeyValuePair<uint, UniversalisItem>>();
                }

                var pairs = new List<KeyValuePair<uint, UniversalisItem>>(multi.Items.Count);
                foreach (var (idStr, item) in multi.Items)
                    if (uint.TryParse(idStr, out var id))
                        pairs.Add(new KeyValuePair<uint, UniversalisItem>(id, item));
                return pairs.ToArray();
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _diag?.Invoke($"Universalis batch failed (size={batch.Count}, scope={scope})", ex);
                return Array.Empty<KeyValuePair<uint, UniversalisItem>>();
            }
        }
        finally { _gate.Release(); }
    }

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var preview = await SafeBodyPreviewAsync(resp, ct).ConfigureAwait(false);
            _diag?.Invoke($"Universalis HTTP {(int)resp.StatusCode} at {url} — {preview}", null);
            return default;
        }
        await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOpts, ct).ConfigureAwait(false);
    }

    private static async Task<string> SafeBodyPreviewAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return body.Length <= 200 ? body : body[..200] + "...";
        }
        catch { return "<no body>"; }
    }

    private static IEnumerable<IReadOnlyList<uint>> Chunk(IReadOnlyCollection<uint> src, int size)
    {
        var buffer = new List<uint>(size);
        foreach (var id in src)
        {
            buffer.Add(id);
            if (buffer.Count == size)
            {
                yield return buffer;
                buffer = new List<uint>(size);
            }
        }
        if (buffer.Count > 0) yield return buffer;
    }

    public void Dispose()
    {
        _http.Dispose();
        _gate.Dispose();
    }
}
