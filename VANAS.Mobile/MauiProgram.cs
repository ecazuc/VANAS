using Microsoft.Extensions.Logging;
#if ANDROID
using ZXing.Net.Maui.Controls;
#endif

namespace VANAS.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
#if ANDROID
			.UseBarcodeReader()
#endif
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		builder.Services.AddSingleton<Services.ApiService>();
		builder.Services.AddSingleton<AppShell>();
		builder.Services.AddTransient<Pages.EventsPage>();
		builder.Services.AddTransient<Pages.EventDetailPage>();
		builder.Services.AddTransient<Pages.ScanPage>();
		builder.Services.AddTransient<Pages.LoginPage>();
		builder.Services.AddTransient<Pages.HomePage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
