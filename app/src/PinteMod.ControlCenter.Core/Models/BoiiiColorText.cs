namespace PinteMod.ControlCenter.Core.Models;

public sealed record BoiiiColorTextSegment(char ColorCode, string Text);

public sealed record BoiiiColorEditResult(
    string Text,
    int SelectionStart,
    int SelectionLength,
    bool Applied);

public static class BoiiiColorText
{
    public const char DefaultColorCode = '7';

    public static IReadOnlyList<BoiiiColorTextSegment> Parse(string? encodedText)
    {
        var text = encodedText ?? string.Empty;
        var segments = new List<BoiiiColorTextSegment>();
        var currentColor = DefaultColorCode;
        var buffer = new System.Text.StringBuilder();

        void Flush()
        {
            if (buffer.Length == 0)
            {
                return;
            }

            segments.Add(new BoiiiColorTextSegment(currentColor, buffer.ToString()));
            buffer.Clear();
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '^' &&
                index + 1 < text.Length &&
                IsColorCode(text[index + 1]))
            {
                Flush();
                currentColor = text[index + 1];
                index++;
                continue;
            }

            buffer.Append(text[index]);
        }

        Flush();
        return segments;
    }

    public static BoiiiColorEditResult ApplyColor(
        string? encodedText,
        int selectionStart,
        int selectionLength,
        char colorCode,
        int maximumLength)
    {
        var text = encodedText ?? string.Empty;
        if (!IsColorCode(colorCode) || maximumLength <= 0)
        {
            return new(text, Clamp(selectionStart, 0, text.Length), 0, false);
        }

        var start = Clamp(selectionStart, 0, text.Length);
        var length = Clamp(selectionLength, 0, text.Length - start);
        var token = $"^{colorCode}";
        string updated;
        int updatedSelectionStart;
        int updatedSelectionLength;

        if (length == 0)
        {
            updated = text.Insert(start, token);
            updatedSelectionStart = start + token.Length;
            updatedSelectionLength = 0;
        }
        else
        {
            var previousColor = ColorAt(text, start);
            var selected = text.Substring(start, length);
            var replacement = $"{token}{selected}^{previousColor}";
            updated = text.Remove(start, length).Insert(start, replacement);
            updatedSelectionStart = start + token.Length;
            updatedSelectionLength = selected.Length;
        }

        return updated.Length <= maximumLength
            ? new(updated, updatedSelectionStart, updatedSelectionLength, true)
            : new(text, start, length, false);
    }

    public static bool IsColorCode(char value) => value is >= '0' and <= '9';

    private static char ColorAt(string text, int position)
    {
        var color = DefaultColorCode;
        for (var index = 0; index + 1 < position; index++)
        {
            if (text[index] == '^' && IsColorCode(text[index + 1]))
            {
                color = text[index + 1];
                index++;
            }
        }

        return color;
    }

    private static int Clamp(int value, int minimum, int maximum) =>
        Math.Min(Math.Max(value, minimum), maximum);
}
