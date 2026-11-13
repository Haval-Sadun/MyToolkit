using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyToolkit.Models;

public partial class Notification : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("id")]
    public partial long Id { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("type")]
    public partial string Type { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("title")]
    public partial string Title { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("message")]
    public partial string Message { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("actor")]
    public partial ChatSender? Actor { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("target_type")]
    public partial string TargetType { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("target_id")]
    public partial string TargetId { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("extra")]
    public partial Dictionary<string, JsonElement>? Extra { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("icon")]
    public partial string Icon { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("is_read")]
    public partial bool IsRead { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("created_at")]
    public partial DateTime CreatedAt { get; set; }

    public Notification()
    {
        Type = string.Empty;
        Title = string.Empty;
        Message = string.Empty;
        TargetType = string.Empty;
        TargetId = string.Empty;
        Icon = string.Empty;
    }

    [JsonIgnore]
    public string TimeAgo => Services.Time.TimeAgo.Relative(CreatedAt);

    /// <summary>
    /// Builds a Notification from an FCM push data payload (all string values).
    /// Lets the push-tap path reuse the same navigation logic as the in-app list.
    /// Only routing fields are populated — the row itself is fetched from the API when the
    /// Notification Centre opens.
    /// </summary>
    public static Notification FromPushData(IDictionary<string, string?> data)
    {
        string? Get(string k) => data.TryGetValue(k, out var v) ? v : null;

        var extra = new Dictionary<string, JsonElement>();
        foreach (var key in new[] { "conversation_id", "user_id", "group_id", "post_id", "event_id" })
        {
            var val = Get(key);
            if (!string.IsNullOrEmpty(val))
                extra[key] = JsonSerializer.SerializeToElement(val);
        }

        return new Notification
        {
            Id = long.TryParse(Get("notification_id"), out var id) ? id : 0,
            Type = Get("type") ?? string.Empty,
            TargetType = Get("target_type") ?? string.Empty,
            TargetId = Get("target_id") ?? string.Empty,
            Extra = extra,
        };
    }

    /// <summary>
    /// Reads a string value from the free-form <c>extra</c> object. Numbers are returned as
    /// raw text. Returns null when the key is missing or null.
    /// </summary>
    public string? GetExtra(string key)
    {
        if (Extra is null || !Extra.TryGetValue(key, out var el))
            return null;

        return el.ValueKind switch
        {
            JsonValueKind.String    => el.GetString(),
            JsonValueKind.Null      => null,
            JsonValueKind.Undefined => null,
            _                       => el.GetRawText(),
        };
    }
}

public record UnreadCountResponse
{
    [JsonPropertyName("count")]
    public int Count { get; init; }
}
