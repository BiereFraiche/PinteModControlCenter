namespace PinteMod.ControlCenter.ViewModels;

public sealed class PublicChatTipItemViewModel : ObservableObject
{
    private string _text;

    public PublicChatTipItemViewModel(string text)
    {
        _text = text;
    }

    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value ?? string.Empty);
    }
}
