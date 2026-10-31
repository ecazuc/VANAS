using Microsoft.AspNetCore.SignalR.Client;
using VANAS.Mobile.Services;
using VANAS.Shared.Contracts;

namespace VANAS.Mobile.Pages;

/// <summary>A game's room (?room=gameId) or a private conversation (?user=userId).</summary>
[QueryProperty(nameof(Room), "room")]
[QueryProperty(nameof(User), "user")]
[QueryProperty(nameof(Title), "title")]
public partial class ChatPage : ContentPage
{
    private readonly ApiService _api;
    private readonly HashSet<int> _shown = new();
    private HashSet<int> _blocked = new();
    private HubConnection? _hub;
    private string? _myName;
    private int _roomId, _userId;

    public ChatPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    public string Room { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    private bool IsRoom => _roomId > 0;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        int.TryParse(Room, out _roomId);
        int.TryParse(User, out _userId);
        TitleLabel.Text = (IsRoom ? "# " : "") + Uri.UnescapeDataString(Title);

        _myName = (await _api.GetProfileAsync()).Value?.DisplayName;
        _blocked = ((await _api.GetBlocksAsync()).Value ?? new()).Select(b => b.UserId).ToHashSet();

        MessagesBox.Children.Clear();
        _shown.Clear();
        await LoadMessagesAsync();
        await ConnectAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        var hub = _hub;
        _hub = null;
        if (hub != null)
        {
            try { await hub.DisposeAsync(); } catch (Exception) { /* leaving anyway */ }
        }
    }

    // ----- Loading and live updates -----

    private async Task LoadMessagesAsync()
    {
        var result = IsRoom ? await _api.GetRoomMessagesAsync(_roomId) : await _api.GetDirectMessagesAsync(_userId);
        if (!result.Ok || result.Value == null)
        {
            ShowStatus(result.Error);
            return;
        }

        ShowStatus(null);
        foreach (var m in result.Value) AddBubble(m);
        await ScrollToEndAsync();
    }

    private async Task ConnectAsync()
    {
        if (_api.Token == null) return;
        try
        {
            _hub = new HubConnectionBuilder()
                .WithUrl(_api.ServerUrl + "/hubs/chat", o => o.AccessTokenProvider = () => Task.FromResult(_api.Token))
                .WithAutomaticReconnect()
                .Build();
            _hub.On<ChatMessageDto>("Message", m => MainThread.BeginInvokeOnMainThread(async () =>
            {
                if (!Belongs(m) || _blocked.Contains(m.SenderId)) return;
                AddBubble(m);
                await ScrollToEndAsync();
            }));
            // After a network drop the room membership is lost on the server: join again and catch up.
            _hub.Reconnected += async _ =>
            {
                if (IsRoom) await _hub!.InvokeAsync<bool>("JoinRoom", _roomId);
                MainThread.BeginInvokeOnMainThread(async () => await LoadMessagesAsync());
            };

            await _hub.StartAsync();
            if (IsRoom) await _hub.InvokeAsync<bool>("JoinRoom", _roomId);
        }
        catch (Exception)
        {
            ShowStatus("Direct indisponible : les nouveaux messages apparaîtront quand vous rouvrirez la discussion.");
        }
    }

    private bool Belongs(ChatMessageDto m) =>
        IsRoom ? m.GameId == _roomId : m.GameId == null && (m.SenderId == _userId || m.RecipientId == _userId);

    // ----- Display -----

    private void AddBubble(ChatMessageDto m)
    {
        if (!_shown.Add(m.Id)) return;

        var mine = string.Equals(m.SenderName, _myName, StringComparison.OrdinalIgnoreCase);
        var stack = new VerticalStackLayout { Spacing = 2 };
        if (!mine) stack.Children.Add(new Label { Text = m.SenderName, FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#FCEE0A") });
        stack.Children.Add(new Label { Text = m.Text, FontSize = 14, TextColor = Color.FromArgb(mine ? "#16181C" : "#C8CCD4") });
        stack.Children.Add(new Label { Text = m.CreatedAt.ToLocalTime().ToString("HH:mm"), FontSize = 10, TextColor = Color.FromArgb(mine ? "#4A4D00" : "#7C828F"), HorizontalOptions = LayoutOptions.End });

        var bubble = new Border
        {
            BackgroundColor = Color.FromArgb(mine ? "#FCEE0A" : "#23262D"),
            Stroke = Color.FromArgb(mine ? "#FCEE0A" : "#2E323B"),
            StrokeThickness = 1,
            Padding = new Thickness(12, 8),
            HorizontalOptions = mine ? LayoutOptions.End : LayoutOptions.Start,
            MaximumWidthRequest = 320,
            Content = stack
        };

        // Tap someone else's message to report it or block its author.
        if (!mine) bubble.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await OnBubbleTappedAsync(m)) });
        MessagesBox.Children.Add(bubble);
    }

    private async Task OnBubbleTappedAsync(ChatMessageDto m)
    {
        var choice = await DisplayActionSheet(m.SenderName, "Annuler", null, "Signaler ce message", $"Bloquer {m.SenderName}");
        if (choice == "Signaler ce message")
        {
            var reason = await DisplayPromptAsync("Signaler", "Pourquoi signalez-vous ce message ?", "Envoyer", "Annuler", "Insultes, harcèlement...", 300);
            if (string.IsNullOrWhiteSpace(reason)) return;
            var result = await _api.ReportAsync(m.Id, reason);
            await DisplayAlert(result.Ok ? "Merci" : "Erreur", result.Ok ? "Le signalement a été transmis à l'équipe du café." : result.Error, "OK");
        }
        else if (choice == $"Bloquer {m.SenderName}")
        {
            var result = await _api.BlockAsync(m.SenderId);
            if (!result.Ok)
            {
                await DisplayAlert("Erreur", result.Error, "OK");
                return;
            }

            _blocked.Add(m.SenderId);
            MessagesBox.Children.Clear();
            _shown.Clear();
            await LoadMessagesAsync();
            if (!IsRoom && m.SenderId == _userId) await Shell.Current.GoToAsync("..");
        }
    }

    private async Task ScrollToEndAsync()
    {
        await Task.Delay(60);
        if (MessagesBox.Children.Count > 0) await Scroller.ScrollToAsync(MessagesBox, ScrollToPosition.End, false);
    }

    private async void OnSend(object? sender, EventArgs e)
    {
        var text = (InputEntry.Text ?? string.Empty).Trim();
        if (text.Length == 0) return;

        var result = IsRoom ? await _api.SendRoomMessageAsync(_roomId, text) : await _api.SendDirectMessageAsync(_userId, text);
        if (!result.Ok || result.Value == null)
        {
            ShowStatus(result.Error);
            return;
        }

        ShowStatus(null);
        InputEntry.Text = string.Empty;
        AddBubble(result.Value);
        await ScrollToEndAsync();
        InputEntry.Focus();
    }

    private async void OnBack(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private void ShowStatus(string? message)
    {
        StatusLabel.Text = message;
        StatusLabel.IsVisible = message != null;
    }
}
