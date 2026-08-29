using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using PinteMod.ControlCenter.ViewModels;

namespace PinteMod.ControlCenter;

public partial class ServerManagerWindow : Window
{
    private readonly CancellationTokenSource _lifetime = new();
    private bool _selectionAnalysisRunning;
    private Task? _selectionAnalysisTask;
    private bool _openControlCenterRequested;
    private readonly bool _companionWindow;

    public ServerManagerWindow(ServerManagerViewModel viewModel, bool companionWindow = false)
    {
        InitializeComponent();
        _companionWindow = companionWindow;
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    public ServerManagerViewModel ViewModel { get; }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.CheckGitHubUpdateAsync(_lifetime.Token);
    }

    private async void CheckGitHub_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.CheckGitHubUpdateAsync(_lifetime.Token);
    }

    private async void KeepManagerOpen_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(() => ViewModel.SetKeepManagerOpenAfterControlCenterAsync(
            KeepManagerOpenCheck.IsChecked == true,
            _lifetime.Token));
    }

    private async void SimpleMode_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ViewModel.SetAdvancedModeAsync(false, _lifetime.Token));

    private async void AdvancedMode_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ViewModel.SetAdvancedModeAsync(true, _lifetime.Token));

    private async void ConfigureLocal_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await ViewModel.PrepareOnboardingProfileAsync(remote: false, cancellationToken: _lifetime.Token);
            if (!TryChooseServerRoot("Choisir le dossier du serveur BOIII sur ce PC"))
            {
                return;
            }

            await ViewModel.AnalyzeSelectedAsync(_lifetime.Token);
            await ViewModel.SaveSelectedAsync(_lifetime.Token);
        });
    }

    private async void ConfigureNetwork_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            await ViewModel.PrepareOnboardingProfileAsync(remote: true, cancellationToken: _lifetime.Token);
            if (!TryChooseServerRoot("Choisir le dossier partagé du serveur BOIII"))
            {
                return;
            }

            await ViewModel.AnalyzeSelectedAsync(_lifetime.Token);
            await ViewModel.SaveSelectedAsync(_lifetime.Token);
            var profile = ViewModel.SelectedProfile;
            if (profile is not null &&
                (!PinteMod.ControlCenter.Core.Security.RconEndpointValidator.IsLocalOrPrivateAddress(profile.RconAddress) ||
                 PinteMod.ControlCenter.Core.Security.RconEndpointValidator.IsLoopbackAddress(profile.RconAddress)))
            {
                PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                    "Serveur réseau enregistré. Pour envoyer des commandes, indiquez aussi l’adresse IP locale du PC serveur dans le champ affiché sous le dossier. Le pairing et la lecture des fichiers peuvent être préparés avant cela.",
                    "Serveur réseau",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        });
    }

    private async void RecommendedAction_Click(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel.SelectedProfile;
        if (profile is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(profile.ServerRoot) || profile.Analysis is { BoiiiRootDetected: false })
        {
            if (TryChooseServerRoot(profile.IsUncProfile
                    ? "Choisir le dossier partagé du serveur BOIII"
                    : "Choisir la racine du serveur BOIII"))
            {
                await RunAsync(() => ViewModel.AnalyzeSelectedAsync(_lifetime.Token));
            }
            return;
        }

        if (profile.Analysis is null)
        {
            await RunAsync(() => ViewModel.AnalyzeSelectedAsync(_lifetime.Token));
            return;
        }

        if (profile.CanInstallPinteModSafely)
        {
            await ConfirmAndPreparePinteModAsync();
            return;
        }

        if (profile.HasThirdPartyScripts && !profile.IsReadyForControlCenter)
        {
            await RunAsync(() => ViewModel.SaveSelectedAsync(_lifetime.Token));
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                "Le serveur a été enregistré sans modifier ses scripts. L’audit 4B catalogue les capacités observables, mais seules les fonctions prouvées restent actives. Les commandes découvertes dans les GSC ne sont jamais exécutées automatiquement.",
                "Compatibilité adaptative",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            await OpenSelectedControlCenterAsync();
            return;
        }

        await OpenSelectedControlCenterAsync();
    }

    private async void Profiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_selectionAnalysisRunning || ViewModel.SelectedProfile is null || string.IsNullOrWhiteSpace(ViewModel.SelectedProfile.ServerRoot))
        {
            return;
        }

        _selectionAnalysisRunning = true;
        try
        {
            _selectionAnalysisTask = ViewModel.AnalyzeSelectedAsync(_lifetime.Token);
            await _selectionAnalysisTask;
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // Closing the modal Manager cancels its lifetime. Never let an async
            // SelectionChanged continuation terminate the WPF process.
        }
        catch (Exception exception)
        {
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                exception is InvalidOperationException or ArgumentException
                    ? exception.Message
                    : "L'analyse automatique du profil n'a pas pu être terminée.",
                "PinteMod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            _selectionAnalysisTask = null;
            _selectionAnalysisRunning = false;
        }
    }

    private async void AddProfile_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ViewModel.AddProfileAsync(_lifetime.Token));

    private async void RemoveProfile_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedProfile is null)
        {
            return;
        }

        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            $"Retirer « {ViewModel.SelectedProfile.DisplayName} » du gestionnaire ?\n\nAucun fichier du serveur et aucun secret local ne sera supprimé.",
            "Retirer un profil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer == MessageBoxResult.Yes)
        {
            await RunAsync(() => ViewModel.RemoveSelectedAsync(_lifetime.Token));
        }
    }

    private void BrowseRoot_Click(object sender, RoutedEventArgs e) =>
        TryChooseServerRoot("Sélectionner la racine du serveur BOIII");

    private bool TryChooseServerRoot(string title)
    {
        if (ViewModel.SelectedProfile is null)
        {
            return false;
        }

        var dialog = new OpenFolderDialog
        {
            Title = title,
            Multiselect = false
        };
        if (!string.IsNullOrWhiteSpace(ViewModel.SelectedProfile.ServerRoot) && Directory.Exists(ViewModel.SelectedProfile.ServerRoot))
        {
            dialog.InitialDirectory = ViewModel.SelectedProfile.ServerRoot;
        }

        if (dialog.ShowDialog(this) != true)
        {
            return false;
        }

        ViewModel.SelectedProfile.ServerRoot = dialog.FolderName;
        return true;
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ViewModel.AnalyzeSelectedAsync(_lifetime.Token));

    private async void Save_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ViewModel.SaveSelectedAsync(_lifetime.Token));

    private async void AnalyzeStorage_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ViewModel.AnalyzeStorageSelectedAsync(_lifetime.Token));

    private async void RepairGeoIp_Click(object sender, RoutedEventArgs e)
    {
        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Réinitialiser uniquement les statistiques pays GeoIP de ce serveur ?\n\ncountries.json, countries.json.tmp* et countries_summary.txt seront remis à zéro. Les ranks, records, langues, bans, profils joueurs et secrets RCON ne seront pas modifiés.\n\nArrêtez d’abord BOIII afin que le GeoIP Bridge ne soit plus actif.",
            "Réparer les statistiques GeoIP",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await ViewModel.RepairGeoIpSelectedAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                result.Message,
                "Maintenance PinteMod",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async Task ConfirmAndPreparePinteModAsync()
    {
        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Préparer automatiquement ce serveur avec PinteMod ?\n\nLe Control Center installe les fichiers first-party, met le module de compatibilité à jour et, sur ce PC, prépare l’Agent de gestion. Les scripts tiers et les données joueurs ne sont jamais écrasés. Change Map reste fermé par défaut tant qu’aucune carte n’est autorisée.",
            "Préparation automatique PinteMod",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await ViewModel.PreparePinteModOneClickAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(result.Message, "PinteMod Control Center", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void InstallPinteMod_Click(object sender, RoutedEventArgs e) =>
        await ConfirmAndInstallPinteModAsync();

    private async Task ConfirmAndInstallPinteModAsync()
    {
        var repair = ViewModel.SelectedProfile?.Analysis?.PinteModDetected == true;
        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            repair
                ? "Vérifier et réparer les fichiers first-party PinteMod sur ce serveur ?\n\nLe serveur doit être arrêté. Seul le vérificateur v2.1.1 stock connu peut être mis à niveau ; toute autre collision bloque l’opération sans écrasement."
                : "Installer PinteMod sur ce serveur ?\n\nLe serveur doit être arrêté. Les scripts et données existants ne sont jamais écrasés silencieusement : toute collision inconnue bloque l’installation.",
            repair ? "Réparer PinteMod" : "Installer PinteMod",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await ViewModel.InstallPinteModAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(result.Message, "PinteMod Control Center", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void InstallBridge_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new MapAllowlistWindow { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            $"Installer/mettre à jour le Bridge v0.3.1 avec {dialog.SelectedMapCodes.Count} carte(s) autorisée(s) ?\n\nLe serveur doit être arrêté. Seules les cartes réellement installées sur ce serveur doivent être cochées.",
            "Bridge Control Center",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var result = await ViewModel.InstallBridgeAsync(dialog.SelectedMapCodes, _lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(result.Message, "PinteMod Manager", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void EnableRemoteAgent_Click(object sender, RoutedEventArgs e)
    {
        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Activer ou mettre à jour le même PinteMod.ControlCenter.exe comme Agent local pour tous les profils BOIII locaux de ce PC ?\n\nL’Agent démarre avec la session Windows, n’ouvre aucun port et crée un pairing de 15 minutes dans chaque racine serveur.",
            "Agent distant PinteMod",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        await RunAsync(async () =>
        {
            var result = await ViewModel.EnableRemoteAgentAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(result.Message, "Agent distant PinteMod", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void PairRemoteAgent_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            var result = await ViewModel.PairSelectedRemoteAgentAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(result.Message, "Pairing Agent distant", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void UpdateRemoteAgent_Click(object sender, RoutedEventArgs e)
    {
        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            "Synchroniser les deux PC sur la version PinteMod Control Center la plus récente disponible entre eux ?\n\nSi ce PC est en avance, sa version sera poussée vers le PC serveur. Si le PC serveur est en avance, son package authentifié sera récupéré ici. Dans les deux sens : HMAC + SHA-256, aucun downgrade, aucun nouveau port et appairage conservé.",
            "Synchronisation PinteMod Control Center",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        var progressWindow = new PinteMod.ControlCenter.Views.RemoteUpdateProgressWindow { Owner = this };
        var progress = new Progress<PinteMod.ControlCenter.Services.RemoteAgentUpdateProgress>(progressWindow.Report);
        try
        {
            progressWindow.Show();
            IsEnabled = false;
            var result = await ViewModel.UpdateSelectedRemoteAgentAsync(progress, _lifetime.Token);
            progressWindow.Report(new PinteMod.ControlCenter.Services.RemoteAgentUpdateProgress(100,
                result.Success ? "Synchronisation terminée." : "La synchronisation nécessite votre attention."));
            await Task.Delay(250, _lifetime.Token);
            progressWindow.Close();
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(result.Message, "Synchronisation PinteMod Control Center", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            if (result.Success && result.ApplicationRestartRequired)
            {
                Application.Current.Shutdown(0);
                return;
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            if (progressWindow.IsVisible) progressWindow.Close();
        }
        catch (Exception exception)
        {
            if (progressWindow.IsVisible) progressWindow.Close();
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                exception is InvalidOperationException or ArgumentException
                    ? exception.Message
                    : "La synchronisation n’a pas pu être terminée. Vérifiez le partage réseau et réessayez.",
                "Synchronisation PinteMod Control Center",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
            Activate();
        }
    }

    private async void RefreshRemoteAgent_Click(object sender, RoutedEventArgs e) =>
        await RunAsync(() => ViewModel.RefreshSelectedRemoteAgentAsync(_lifetime.Token));

    private async void LaunchAll_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            var result = await ViewModel.LaunchAllLocalPinteModAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                result.Message + (result.Success
                    ? "\n\nLes lancements locaux utilisent Worker v3/RecordHub. Si un secret Worker n’a pas pu être préparé depuis le secret RCON local, le PC serveur peut demander ce secret une fois."
                    : string.Empty),
                "PinteMod MultiServer",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void LaunchSelected_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            var result = await ViewModel.LaunchSelectedAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                result.Message,
                "PinteMod Manager",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void StopSelected_Click(object sender, RoutedEventArgs e)
    {
        var profile = ViewModel.SelectedProfile;
        if (profile is null || !profile.CanStopSelected) return;

        var answer = PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
            $"Arrêter « {profile.DisplayName} » maintenant ?\n\nLa partie en cours sera interrompue. Le Worker et les services PinteMod de ce profil seront également arrêtés.",
            "Arrêter le serveur",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes) return;

        await RunAsync(async () =>
        {
            var result = await ViewModel.StopSelectedAsync(_lifetime.Token);
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                result.Message,
                "PinteMod Manager",
                MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        });
    }

    private async void LaunchAndOpen_Click(object sender, RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            var result = await ViewModel.LaunchSelectedAsync(_lifetime.Token);
            if (!result.Success)
            {
                PinteMod.ControlCenter.Services.PinteModMessageBox.Show(result.Message, "PinteMod Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_companionWindow)
            {
                Application.Current.MainWindow?.Activate();
            }
            else
            {
                DialogResult = true;
            }
        });
    }

    private async void OpenControlCenter_Click(object sender, RoutedEventArgs e) =>
        await OpenSelectedControlCenterAsync();

    private async Task OpenSelectedControlCenterAsync()
    {
        if (_openControlCenterRequested)
        {
            return;
        }

        _openControlCenterRequested = true;
        try
        {
            await RunAsync(async () =>
            {
                var pendingAnalysis = _selectionAnalysisTask;
                if (pendingAnalysis is not null)
                {
                    await pendingAnalysis;
                }

                await ViewModel.AnalyzeSelectedAsync(_lifetime.Token);
                await ViewModel.SelectForControlCenterAsync(_lifetime.Token);
                if (_companionWindow)
                {
                    Application.Current.MainWindow?.Activate();
                }
                else
                {
                    DialogResult = true;
                }
            });
        }
        finally
        {
            if (IsVisible)
            {
                _openControlCenterRequested = false;
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_companionWindow)
        {
            Close();
            return;
        }

        DialogResult = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        base.OnClosed(e);
    }

    private static async Task RunAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (Exception exception)
        {
            PinteMod.ControlCenter.Services.PinteModMessageBox.Show(
                exception is InvalidOperationException or ArgumentException
                    ? exception.Message
                    : "L'opération n'a pas pu être terminée. Vérifiez la racine et les permissions du serveur.",
                "PinteMod Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
