using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace MyToolkit.Models;

public partial class ChatSender : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("id")]
    public partial string Id { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("username")]
    public partial string Username { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("first_name")]
    public partial string FirstName { get; set; }

    [ObservableProperty]
    [property: JsonPropertyName("last_name")]
    public partial string LastName { get; set; }

    public ChatSender()
    {
        Id = string.Empty;
        Username = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        ProfessionalTitle = string.Empty;
    }

    private string _profileImage = "profile_placeholder.jpg";

    [JsonPropertyName("profile_image")]
    public string ProfileImage
    {
        get => _profileImage;
        set => SetProperty(ref _profileImage, MediaUrl.NormalizeOrFallback(value, "profile_placeholder.jpg"));
    }

    [ObservableProperty]
    [property: JsonPropertyName("professional_title")]
    public partial string ProfessionalTitle { get; set; }

    [JsonIgnore]
    public bool HasProfessionalTitle => !string.IsNullOrWhiteSpace(ProfessionalTitle);

    partial void OnProfessionalTitleChanged(string value) => OnPropertyChanged(nameof(HasProfessionalTitle));

    [JsonIgnore]
    public string DisplayName
    {
        get
        {
            var full = $"{FirstName} {LastName}".Trim();
            return full.Length > 0 ? full : Username;
        }
    }
}
