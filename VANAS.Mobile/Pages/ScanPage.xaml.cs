using VANAS.Mobile.Services;
using VANAS.Shared.Contracts;
using VANAS.Shared.Models;
#if ANDROID
using ZXing.Net.Maui;
using ZXing.Net.Maui.Controls;
#endif

namespace VANAS.Mobile.Pages;

public partial class ScanPage : ContentPage
{
    private readonly ApiService _api;
    private string? _lastCode;
    private DateTime _lastScanAt = DateTime.MinValue;
    private bool _checking;

#if ANDROID
    private CameraBarcodeReaderView? _reader;
#endif

    public ScanPage(ApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
#if ANDROID
        await StartCameraAsync();
#else
        await Task.CompletedTask;
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
#if ANDROID
        if (_reader != null) _reader.IsDetecting = false;
#endif
    }

#if ANDROID
    private async Task StartCameraAsync()
    {
        var permission = await Permissions.RequestAsync<Permissions.Camera>();
        if (permission != PermissionStatus.Granted)
        {
            ShowResult("Autorisez la caméra pour scanner, ou saisissez le code à la main.", false);
            return;
        }

        if (_reader == null)
        {
            _reader = new CameraBarcodeReaderView
            {
                Options = new BarcodeReaderOptions { Formats = BarcodeFormats.TwoDimensional, AutoRotate = true, Multiple = false }
            };
            _reader.BarcodesDetected += OnBarcodesDetected;
            CameraHost.Content = _reader;
            CameraHost.IsVisible = true;
        }

        _reader.IsDetecting = true;
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var code = e.Results?.Select(r => TicketQr.TryParse(r.Value)).FirstOrDefault(c => c != null);
        if (code == null) return;

        // The camera reports the same QR code many times per second: ignore repeats for a few seconds.
        if (code == _lastCode && DateTime.UtcNow - _lastScanAt < TimeSpan.FromSeconds(4)) return;
        _lastCode = code;
        _lastScanAt = DateTime.UtcNow;
        MainThread.BeginInvokeOnMainThread(async () => await CheckInAsync(code));
    }
#endif

    private async void OnManual(object? sender, EventArgs e)
    {
        var text = (CodeEntry.Text ?? string.Empty).Trim();
        // Accept either the bare ticket code or the full text of the QR code.
        var code = TicketQr.TryParse(text) ?? (text.Length == 10 ? text.ToUpperInvariant() : null);
        if (code == null)
        {
            ShowResult("Code invalide : il faut les 10 caractères du billet.", false);
            return;
        }

        await CheckInAsync(code);
        CodeEntry.Text = string.Empty;
    }

    private async Task CheckInAsync(string code)
    {
        if (_checking) return;
        _checking = true;
        Busy.IsVisible = Busy.IsRunning = true;
        try
        {
            // A paid entry is collected at the door: show the amount and ask how it was paid before letting the player in.
            var lookup = await _api.LookupTicketAsync(code);
            if (!lookup.Ok || lookup.Value == null)
            {
                ShowResult("✘ " + (lookup.Error ?? "Billet refusé."), false);
                return;
            }

            PaymentMethod? method = null;
            if (!lookup.Value.CheckedIn && lookup.Value.AmountDueCents > 0)
            {
                var choice = await DisplayActionSheet(
                    $"À encaisser : {OfferText.Euros(lookup.Value.AmountDueCents)} ({lookup.Value.DisplayName})",
                    "Annuler", null, "Encaissé en espèces", "Encaissé par carte", "Encaissé autrement");
                method = choice switch
                {
                    "Encaissé en espèces" => PaymentMethod.Cash,
                    "Encaissé par carte" => PaymentMethod.Card,
                    "Encaissé autrement" => PaymentMethod.Other,
                    _ => null
                };
                if (method == null)
                {
                    ShowResult("Entrée non validée : paiement non encaissé.", false);
                    return;
                }
            }

            var result = await _api.CheckInAsync(code, method);
            if (result.Ok && result.Value != null)
            {
                var paid = result.Value.PaidCents > 0 ? $"\n{OfferText.Euros(result.Value.PaidCents)} encaissés" : string.Empty;
                ShowResult($"✔ ENTRÉE VALIDÉE\n{result.Value.DisplayName}\n{result.Value.EventTitle}{paid}", true);
            }
            else
                ShowResult("✘ " + (result.Error ?? "Billet refusé."), false);
        }
        finally
        {
            Busy.IsVisible = Busy.IsRunning = false;
            _checking = false;
        }
    }

    private void ShowResult(string message, bool ok)
    {
        ResultLabel.Text = message;
        ResultLabel.TextColor = Color.FromArgb(ok ? "#3DF2A5" : "#FF4D5E");
        ResultBox.Stroke = Color.FromArgb(ok ? "#3DF2A5" : "#FF4D5E");
        ResultBox.BackgroundColor = Color.FromArgb(ok ? "#12301F" : "#3A1D22");
        ResultBox.IsVisible = true;
    }
}
