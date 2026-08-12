using System.Globalization;
using System.Text.Json;
using PinteMod.ControlCenter.Core.Models;

namespace PinteMod.ControlCenter.Infrastructure.Local;

internal static class RuntimeJsonContract
{
    public const string TimeAuthority = "session_gettime_and_file_mtime";

    public static void RequireObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw Invalid("La racine JSON doit être un objet.");
        }
    }

    public static string RequiredString(
        JsonElement root,
        string name,
        int maximumLength,
        Func<string, bool>? validator = null)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw Invalid($"Champ requis invalide : {name}.");
        }

        var value = property.GetString()?.Trim() ?? string.Empty;
        if (value.Length is 0 || value.Length > maximumLength || validator?.Invoke(value) == false)
        {
            throw Invalid($"Champ requis invalide : {name}.");
        }

        return value;
    }

    public static string? OptionalString(
        JsonElement root,
        string name,
        int maximumLength,
        Func<string, bool>? validator = null,
        bool allowEmpty = true)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            throw Invalid($"Champ optionnel invalide : {name}.");
        }

        var value = property.GetString()?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            if (allowEmpty)
            {
                return null;
            }

            throw Invalid($"Champ optionnel invalide : {name}.");
        }

        if (value.Length > maximumLength || validator?.Invoke(value) == false)
        {
            throw Invalid($"Champ optionnel invalide : {name}.");
        }

        return value;
    }

    public static long RequiredInt64(JsonElement root, string name, long minimum, long maximum)
    {
        if (!root.TryGetProperty(name, out var property) || !TryInt64(property, out var value) ||
            value < minimum || value > maximum)
        {
            throw Invalid($"Champ numérique invalide : {name}.");
        }

        return value;
    }

    public static long? OptionalInt64(JsonElement root, string name, long minimum, long maximum)
    {
        if (!root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (!TryInt64(property, out var value) || value < minimum || value > maximum)
        {
            throw Invalid($"Champ numérique optionnel invalide : {name}.");
        }

        return value;
    }

    public static int RequiredInt32(JsonElement root, string name, int minimum, int maximum) =>
        checked((int)RequiredInt64(root, name, minimum, maximum));

    public static int? OptionalInt32(JsonElement root, string name, int minimum, int maximum)
    {
        var value = OptionalInt64(root, name, minimum, maximum);
        return value is null ? null : checked((int)value.Value);
    }

    public static bool RequiredFlag(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var property))
        {
            throw Invalid($"Indicateur requis invalide : {name}.");
        }

        if (property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return property.GetBoolean();
        }

        if (TryInt64(property, out var numeric) && numeric is 0 or 1)
        {
            return numeric == 1;
        }

        throw Invalid($"Indicateur requis invalide : {name}.");
    }

    public static DateTimeOffset? OptionalUtc(JsonElement root, string name)
    {
        var value = OptionalString(root, name, 64);
        if (value is null)
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            throw Invalid($"Horodatage optionnel invalide : {name}.");
        }

        return parsed;
    }

    public static bool IsSafeIdentifier(string value) =>
        value.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.');

    public static LocalJsonValidationException Invalid(string message) =>
        new(LocalReadStatus.Invalid, message);

    private static bool TryInt64(JsonElement property, out long value)
    {
        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetInt64(out value);
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return long.TryParse(
                property.GetString(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value);
        }

        value = 0;
        return false;
    }
}
