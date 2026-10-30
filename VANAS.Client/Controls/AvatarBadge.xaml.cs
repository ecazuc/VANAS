using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VANAS.Shared.Contracts;

namespace VANAS.Client.Controls
{
    /// <summary>Preset avatar: a coloured disc with the player's initial.</summary>
    public partial class AvatarBadge : UserControl
    {
        public AvatarBadge() => InitializeComponent();

        public static Brush BrushFor(string? key)
        {
            var hex = Avatars.Colors.TryGetValue(key ?? string.Empty, out var h) ? h : Avatars.Colors[Avatars.Default];
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }

        public void Set(string avatarKey, string name)
        {
            disc.Fill = BrushFor(avatarKey);
            initial.Text = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[0].ToString().ToUpperInvariant();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e) =>
            initial.FontSize = Math.Max(10, Math.Min(ActualWidth, ActualHeight) * 0.5);
    }
}
