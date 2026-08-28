using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PinteMod.ControlCenter.Views;

public partial class PinteModMessageBoxWindow : Window
{
    private MessageBoxResult _result = MessageBoxResult.None;
    private readonly MessageBoxResult _defaultResult;

    public PinteModMessageBoxWindow(string message, string title, MessageBoxButton buttons, MessageBoxImage image, MessageBoxResult defaultResult)
    {
        InitializeComponent();
        TitleText.Text = string.IsNullOrWhiteSpace(title) ? "PinteMod Control Center" : title;
        MessageText.Text = message ?? string.Empty;
        _defaultResult = defaultResult == MessageBoxResult.None ? DefaultFor(buttons) : defaultResult;
        ConfigureIcon(image);
        ConfigureButtons(buttons);
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public MessageBoxResult Result => _result == MessageBoxResult.None ? _defaultResult : _result;

    private void ConfigureIcon(MessageBoxImage image)
    {
        var (glyph, brushKey, softKey) = image switch
        {
            MessageBoxImage.Error => ("!", "DangerBrush", "DangerSoftBrush"),
            MessageBoxImage.Warning => ("!", "WarningBrush", "WarningSoftBrush"),
            MessageBoxImage.Question => ("?", "AccentBrightBrush", "AccentSoftBrush"),
            _ => ("i", "SuccessBrush", "SuccessSoftBrush")
        };
        IconText.Text = glyph;
        IconText.Foreground = (Brush)FindResource(brushKey);
        IconBadge.Background = (Brush)FindResource(softKey);
        IconBadge.BorderBrush = (Brush)FindResource(brushKey);
    }

    private void ConfigureButtons(MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddButton("OK", MessageBoxResult.OK, "PrimaryButtonStyle");
                break;
            case MessageBoxButton.OKCancel:
                AddButton("ANNULER", MessageBoxResult.Cancel, "SecondaryButtonStyle");
                AddButton("OK", MessageBoxResult.OK, "PrimaryButtonStyle");
                break;
            case MessageBoxButton.YesNo:
                AddButton("NON", MessageBoxResult.No, "SecondaryButtonStyle");
                AddButton("OUI", MessageBoxResult.Yes, "PrimaryButtonStyle");
                break;
            case MessageBoxButton.YesNoCancel:
                AddButton("ANNULER", MessageBoxResult.Cancel, "SecondaryButtonStyle");
                AddButton("NON", MessageBoxResult.No, "SecondaryButtonStyle");
                AddButton("OUI", MessageBoxResult.Yes, "PrimaryButtonStyle");
                break;
        }
    }

    private void AddButton(string label, MessageBoxResult result, string styleKey)
    {
        var button = new System.Windows.Controls.Button
        {
            Content = label,
            MinWidth = 92,
            Margin = new Thickness(8, 0, 0, 0),
            Style = (Style)FindResource(styleKey),
            IsDefault = result == _defaultResult,
            IsCancel = result is MessageBoxResult.Cancel or MessageBoxResult.No
        };
        button.Click += (_, _) => { _result = result; DialogResult = true; };
        ButtonsPanel.Children.Add(button);
    }

    private static MessageBoxResult DefaultFor(MessageBoxButton buttons) => buttons switch
    {
        MessageBoxButton.OK => MessageBoxResult.OK,
        MessageBoxButton.OKCancel => MessageBoxResult.Cancel,
        MessageBoxButton.YesNo => MessageBoxResult.No,
        MessageBoxButton.YesNoCancel => MessageBoxResult.Cancel,
        _ => MessageBoxResult.None
    };

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => CloseWithDefault();

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) CloseWithDefault();
    }

    private void CloseWithDefault()
    {
        _result = _defaultResult;
        DialogResult = false;
    }
}
