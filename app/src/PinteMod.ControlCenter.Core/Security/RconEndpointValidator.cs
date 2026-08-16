using System.Net;
using System.Net.Sockets;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Core.Security;

public static class RconEndpointValidator
{
    public static bool IsAllowed(RconEndpoint? endpoint) =>
        endpoint is not null &&
        endpoint.Port is >= 1 and <= 65535 &&
        endpoint.Timeout >= TimeSpan.FromMilliseconds(500) &&
        endpoint.Timeout <= TimeSpan.FromSeconds(15) &&
        IsLocalOrPrivateAddress(endpoint.Address);

    public static bool IsLocalOrPrivateAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64 ||
            !IPAddress.TryParse(value.Trim(), out var address))
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return bytes[0] == 10 ||
                   bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168 ||
                   bytes[0] == 169 && bytes[1] == 254;
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 &&
               (address.IsIPv6LinkLocal || (bytes[0] & 0xFE) == 0xFC);
    }

    public static bool IsLoopbackAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !IPAddress.TryParse(value.Trim(), out var address))
        {
            return false;
        }

        return IPAddress.IsLoopback(
            address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address);
    }
}
