using VANAS.Mobile.Pages;
using VANAS.Mobile.Services;

namespace VANAS.Mobile;

public partial class AppShell : Shell
{
	public AppShell(ApiService api)
	{
		InitializeComponent();
		Routing.RegisterRoute("eventdetail", typeof(EventDetailPage));
		Routing.RegisterRoute("chat", typeof(ChatPage));

		// The scanner tab is for staff only; it follows whoever is signed in.
		api.SessionChanged += () => MainThread.BeginInvokeOnMainThread(() => ScanTab.IsVisible = api.IsSignedIn && api.IsStaff);
	}
}
