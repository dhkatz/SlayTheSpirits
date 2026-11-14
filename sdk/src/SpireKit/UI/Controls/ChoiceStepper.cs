namespace SpireKit.UI.Controls;

/// <summary>
/// A compact option picker (<c>‹ Everyone ›</c>): a <see cref="NumberStepper"/> over a list of labels that wraps
/// around. <see cref="NumberStepper.Value"/> is the selected index. For full-width settings rows prefer the game's
/// own picker (<see cref="Settings.SettingsChoice"/>); this one fits several to a row.
/// </summary>
public partial class ChoiceStepper : NumberStepper
{
    private IReadOnlyList<string> _options = [];

    public ChoiceStepper()
    {
        Wrap = true;
        LabelWidth = 160;
        Format = i => i >= 0 && i < _options.Count ? _options[i] : string.Empty;
    }

    /// <summary>The labels to choose from. Setting it keeps the index if still in range.</summary>
    public IReadOnlyList<string> Options
    {
        get => _options;
        set
        {
            _options = value ?? [];
            Min = 0;
            Max = Math.Max(0, _options.Count - 1);
            SetValueNoSignal(Value);
        }
    }
}
