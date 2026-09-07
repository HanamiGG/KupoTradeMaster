using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace KupoTradeMaster.Services;

/// TSM-style share strings for groups + formulas + watchlist bundles. Format: "KM1:<base64url(gzip(json))>"
/// so users can copy/paste a shareable payload into Discord/text and consumers can decode it locally.
public static class ShareCodec
{
    private const string Prefix = "KM1:";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    public static string EncodeGroup(string name, IEnumerable<uint> itemIds, string? formula)
    {
        var payload = new SharePayload
        {
            Kind = "group",
            Name = name,
            ItemIds = new List<uint>(itemIds),
            Formula = formula,
        };
        return Encode(payload);
    }

    public static string EncodeCustomSources(Dictionary<string, string> sources)
    {
        var payload = new SharePayload
        {
            Kind = "customSources",
            CustomSources = sources,
        };
        return Encode(payload);
    }

    public static string EncodeBundle(Dictionary<string, List<uint>> groups,
                                       Dictionary<string, string> groupFormulas,
                                       Dictionary<string, string> customSources,
                                       string buyFormula)
    {
        var payload = new SharePayload
        {
            Kind = "bundle",
            Groups = groups,
            GroupFormulas = groupFormulas,
            CustomSources = customSources,
            BuyFormula = buyFormula,
        };
        return Encode(payload);
    }

    public static bool TryDecode(string code, out SharePayload? payload, out string? error)
    {
        payload = null;
        error = null;
        if (string.IsNullOrWhiteSpace(code)) { error = "empty code"; return false; }

        var trimmed = code.Trim();
        if (!trimmed.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            error = $"not a KupoTradeMaster share code (missing '{Prefix}' prefix)";
            return false;
        }
        var b64 = trimmed[Prefix.Length..];
        try
        {
            var gz = Base64UrlDecode(b64);
            var json = Decompress(gz);
            payload = JsonSerializer.Deserialize<SharePayload>(json, JsonOpts);
            if (payload == null) { error = "empty payload"; return false; }
            return true;
        }
        catch (Exception ex) { error = $"decode failed: {ex.Message}"; return false; }
    }

    private static string Encode(SharePayload payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var gz = Compress(json);
        return Prefix + Base64UrlEncode(gz);
    }

    private static byte[] Compress(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        using var ms = new System.IO.MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            gz.Write(bytes, 0, bytes.Length);
        return ms.ToArray();
    }

    private static string Decompress(byte[] gz)
    {
        using var ms = new System.IO.MemoryStream(gz);
        using var gzs = new GZipStream(ms, CompressionMode.Decompress);
        using var reader = new System.IO.StreamReader(gzs, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] Base64UrlDecode(string s)
    {
        var v = s.Replace('-', '+').Replace('_', '/');
        switch (v.Length % 4) { case 2: v += "=="; break; case 3: v += "="; break; }
        return Convert.FromBase64String(v);
    }
}

public sealed class SharePayload
{
    public int V { get; set; } = 1;
    public string Kind { get; set; } = "";
    public string? Name { get; set; }
    public string? Formula { get; set; }
    public List<uint>? ItemIds { get; set; }
    public Dictionary<string, List<uint>>? Groups { get; set; }
    public Dictionary<string, string>? GroupFormulas { get; set; }
    public Dictionary<string, string>? CustomSources { get; set; }
    public string? BuyFormula { get; set; }
}
