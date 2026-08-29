using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;

namespace PinteMod.ControlCenter.Services;

/// <summary>
/// Converts local activation failures into operator-safe messages. Exception
/// text is deliberately never shown because it may contain private paths.
/// </summary>
internal static class RemoteAgentActivationDiagnostic
{
    public static string Describe(Exception exception) => exception switch
    {
        CryptographicException =>
            "L’Agent ne peut pas accéder au stockage sécurisé Windows (DPAPI) de cette session. Ouvrez le Control Center dans la session Windows propriétaire du PC serveur, puis réessayez.",
        UnauthorizedAccessException =>
            "L’Agent ne peut pas écrire dans son dossier local ou préparer sa tâche Windows. Vérifiez que le Control Center est lancé depuis la session Windows du PC serveur et que le dossier n’est pas protégé.",
        IOException =>
            "L’Agent ne peut pas accéder à un de ses fichiers locaux. Fermez les autres fenêtres du Control Center puis réessayez.",
        Win32Exception =>
            "Windows a refusé le démarrage ou la préparation de l’Agent. Relancez le Control Center depuis la session Windows du PC serveur, puis réessayez.",
        _ => $"La préparation locale de l’Agent a été interrompue ({exception.GetType().Name}). Aucun ordre BOIII n’a été envoyé."
    };
}
