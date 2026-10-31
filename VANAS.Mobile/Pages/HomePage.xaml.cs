using VANAS.Mobile.Services;

namespace VANAS.Mobile.Pages;

public partial class HomePage : ContentPage
{
    private readonly ApiService _api;

    public HomePage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var result = await _api.GetProfileAsync();
        if (!result.Ok || result.Value == null)
        {
            // An expired session already cleared the token: go back to the login page.
            if (!_api.IsSignedIn)
            {
                await Shell.Current.GoToAsync("//login");
                return;
            }

            ErrorLabel.Text = result.Error;
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        var p = result.Value;
        NameLabel.Text = p.DisplayName;
        AccountLabel.Text = $"Compte {p.Username}";
        TimeLabel.Text = Format(p.MinutesLeft);
        PlayedLabel.Text = Format(p.TotalMinutesPlayed);
        SessionsLabel.Text = $"{p.SessionCount} session(s)";
    }

    private async void OnRefresh(object? sender, EventArgs e) => await LoadAsync();

    private async void OnSignOut(object? sender, EventArgs e)
    {
        _api.SignOut();
        await Shell.Current.GoToAsync("//login");
    }

    private static string Format(double minutes)
    {
        var total = (int)Math.Round(Math.Max(0, minutes));
        return total >= 60 ? $"{total / 60} h {total % 60:00} min" : $"{total} min";
    }
}
