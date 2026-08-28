using Microsoft.VisualStudio.TestTools.UnitTesting;
using PinteMod.ControlCenter.Infrastructure.Local;

namespace PinteMod.ControlCenter.Tests;

[TestClass]
public sealed class LogPrivacyFilterTests
{
    [TestMethod]
    public void SensitiveValues_AreRemovedOrAbbreviatedBeforePresentation()
    {
        const string fullXuid = "abcdef0123456789";
        var raw = $"xuid={fullXuid} ip=192.168.1.15:28960 guid=123e4567-e89b-12d3-a456-426614174000 path=C:\\Users\\private\\server command=ban {fullXuid}\r\nINJECT";

        var safe = LogPrivacyFilter.SanitizeDisplayText(raw, 500);

        Assert.IsFalse(safe.Contains(fullXuid, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("192.168.1.15", StringComparison.Ordinal));
        Assert.IsFalse(safe.Contains("C:\\Users", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("123e4567", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("command=ban", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains('\r'));
        Assert.IsFalse(safe.Contains('\n'));
    }

    [TestMethod]
    public void Ipv6UncAndUnixPaths_AreRemovedBeforePresentation()
    {
        const string raw = "peer=2001:db8::8a2e:370:7334 endpoint=[fe80::1]:28960 " +
                           "source=\\\\fileserver\\private-share\\PinteMod\\secret.json " +
                           "runtime=/srv/pintemod/private/current_session.json";

        var safe = LogPrivacyFilter.SanitizeDisplayText(raw, 500);

        Assert.IsFalse(safe.Contains("2001:db8", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("fe80::1", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("fileserver", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("private-share", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(safe.Contains("/srv/pintemod", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(safe, "[adresse masquée]");
        StringAssert.Contains(safe, "[chemin masqué]");
    }

}
