using System.Net.Http;
using System.Text.Json;
using MyToolkit.Services.Errors;

namespace MyToolkit.Services.Net;

/// <summary>
/// Turns a failed HTTP response into a rich <see cref="ApiException"/>. This is the
/// ONLY place that knows the backend error envelope shape — extracted from
/// <see cref="ApiService"/> so error parsing has a single home and a single reason to
/// change (the wire format).
/// </summary>
public sealed class ApiExceptionFactory
{
    /// <summary>
    /// Builds an <see cref="ApiException"/> from a failed response, parsing the backend
    /// error envelope (<c>{ "error": { message, code, severity, trace_id, stack_trace } }</c>)
    /// when present, falling back to a bare DRF <c>{ "detail": ... }</c> or the raw body.
    /// </summary>
    public async Task<ApiException> CreateAsync(
        HttpResponseMessage response, string method, string endpoint, CancellationToken cancellationToken = default)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string? message = null, code = null, traceId = null, stack = null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("error", out var err) &&
                err.ValueKind == JsonValueKind.Object)
            {
                if (err.TryGetProperty("message", out var m)) message = m.GetString();
                if (err.TryGetProperty("code", out var c)) code = c.GetString();
                if (err.TryGetProperty("trace_id", out var t)) traceId = t.GetString();
                if (err.TryGetProperty("stack_trace", out var s)) stack = s.GetString();
                // The envelope "message" is generic; "detail" carries the actionable text
                // (field errors or the non_field_errors string). Prefer it for the user message.
                if (err.TryGetProperty("detail", out var dd))
                {
                    var flat = FlattenDetail(dd);
                    if (!string.IsNullOrWhiteSpace(flat)) message = flat;
                }
            }
            else if (root.ValueKind == JsonValueKind.Object &&
                     root.TryGetProperty("detail", out var d))
            {
                message = FlattenDetail(d);
            }
            // Flat envelope: { "message", "error_code"|"code", "status_code", "details" }.
            // Used by backends that don't wrap errors under an "error" object (e.g.
            // KurdishConnect / Django Ninja). Checked last so the richer shapes win.
            else if (root.ValueKind == JsonValueKind.Object &&
                     root.TryGetProperty("message", out var flatMsg))
            {
                message = flatMsg.GetString();
                if (root.TryGetProperty("error_code", out var ec)) code = ec.GetString();
                else if (root.TryGetProperty("code", out var c2)) code = c2.GetString();
            }
        }
        catch { /* non-JSON body (e.g. an HTML 500 page) — keep the raw body */ }

        return new ApiException(
            response.StatusCode, method, endpoint, body,
            message, code, traceId, stack);
    }

    /// <summary>
    /// Flattens a DRF "detail" value into a readable line. Handles a plain string,
    /// a list of strings (non_field_errors), and a {field: [msgs]} validation map.
    /// </summary>
    private static string? FlattenDetail(JsonElement detail)
    {
        switch (detail.ValueKind)
        {
            case JsonValueKind.String:
                return detail.GetString();
            case JsonValueKind.Array:
                return string.Join(" ", detail.EnumerateArray()
                    .Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString()));
            case JsonValueKind.Object:
                var parts = new List<string>();
                foreach (var prop in detail.EnumerateObject())
                {
                    var val = FlattenDetail(prop.Value);
                    if (!string.IsNullOrWhiteSpace(val))
                        parts.Add(prop.Name == "non_field_errors" ? val : $"{prop.Name}: {val}");
                }
                return parts.Count > 0 ? string.Join(" ", parts) : null;
            default:
                return null;
        }
    }
}
