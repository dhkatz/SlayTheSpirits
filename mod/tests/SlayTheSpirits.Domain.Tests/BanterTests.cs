using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;

namespace SlayTheSpirits.Tests;

public sealed class BanterTests
{
    [Fact]
    public void Giver_lines_name_the_target_or_address_everyone()
    {
        var random = new Random(1);
        for (var i = 0; i < 50; i++)
        {
            var line = Banter.GiverLine(random, "Dave");
            Assert.DoesNotContain("{0}", line, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("{0}", Banter.GiverLine(random, null), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_penalty_kind_has_drinker_lines()
    {
        var random = new Random(1);
        foreach (var kind in Enum.GetValues<PenaltyKind>())
        {
            Assert.False(string.IsNullOrWhiteSpace(Banter.DrinkerLine(random, kind)));
        }
    }
}
