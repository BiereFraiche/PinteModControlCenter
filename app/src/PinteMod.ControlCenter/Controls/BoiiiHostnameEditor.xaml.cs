using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Controls;

public partial class BoiiiHostnameEditor : UserControl
{
    public static readonly DependencyProperty EncodedTextProperty = DependencyProperty.Register(
        nameof(EncodedText),
        typeof(string),
        typeof(BoiiiHostnameEditor),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnEncodedTextChanged));

    private bool _synchronizing;

    public BoiiiHostnameEditor()
    {
        InitializeComponent();
        RefreshPreview();
    }

    public string EncodedText
    {
        get => (string)GetValue(EncodedTextProperty);
        set => SetValue(EncodedTextProperty, value ?? string.Empty);
    }

    private static void OnEncodedTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var editor = (BoiiiHostnameEditor)sender;
        var value = args.NewValue as string ?? string.Empty;
        if (!string.Equals(editor.EncodedTextBox.Text, value, StringComparison.Ordinal))
        {
            editor._synchronizing = true;
            editor.EncodedTextBox.Text = value;
            editor.EncodedTextBox.CaretIndex = value.Length;
            editor._synchronizing = false;
        }

        editor.RefreshPreview();
    }

    private void EncodedTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_synchronizing)
        {
            return;
        }

        SetCurrentValue(EncodedTextProperty, EncodedTextBox.Text);
        RefreshPreview();
    }

    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } || tag.Length != 1)
        {
            return;
        }

        var result = BoiiiColorText.ApplyColor(
            EncodedTextBox.Text,
            EncodedTextBox.SelectionStart,
            EncodedTextBox.SelectionLength,
            tag[0],
            EncodedTextBox.MaxLength);
        if (!result.Applied)
        {
            return;
        }

        EncodedTextBox.Text = result.Text;
        EncodedTextBox.Select(result.SelectionStart, result.SelectionLength);
        EncodedTextBox.Focus();
    }

    private void RefreshPreview()
    {
        PreviewTextBlock.Inlines.Clear();
        var segments = BoiiiColorText.Parse(EncodedTextBox.Text);
        if (segments.Count == 0)
        {
            PreviewTextBlock.Inlines.Add(new Run("Votre nom serveur")
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xD1, 0xD8, 0xE0)),
                FontStyle = FontStyles.Italic
            });
            return;
        }

        foreach (var segment in segments)
        {
            PreviewTextBlock.Inlines.Add(new Run(segment.Text)
            {
                Foreground = BrushFor(segment.ColorCode)
            });
        }
    }

    private static Brush BrushFor(char code) => code switch
    {
        '0' => new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)),
        '1' => new SolidColorBrush(Color.FromRgb(0xFF, 0x4B, 0x4B)),
        '2' => new SolidColorBrush(Color.FromRgb(0x4D, 0xDB, 0x7D)),
        '3' => new SolidColorBrush(Color.FromRgb(0xFF, 0xD4, 0x47)),
        '4' => new SolidColorBrush(Color.FromRgb(0x4E, 0xA1, 0xFF)),
        '5' => new SolidColorBrush(Color.FromRgb(0x40, 0xE0, 0xE0)),
        '6' => new SolidColorBrush(Color.FromRgb(0xFF, 0x6A, 0xD5)),
        '8' => new SolidColorBrush(Color.FromRgb(0x9A, 0xA7, 0xB5)),
        '9' => new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x43)),
        _ => new SolidColorBrush(Color.FromRgb(0xF4, 0xF7, 0xFB))
    };
}
