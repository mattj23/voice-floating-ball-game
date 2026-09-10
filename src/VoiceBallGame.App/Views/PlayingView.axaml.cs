using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VoiceBallGame.App.Views;

public partial class PlayingView : UserControl
{
    public PlayingView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
