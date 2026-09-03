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

    private async void UpdateServerPort_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Appliquer ce port au fichier serveur déclaré par Server.bat ?\n\nLe serveur doit être arrêté. Le Control Center modifie uniquement la directive net_port ; aucun mot de passe RCON n’est lu ni affiché.",
            "Modifier le port BOIII",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await viewModel.UpdateServerPortAsync();
    }

    private void RemovePublicChatTip_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel && sender is FrameworkElement { Tag: PublicChatTipItemViewModel tip })
        {
            viewModel.RemovePublicChatTip(tip);
        }
    }

    private async void SavePublicChatTips_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Enregistrer les messages automatiques dans ce serveur ?\n\nLe serveur doit être arrêté. Le Control Center modifie uniquement le bloc dédié des réglages PinteMod. Si le module officiel de messages est ancien, il sera mis à jour avec une sauvegarde locale.",
            "Enregistrer les messages automatiques",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            await viewModel.SavePublicChatTipsAsync();
        }
    }

    private async void SaveAntiAfk_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Enregistrer la protection anti-AFK dans ce serveur ?\n\nLe serveur doit être arrêté. Les joueurs AFK sont placés spectateur sans mort ni perte d’équipement et peuvent revenir avec .retour.",
            "Enregistrer la protection anti-AFK",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
        {
            await viewModel.SaveAntiAfkAsync();
        }
    }
}
