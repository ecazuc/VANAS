using VANAS.Mobile.Services;

namespace VANAS.Mobile.Pages;

public partial class EventsPage : ContentPage
{
    private readonly ApiService _api;

    public EventsPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
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
        var events = await _api.GetEventsAsync();
        if (!events.Ok || events.Value == null)
        {
            if (!_api.IsSignedIn) await Shell.Current.GoToAsync("//login");
            ErrorLabel.Text = events.Error;
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        var mine = (await _api.GetMyTicketsAsync()).Value ?? new();
        var registered = mine.Select(t => t.EventId).ToHashSet();

        ListBox.Children.Clear();
        EmptyLabel.IsVisible = events.Value.Count == 0;

        foreach (var e in events.Value)
        {
            var left = e.Capacity - e.RegisteredCount;
            var status = registered.Contains(e.Id) ? "✔ INSCRIT" : left > 0 ? $"{left} place(s) restante(s)" : "COMPLET";
            var statusColor = registered.Contains(e.Id) ? "#3DF2A5" : left > 0 ? "#FCEE0A" : "#FF4D5E";
            var price = e.PriceCents == 0 ? "Gratuit" : $"{e.PriceCents / 100m:0.00} €";

            var card = new Border
            {
                BackgroundColor = Color.FromArgb("#23262D"),
                Stroke = Color.FromArgb("#2E323B"),
                StrokeThickness = 1,
                Padding = 16,
                Content = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label { Text = $"{e.StartsAt.ToLocalTime():dd/MM/yyyy HH:mm}" + (e.GameName.Length > 0 ? $"  ·  {e.GameName}" : ""), FontSize = 12, TextColor = Color.FromArgb("#7C828F") },
                        new Label { Text = e.Title, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#C8CCD4") },
                        new Label { Text = $"{status}  ·  {price}", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(statusColor) }
                    }
                }
            };

            var id = e.Id;
            card.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(async () => await Shell.Current.GoToAsync($"eventdetail?id={id}"))
            });
            ListBox.Children.Add(card);
        }
    }
}
