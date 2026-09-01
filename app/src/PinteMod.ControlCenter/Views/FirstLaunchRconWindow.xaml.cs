using System.Windows;

namespace PinteMod.ControlCenter.Views;

public partial class FirstLaunchRconWindow : Window
{
    private string? _secret;

    public FirstLaunchRconWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => RconPasswordBox.Focus();
    }

    public bool StartWithoutRcon { get; private set; }

    public string TakeSecret()
    {
        var secret = _secret ?? string.Empty;
        _secret = null;
        return secret;
    }

    private void StartWithoutRcon_Click(object sender, RoutedEventArgs e)
    {
        RconPasswordBox.Clear();
        ConfirmPasswordBox.Clear();
        StartWithoutRcon = true;
        DialogResult = true;
    }

    private void SaveAndStart_Click(object sender, RoutedEventArgs e)
    {
        var password = RconPasswordBox.Password;
        var confirmation = ConfirmPasswordBox.Password;
        RconPasswordBox.Clear();
        ConfirmPasswordBox.Clear();

        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            ValidationText.Text = "Les deux mots de passe ne correspondent pas.";
            return;
        }

        if (password.Length is < 8 or > 128 || password.Any(char.IsWhiteSpace) || password.Contains('"'))
        {
            ValidationText.Text = "Utilisez 8 à 128 caractères, sans espace ni guillemet.";
            return;
        }

        _secret = password;
        DialogResult = true;
    }
}
