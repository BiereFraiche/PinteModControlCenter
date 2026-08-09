using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PinteMod.ControlCenter.Core.Contracts;
using PinteMod.ControlCenter.Core.Models;
using PinteMod.ControlCenter.Core.Security;

namespace PinteMod.ControlCenter.Infrastructure.Rcon;

public sealed partial class BoiiiUdpRconClient : IRconClient
{
    private const int MaximumResponseBytes = 64 * 1024;

    public async Task<string> SendAsync(
        RconEndpoint endpoint,
        string password,
        string command,
        CancellationToken cancellationToken = default)
    {
        Validate(endpoint, password, command);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(endpoint.Timeout);
        using var client = new UdpClient();

        var marker = Encoding.ASCII.GetBytes("rcon ");
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var separator = new byte[] { (byte)' ' };
        var commandBytes = Encoding.UTF8.GetBytes(command);
        var body = new byte[marker.Length + passwordBytes.Length + separator.Length + commandBytes.Length];
        marker.CopyTo(body, 0);
        passwordBytes.CopyTo(body, marker.Length);
        separator.CopyTo(body, marker.Length + passwordBytes.Length);
        commandBytes.CopyTo(body, marker.Length + passwordBytes.Length + separator.Length);
        var packet = new byte[body.Length + 4];
        packet.AsSpan(0, 4).Fill(0xFF);
        body.CopyTo(packet, 4);

        try
        {
            await client.SendAsync(packet, endpoint.Address, endpoint.Port, timeout.Token).ConfigureAwait(false);
            var response = await client.ReceiveAsync(timeout.Token).ConfigureAwait(false);
            if (response.Buffer.Length <= 4)
            {
                return string.Empty;
            }

            var length = Math.Min(response.Buffer.Length - 4, MaximumResponseBytes);
            var text = Encoding.UTF8.GetString(response.Buffer, 4, length);
            return PrintPrefix().Replace(text, string.Empty).Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Le serveur RCON n’a pas répondu dans le délai prévu.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
            Array.Clear(body);
            Array.Clear(packet);
        }
    }

    private static void Validate(RconEndpoint endpoint, string password, string command)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!RconEndpointValidator.IsAllowed(endpoint))
        {
            throw new ArgumentException("La cible RCON est invalide.", nameof(endpoint));
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length > 128 ||
            password.Any(character => char.IsWhiteSpace(character) || character == '"'))
        {
            throw new ArgumentException("Le secret RCON est invalide.", nameof(password));
        }

        if (string.IsNullOrWhiteSpace(command) || command.Length > 128 ||
            command.Any(character => character is '\r' or '\n' or '\0'))
        {
            throw new ArgumentException("La commande RCON est invalide.", nameof(command));
        }
    }

    [GeneratedRegex(@"(?i)^print[\s\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex PrintPrefix();
}
