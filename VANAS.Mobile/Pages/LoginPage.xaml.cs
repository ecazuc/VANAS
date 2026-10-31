using VANAS.Mobile.Services;

namespace VANAS.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    private readonly ApiService _api;
    private bool _creating;

    public LoginPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
        ServerEntry.Text = _api.ServerUrl;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (await _api.TryRestoreAsync()) await Shell.Current.GoToAsync("//home");
    }

    private void OnToggleMode(object? sender, EventArgs e)
    {
        _creating = !_creating;
        TitleLabel.Text = _creating ? "Créer un compte" : "Connexion";
        SubmitButton.Text = _creating ? "CRÉER MON COMPTE" : "ME CONNECTER";
        ModeButton.Text = _creating ? "Déjà un compte ? Se connecter" : "Pas encore de compte ? En créer un";
        ConfirmBox.IsVisible = _creating;
        ShowError(null);
    }

    private void OnToggleServer(object? sender, EventArgs e) => ServerBox.IsVisible = !ServerBox.IsVisible;

    private void OnSaveServer(object? sender, EventArgs e)
    {
        if (!ApiService.IsValidServer(ServerEntry.Text ?? string.Empty))
        {
            ShowError("Adresse du serveur invalide.");
            return;
        }

        _api.ServerUrl = ServerEntry.Text!;
        ServerEntry.Text = _api.ServerUrl;
        ShowError(null);
    }

    private async void OnSubmit(object? sender, EventArgs e)
    {
        ShowError(null);
        var username = (UsernameEntry.Text ?? string.Empty).Trim();
        var password = PasswordEntry.Text ?? string.Empty;

        if (username.Length == 0 || password.Length == 0)
        {
            ShowError("Renseignez l'identifiant et le mot de passe.");
            return;
        }

        if (_creating && password != ConfirmEntry.Text)
        {
            ShowError("Les deux mots de passe ne correspondent pas.");
            return;
        }

        SetBusy(true);
        try
        {
            if (_creating)
            {
                var created = await _api.RegisterAsync(username, password);
                if (!created.Ok)
                {
                    ShowError(created.Error);
                    return;
                }
            }

            var login = await _api.LoginAsync(username, password);
            if (!login.Ok)
            {
                ShowError(login.Error);
                return;
            }

            PasswordEntry.Text = ConfirmEntry.Text = string.Empty;
            await Shell.Current.GoToAsync("//home");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = Busy.IsRunning = busy;
        SubmitButton.IsEnabled = !busy;
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = message != null;
    }
}
