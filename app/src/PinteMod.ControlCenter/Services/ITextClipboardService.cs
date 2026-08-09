using System.Runtime.InteropServices;
using System.Windows;

namespace PinteMod.ControlCenter.Services;

public interface ITextClipboardService
{
    bool TrySetText(string text);
}

public sealed class WindowsTextClipboardService : ITextClipboardService
{
    public bool TrySetText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            Clipboard.SetText(text, TextDataFormat.UnicodeText);
            return true;
        }
        catch (ExternalException)
        {
            return false;
        }
    }
}
