using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace SpireKit.UI.Settings;

/// <summary>The game's settings option picker (<c>‹ Normal ›</c> with sliding text) driving a mod setting.</summary>
public partial class SettingsChoice : NPaginator
{
    private int _initial;

    public SettingsChoice()
    {
        GameScenes.Graft(this, GameScenes.Paginator);

        // The Settings screen sizes its pickers itself (the scene's root has no minimum), arrows included.
        CustomMinimumSize = new Godot.Vector2(Math.Max(CustomMinimumSize.X, 324), Math.Max(CustomMinimumSize.Y, 64));
    }

    /// <summary>Raised with the new index when the player pages (not when <see cref="Select"/> is called).</summary>
    public event Action<int>? Changed;

    /// <summary>The options' labels, in order.</summary>
    public IReadOnlyList<string> Options => _options;

    public int SelectedIndex => _currentIndex;

    /// <summary>Replaces the options and selects <paramref name="index"/>; safe before the control is in the tree.</summary>
    public void SetOptions(IEnumerable<string> options, int index)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options.Clear();
        _options.AddRange(options);
        Select(index);
    }

    /// <summary>Selects an option without raising <see cref="Changed"/>.</summary>
    public void Select(int index)
    {
        _initial = Math.Clamp(index, 0, Math.Max(0, _options.Count - 1));
        _currentIndex = _initial;
        if (IsNodeReady())
        {
            ShowCurrent();
        }
    }

    /// <summary>Greys out the arrows and ignores input (read-only rules).</summary>
    public void SetEditable(bool editable)
    {
        foreach (var arrow in GetChildren().OfType<NSettingsPaginatorArrow>())
        {
            if (editable)
            {
                arrow.Enable();
            }
            else
            {
                arrow.Disable();
            }
        }

        FocusMode = editable ? FocusModeEnum.All : FocusModeEnum.None;
        SetProcessInput(editable);
        _editable = editable;
    }

    private bool _editable = true;

    public override void _Ready()
    {
        ConnectSignals();
        _currentIndex = _initial;
        ShowCurrent();
    }

    public override void _GuiInput(Godot.InputEvent input)
    {
        if (_editable)
        {
            base._GuiInput(input);
        }
    }

    protected override void OnIndexChanged(int index)
    {
        ShowCurrent();
        Changed?.Invoke(index);
    }

    private void ShowCurrent()
    {
        if (_currentIndex >= 0 && _currentIndex < _options.Count)
        {
            _label.SetTextAutoSize(_options[_currentIndex]);
        }
    }
}
