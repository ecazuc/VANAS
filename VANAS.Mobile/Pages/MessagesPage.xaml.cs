using VANAS.Mobile.Services;
using VANAS.Shared.Contracts;

namespace VANAS.Mobile.Pages;

public partial class MessagesPage : ContentPage
{
    private readonly ApiService _api;
    private List<CatalogGameDto> _catalog = new();
    private HashSet<int> _myGames = new();

    public MessagesPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
        RulesLabel.Text = ChatRules.Summary;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        await LoadAsync();
        Refresher.IsRefreshing = false;
    }

    private async Task LoadAsync()
    {
        var status = await _api.GetChatStatusAsync();
        if (!status.Ok || status.Value == null)
        {
            if (!_api.IsSignedIn) await Shell.Current.GoToAsync("//login");
            ShowError(status.Error);
            return;
        }

        ShowError(null);
        var s = status.Value;
        if (s.ConsentPending)
        {
            RulesBox.IsVisible = MainBox.IsVisible = BannedLabel.IsVisible = false;
            ShowError("Accord parental requis : un parent ou responsable légal doit passer au comptoir du café avant que vous puissiez utiliser la messagerie.");
            return;
        }

        RulesBox.IsVisible = !s.RulesAccepted;
        BannedLabel.IsVisible = s.RulesAccepted && s.IsBanned;
        MainBox.IsVisible = s.RulesAccepted && !s.IsBanned;
        MutedLabel.IsVisible = s.MutedUntil != null;
        if (s.MutedUntil != null) MutedLabel.Text = $"Vous ne pouvez pas écrire jusqu'au {s.MutedUntil.Value.ToLocalTime():dd/MM à HH:mm}.";
        if (!MainBox.IsVisible) return;

        _catalog = (await _api.GetCatalogAsync()).Value ?? new();
        _myGames = ((await _api.GetMyGamesAsync()).Value ?? new()).ToHashSet();
        RenderGames();
        RenderRooms();
        await LoadPeopleAsync();
    }

    private async Task LoadPeopleAsync()
    {
        var players = (await _api.GetSuggestionsAsync()).Value ?? new();
        PlayersBox.Children.Clear();
        NoPlayersLabel.IsVisible = players.Count == 0;
        foreach (var p in players)
        {
            var userId = p.UserId;
            var name = p.DisplayName;
            PlayersBox.Children.Add(Card(p.DisplayName, "Joue à : " + string.Join(", ", p.SharedGames),
                () => OpenChatAsync($"chat?user={userId}&title={Uri.EscapeDataString(name)}")));
        }

        var conversations = (await _api.GetConversationsAsync()).Value ?? new();
        ConversationsBox.Children.Clear();
        NoConversationsLabel.IsVisible = conversations.Count == 0;
        foreach (var c in conversations)
        {
            var userId = c.UserId;
            var name = c.DisplayName;
            ConversationsBox.Children.Add(Card(c.DisplayName, c.LastText,
                () => OpenChatAsync($"chat?user={userId}&title={Uri.EscapeDataString(name)}")));
        }

        var blocks = (await _api.GetBlocksAsync()).Value ?? new();
        BlocksBox.Children.Clear();
        BlocksTitle.IsVisible = blocks.Count > 0;
        foreach (var b in blocks)
        {
            var id = b.UserId;
            var row = new HorizontalStackLayout { Spacing = 10 };
            row.Children.Add(new Label { Text = b.DisplayName, TextColor = Color.FromArgb("#C8CCD4"), VerticalOptions = LayoutOptions.Center });
            var unblock = new Button { Text = "Débloquer", BackgroundColor = Color.FromArgb("#2E323B"), TextColor = Color.FromArgb("#C8CCD4"), CornerRadius = 0, Padding = new Thickness(12, 0), HeightRequest = 34 };
            unblock.Clicked += async (_, _) =>
            {
                await _api.UnblockAsync(id);
                await LoadPeopleAsync();
            };
            row.Children.Add(unblock);
            BlocksBox.Children.Add(row);
        }
    }

    private void RenderGames()
    {
        GamesBox.Children.Clear();
        foreach (var game in _catalog)
        {
            var on = _myGames.Contains(game.Id);
            var chip = new Button
            {
                Text = (on ? "✔ " : "") + game.Title,
                BackgroundColor = on ? Color.FromArgb("#FCEE0A") : Color.FromArgb("#23262D"),
                TextColor = on ? Color.FromArgb("#16181C") : Color.FromArgb("#C8CCD4"),
                CornerRadius = 0,
                FontSize = 12,
                Padding = new Thickness(10, 0),
                HeightRequest = 34,
                Margin = new Thickness(0, 0, 6, 6)
            };
            var id = game.Id;
            chip.Clicked += async (_, _) => await ToggleGameAsync(id);
            GamesBox.Children.Add(chip);
        }
    }

    private async Task ToggleGameAsync(int gameId)
    {
        var next = new HashSet<int>(_myGames);
        if (!next.Remove(gameId)) next.Add(gameId);

        var result = await _api.SetMyGamesAsync(next);
        if (!result.Ok)
        {
            ShowError(result.Error);
            return;
        }

        _myGames = next;
        ShowError(null);
        RenderGames();
        RenderRooms();
        await LoadPeopleAsync();
    }

    private void RenderRooms()
    {
        RoomsBox.Children.Clear();
        var mine = _catalog.Where(g => _myGames.Contains(g.Id)).ToList();
        NoRoomsLabel.IsVisible = mine.Count == 0;
        foreach (var game in mine)
        {
            var id = game.Id;
            var title = game.Title;
            RoomsBox.Children.Add(Card("# " + game.Title, "Salon de discussion",
                () => OpenChatAsync($"chat?room={id}&title={Uri.EscapeDataString(title)}")));
        }
    }

    private static Border Card(string title, string subtitle, Func<Task> onTap)
    {
        var card = new Border
        {
            BackgroundColor = Color.FromArgb("#23262D"),
            Stroke = Color.FromArgb("#2E323B"),
            StrokeThickness = 1,
            Padding = 14,
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#C8CCD4") },
                    new Label { Text = subtitle, FontSize = 12, TextColor = Color.FromArgb("#7C828F"), LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1 }
                }
            }
        };
        card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await onTap()) });
        return card;
    }

    private static Task OpenChatAsync(string route) => Shell.Current.GoToAsync(route);

    private async void OnAcceptRules(object? sender, EventArgs e)
    {
        var result = await _api.AcceptChatRulesAsync();
        if (!result.Ok)
        {
            ShowError(result.Error);
            return;
        }

        await LoadAsync();
    }

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = message != null;
    }
}
