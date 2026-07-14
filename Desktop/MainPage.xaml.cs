namespace Desktop;

public partial class MainPage : ContentPage
{
    private string? _loadedUrl;

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await EnsureServerLoadedAsync();
    }

    private async Task EnsureServerLoadedAsync()
    {
        var url = Preferences.Get(SettingsPage.PrefKeyServerUrl, string.Empty);

        if (string.IsNullOrWhiteSpace(url))
        {
            await OpenSettingsAsync();
            return;
        }

        if (url != _loadedUrl)
        {
            _loadedUrl = url;
            ServerWebView.Source = url;
        }
    }

    private async void OnReloadClicked(object? sender, EventArgs e)
    {
        var url = Preferences.Get(SettingsPage.PrefKeyServerUrl, string.Empty);
        if (string.IsNullOrWhiteSpace(url))
        {
            await OpenSettingsAsync();
            return;
        }

        _loadedUrl = url;
        ServerWebView.Source = url;
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        await OpenSettingsAsync();
    }

    private async Task OpenSettingsAsync()
    {
        await Navigation.PushModalAsync(new SettingsPage());
    }
}
