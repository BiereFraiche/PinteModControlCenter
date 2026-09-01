using System.Windows;
using System.Windows.Controls;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void SaveRconSecret_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var secret = RconPasswordBox.Password;
        RconPasswordBox.Clear();
        await viewModel.SaveRconSecretAsync(secret);
    }

    private async void InitializeFirstRcon_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var secret = RconPasswordBox.Password;
        RconPasswordBox.Clear();
        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Créer le premier mot de passe RCON avec la valeur saisie ?\n\nLe serveur doit être arrêté. Avec PinteMod, le Control Center prépare aussi les fichiers locaux requis par BOIII et le bridge. Aucun mot de passe déjà configuré n’est remplacé.",
            "Initialiser RCON",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await viewModel.InitializeFirstRconSecretAsync(secret);
    }

    private async void ReplaceRcon_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var secret = RconPasswordBox.Password;
        RconPasswordBox.Clear();
        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Remplacer le mot de passe RCON existant par la nouvelle valeur saisie ?\n\nLe serveur doit être arrêté. L’ancien mot de passe ne sera jamais affiché et le nouveau sera protégé pour ce compte Windows.",
            "Remplacer RCON",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await viewModel.ReplaceRconSecretAsync(secret);
    }
}
