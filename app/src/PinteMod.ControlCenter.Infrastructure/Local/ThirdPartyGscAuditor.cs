using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public sealed class ThirdPartyGscAuditor
{
    private const int MaximumFiles = 64;
    private const int MaximumBytesPerFile = 256 * 1024;

    private static readonly IReadOnlyDictionary<string, string> KnownCommandFamilies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["god"] = "Godmode",
            ["godmode"] = "Godmode",
            ["points"] = "Points",
            ["givepoints"] = "Points",
            ["setround"] = "Manches",
            ["nextround"] = "Manches",
            ["round"] = "Manches",
            ["weapon"] = "Armes",
            ["giveweapon"] = "Armes",
            ["pap"] = "Pack-a-Punch",
            ["papweapon"] = "Pack-a-Punch",
            ["perk"] = "Atouts",
            ["perks"] = "Atouts",
            ["kick"] = "Modération",
            ["kill"] = "Joueur",
            ["revive"] = "Joueur",
            ["ammo"] = "Munitions",
            ["tp"] = "Téléportation",
            ["teleport"] = "Téléportation",
            ["map"] = "Carte",
            ["restartmap"] = "Carte"
        };

    public ThirdPartyGscAudit Audit(IReadOnlyList<string> filePaths, CancellationToken cancellationToken = default)
    {
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usesScriptData = false;
        var observesPlayers = false;
        var observesChat = false;
        var scanned = 0;

        foreach (var path in filePaths.Take(MaximumFiles))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(path))
            {
                continue;
            }

            string text;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var length = (int)Math.Min(stream.Length, MaximumBytesPerFile);
                var buffer = new byte[length];
                var read = stream.Read(buffer, 0, length);
                text = Encoding.UTF8.GetString(buffer, 0, read);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            scanned++;
            foreach (Match match in AddCommandRegex.Matches(text))
            {
                var command = match.Groups["command"].Value.Trim().ToLowerInvariant();
                if (command.Length == 0 || command.Length > 40)
                {
                    continue;
                }

                commands.Add(command);
                if (KnownCommandFamilies.TryGetValue(command, out var family))
                {
                    families.Add(family);
                }
            }

            usesScriptData |= ContainsAny(text, "writefile(", "readfile(", "fileexists(", "scriptdata");
            observesPlayers |= ContainsAny(text, "level.players", "getplayers(", "playerconnect", "connected", "spawned_player");
            observesChat |= ContainsAny(text, "sayall(", "iprintln(", "chat", "clientcommand");
        }

        var orderedCommands = commands.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Take(48).ToArray();
        var orderedFamilies = families.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var summary = scanned == 0
            ? "Aucun GSC tiers lisible n’a pu être audité."
            : $"{scanned} GSC audité(s) · {orderedCommands.Length} commande(s) déclarée(s) observée(s)" +
              (orderedFamilies.Length > 0 ? $" · familles : {string.Join(", ", orderedFamilies)}" : string.Empty) +
              ". Les commandes observées ne sont jamais exécutées sans adaptateur fermé explicite.";

        return new ThirdPartyGscAudit(
            scanned,
            orderedCommands,
            orderedFamilies,
            usesScriptData,
            observesPlayers,
            observesChat,
            summary);
    }

    private static bool ContainsAny(string text, params string[] needles) =>
        needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));

    private static readonly Regex AddCommandRegex = new(
        "\\baddcommand\\s*\\(\\s*\"(?<command>[A-Za-z0-9_-]{1,40})\"",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));
}
