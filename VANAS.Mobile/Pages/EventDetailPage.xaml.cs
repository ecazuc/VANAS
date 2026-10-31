using QRCoder;
using VANAS.Mobile.Services;
using VANAS.Shared.Content;
using VANAS.Shared.Contracts;

namespace VANAS.Mobile.Pages;

[QueryProperty(nameof(EventId), "id")]
public partial class EventDetailPage : ContentPage
{
    private readonly ApiService _api;
    private EventDetail? _event;
    private TicketInfo? _ticket;

    public EventDetailPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    public string EventId { get; set; } = string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!int.TryParse(EventId, out var id)) return;

        var result = await _api.GetEventAsync(id);
        if (!result.Ok || result.Value == null)
        {
            ShowError(result.Error);
            return;
        }

        _event = result.Value;
        _ticket = (await _api.GetMyTicketsAsync()).Value?.FirstOrDefault(t => t.EventId == id);

        var e = _event;
        var left = e.Capacity - e.RegisteredCount;
        var price = e.PriceCents == 0 ? "Gratuit" : $"{e.PriceCents / 100m:0.00} €";
        WhenLabel.Text = $"{e.StartsAt.ToLocalTime():dd/MM/yyyy HH:mm}" + (e.GameName.Length > 0 ? $"  ·  {e.GameName}" : "");
        TitleLabel.Text = e.Title;
        InfoLabel.Text = $"{(left > 0 ? $"{left} place(s) restante(s) sur {e.Capacity}" : "COMPLET")}  ·  {price}";
        DescriptionLabel.Text = MarkdownRenderer.ToHtml(e.DescriptionMarkdown);
        ShowError(null);
        RenderState();
    }

    private void RenderState()
    {
        var started = _event != null && _event.StartsAt < DateTime.UtcNow;
        var full = _event != null && _event.RegisteredCount >= _event.Capacity;

        TicketBox.IsVisible = _ticket != null;
        if (_ticket != null)
        {
            CodeLabel.Text = _ticket.TicketCode;
            TicketHeader.Text = _ticket.CheckedIn ? "ENTRÉE VALIDÉE" : "VOTRE BILLET";
            QrImage.Source = QrFor(_ticket.TicketCode);
        }

        if (_ticket != null)
        {
            ActionButton.IsVisible = !_ticket.CheckedIn;
            ActionButton.Text = "ME DÉSINSCRIRE";
            ActionButton.BackgroundColor = Colors.Transparent;
            ActionButton.TextColor = Color.FromArgb("#FF4D5E");
            ActionButton.BorderColor = Color.FromArgb("#FF4D5E");
            ActionButton.BorderWidth = 1;
        }
        else
        {
            ActionButton.IsVisible = !full && !started;
            ActionButton.Text = "JE M'INSCRIS";
            ActionButton.BackgroundColor = Color.FromArgb("#FCEE0A");
            ActionButton.TextColor = Color.FromArgb("#16181C");
            ActionButton.BorderWidth = 0;
        }
    }

    /// <summary>The QR code carries the ticket code; staff scan it at the door.</summary>
    private static ImageSource QrFor(string ticketCode)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(TicketQr.Encode(ticketCode), QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(12);
        return ImageSource.FromStream(() => new MemoryStream(png));
    }

    private async void OnAction(object? sender, EventArgs e)
    {
        if (_event == null) return;
        Busy.IsVisible = Busy.IsRunning = true;
        ActionButton.IsEnabled = false;
        try
        {
            if (_ticket == null)
            {
                var result = await _api.RegisterForEventAsync(_event.Id);
                if (!result.Ok)
                {
                    ShowError(result.Error);
                    return;
                }
            }
            else
            {
                var result = await _api.UnregisterFromEventAsync(_event.Id);
                if (!result.Ok)
                {
                    ShowError(result.Error);
                    return;
                }
            }

            await LoadAsync();
        }
        finally
        {
            Busy.IsVisible = Busy.IsRunning = false;
            ActionButton.IsEnabled = true;
        }
    }

    private async void OnBack(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    private void ShowError(string? message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = message != null;
    }
}
