namespace Desktop;

public partial class SettingsPage : ContentPage
{
    public const string PrefKeyServerUrl = "ServerUrl";

    public SettingsPage()
    {
        InitializeComponent();
        var current = Preferences.Get(PrefKeyServerUrl, string.Empty);
        ServerUrlEntry.Text = current;
        CancelButton.IsVisible = !string.IsNullOrWhiteSpace(current);
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        var text = ServerUrlEntry.Text?.Trim() ?? "";

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ErrorLabel.Text = "Enter a full URL starting with http:// or https://";
            ErrorLabel.IsVisible = true;
            return;
        }

        Preferences.Set(PrefKeyServerUrl, uri.ToString().TrimEnd('/'));
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
