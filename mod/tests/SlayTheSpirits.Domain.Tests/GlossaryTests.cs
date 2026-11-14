using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Tests;

public sealed class GlossaryTests
{
    [Fact]
    public void Penalty_entries_state_the_configured_tipsy()
    {
        var rules = Presets.Standard with { Tipsy = new Dictionary<PenaltyKind, int> { [PenaltyKind.Shot] = 7 } };

        var shot = Glossary.For(rules, PenaltyKind.Shot);

        Assert.Equal("Shot", shot.Title);
        Assert.Equal("Take a shot of liquor. Gain 7 Tipsy.", shot.Body);
    }

    [Fact]
    public void Water_gives_no_tipsy_line()
    {
        Assert.Equal("Drink some water.", Glossary.For(Presets.Standard, PenaltyKind.Water).Body);
    }

    [Fact]
    public void Custom_labels_replace_the_default_action_text()
    {
        var shot = Glossary.For(Presets.Sober, PenaltyKind.Shot);

        Assert.Equal("Shot of pickle juice", shot.Title);
        Assert.Equal("Shot of pickle juice. Gain 10 Tipsy.", shot.Body);
    }

    [Fact]
    public void Tipsy_entry_lists_the_scale()
    {
        var body = Glossary.Tipsy(Presets.Standard).Body;

        Assert.Contains("Sip 1, Finish Your Drink 3, Chug 5, Shot 10", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Water", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Tab_entries_use_the_plural_label()
    {
        Assert.Equal("Shots", Glossary.Tab(Presets.Standard, PenaltyKind.Shot).Title);
        Assert.StartsWith("Shots of pickle juice handed", Glossary.Tab(Presets.Sober, PenaltyKind.Shot).Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Drinks_are_not_confirmed_by_default()
    {
        Assert.False(new RuleSet().ConfirmDrinks);
        Assert.False(Presets.Standard.ConfirmDrinks);
    }
}
