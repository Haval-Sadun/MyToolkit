using System.Text.Json.Serialization;

namespace MyToolkit.Services.Net;

/// <summary>Generic API response carrying a status string.</summary>
public record StatusResponse(string Status);

/// <summary>Generic API response carrying a created resource's id.</summary>
public record CreateResponse(string Id);

/// <summary>Cursor-based paginated response.</summary>
public class PagedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextCursor { get; set; }
}
