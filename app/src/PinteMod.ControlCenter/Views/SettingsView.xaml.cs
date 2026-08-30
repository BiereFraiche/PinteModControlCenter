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
            "Créer le premier mot de passe RCON avec la valeur saisie ?\n\nLe serveur doit être arrêté. Le Control Center ne remplace jamais un mot de passe déjà présent dans le .cfg BOIII.",
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
}
