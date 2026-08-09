using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

public static class OfficialMapNameResolver
{
    public static string Resolve(string mapCode) => OfficialMapCatalog.ResolveName(mapCode);
}
