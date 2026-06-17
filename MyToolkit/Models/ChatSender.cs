using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace MyToolkit.Models;

public partial class ChatSender : ObservableObject
{
    [ObservableProperty]
    [property: JsonPropertyName("id")]
    private string _id = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("username")]
    private string _username = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("first_name")]
    private string _firstName = string.Empty;

    [ObservableProperty]
    [property: JsonPropertyName("last_name")]
    private string _lastName = string.Empty;

    private string _profileImage = "profile_placeholder.jpg";

    [JsonPropertyName("profile_image")]
    public string ProfileImage
    {
        get => _profileImage;
        set => SetProperty(ref _profileImage, MediaUrl.NormalizeOrFallback(value, "profile_placeholder.jpg"));
    }

    [ObservableProperty]
    [property: JsonPropertyName("professional_title")]
    private string _professionalTitle = string.Empty;

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
