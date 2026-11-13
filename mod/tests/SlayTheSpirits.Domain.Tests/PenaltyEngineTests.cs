using SlayTheSpirits.Engine;
using SlayTheSpirits.Party;
using SlayTheSpirits.Penalties;
using SlayTheSpirits.Rules;
using SlayTheSpirits.Triggers;
using static SlayTheSpirits.Tests.TestParty;

namespace SlayTheSpirits.Tests;

public sealed class PenaltyEngineTests
{
    private const ulong Seed = 0xC0FFEE;

    private static readonly PenaltyCause CardCause = new("CARD.SPIRITS_TEST");

    private static PenaltyEngine NewEngine(RuleSet? rules = null, RunPenaltyState? state = null) =>
        new(rules ?? Presets.Standard, state ?? new RunPenaltyState(), Seed);

    private static PenaltyRequest Request(PenaltyKind kind, TargetKind target, PlayerId? subject = null, int count = 1) =>
        new(new Penalty(kind, count), target, subject, null, CardCause);

    [Fact]
    public void Boss_start_gives_everyone_a_shot()
    {
        var engine = NewEngine();

        var result = engine.Handle(new TriggerOccurrence(BuiltInTriggers.BossEncounterStarted.Id), Four);

        Assert.Equal([Alice, Bob, Cara, Dave], result.Select(a => a.Player));
        Assert.All(result, a => Assert.Equal(PenaltyKind.Shot, a.Penalty.Kind));
    }

    [Fact]
    public void Others_excludes_subject_and_subject_targets_only_subject()
    {
        var engine = NewEngine();

        Assert.Equal([Alice, Cara, Dave], engine.Handle(Request(PenaltyKind.Sip, TargetKind.Others, Bob), Four).Select(a => a.Player));
        Assert.Equal([Bob], engine.Handle(Request(PenaltyKind.Sip, TargetKind.Subject, Bob), Four).Select(a => a.Player));
    }

    [Fact]
    public void Random_targets_are_identical_across_peers_with_the_same_seed()
    {
        var peerA = NewEngine();
        var peerB = NewEngine();

        var picksA = Enumerable.Range(0, 20).Select(_ => peerA.Handle(Request(PenaltyKind.Sip, TargetKind.RandomPlayer), Four).Single().Player).ToList();
        var picksB = Enumerable.Range(0, 20).Select(_ => peerB.Handle(Request(PenaltyKind.Sip, TargetKind.RandomPlayer), Four).Single().Player).ToList();

        Assert.Equal(picksA, picksB);
        Assert.True(picksA.Distinct().Count() > 1);
    }

    [Fact]
    public void RandomOther_never_picks_the_subject()
    {
        var engine = NewEngine();

        for (var i = 0; i < 50; i++)
        {
            Assert.NotEqual(Alice, engine.Handle(Request(PenaltyKind.Sip, TargetKind.RandomOther, Alice), Four).Single().Player);
        }

        Assert.Empty(engine.Handle(Request(PenaltyKind.Sip, TargetKind.RandomOther, Alice), Solo));
    }

    [Fact]
    public void Caps_downgrade_then_reset_on_new_act()
    {
        var engine = NewEngine();
        var shot = Request(PenaltyKind.Shot, TargetKind.Subject, Alice);

        var kinds = Enumerable.Range(0, 4).Select(_ => engine.Handle(shot, Four).Single()).ToList();

        Assert.Equal([PenaltyKind.Shot, PenaltyKind.Shot, PenaltyKind.Shot, PenaltyKind.Drink], kinds.Select(a => a.Penalty.Kind));
        Assert.Contains(AssignmentNote.Downgraded, kinds[3].Notes);
        Assert.Equal(PenaltyKind.Shot, kinds[3].OriginalPenalty?.Kind);

        engine.EnterAct(2);
        Assert.Equal(PenaltyKind.Shot, engine.Handle(shot, Four).Single().Penalty.Kind);
    }

    [Fact]
    public void Zero_caps_cascade_down_through_multiple_kinds()
    {
        var engine = NewEngine(Presets.Casual);

        var result = engine.Handle(Request(PenaltyKind.Finish, TargetKind.Subject, Alice), Four).Single();

        Assert.Equal(PenaltyKind.Drink, result.Penalty.Kind);
    }

    [Fact]
    public void Immune_players_skip_alcohol_but_still_hydrate()
    {
        var engine = NewEngine();
        engine.GrantImmunity(Bob);

        var shots = engine.Handle(Request(PenaltyKind.Shot, TargetKind.Everyone), Four);
        var water = engine.Handle(new TriggerOccurrence(BuiltInTriggers.RestSiteEntered.Id), Four);

        Assert.DoesNotContain(Bob, shots.Select(a => a.Player));
        Assert.Contains(Bob, water.Select(a => a.Player));

        engine.ClearImmunities();
        Assert.Contains(Bob, engine.Handle(Request(PenaltyKind.Shot, TargetKind.Everyone), Four).Select(a => a.Player));
    }

    [Fact]
    public void Redirect_passes_the_next_penalty_to_someone_else_once()
    {
        var engine = NewEngine();
        engine.AddRedirects(Alice);

        var first = engine.Handle(Request(PenaltyKind.Drink, TargetKind.Subject, Alice), Four).Single();
        var second = engine.Handle(Request(PenaltyKind.Drink, TargetKind.Subject, Alice), Four).Single();

        Assert.NotEqual(Alice, first.Player);
        Assert.Equal(Alice, first.OriginalPlayer);
        Assert.Contains(AssignmentNote.Redirected, first.Notes);
        Assert.Equal(Alice, second.Player);
    }

    [Fact]
    public void Tally_tracks_totals_and_worst_moment()
    {
        var engine = NewEngine();
        engine.Handle(Request(PenaltyKind.Sip, TargetKind.Subject, Alice), Four);
        engine.Handle(new TriggerOccurrence(BuiltInTriggers.PlayerDied.Id, Alice, Label: "Gremlin Nob"), Four);
        engine.Handle(Request(PenaltyKind.Sip, TargetKind.Subject, Alice, count: 2), Four);

        var tally = engine.State.Tally;
        Assert.Equal(3, tally.Count(Alice, PenaltyKind.Sip));
        Assert.Equal(1, tally.Count(Alice, PenaltyKind.Finish));
        Assert.Equal((3 * 1) + 5, tally.TipsyOf(Alice));
        Assert.Equal(PenaltyKind.Finish, tally.WorstMoments[Alice.Value].Penalty.Kind);
        Assert.Equal("Gremlin Nob", tally.WorstMoments[Alice.Value].Label);
    }

    [Fact]
    public void Tipsy_uses_configurable_weights_and_counts()
    {
        var rules = Presets.Standard with
        {
            Tipsy = new Dictionary<PenaltyKind, int> { [PenaltyKind.Shot] = 10 },
            CapsPerAct = new Dictionary<PenaltyKind, int>(),
        };
        var engine = NewEngine(rules);

        var shots = engine.Handle(Request(PenaltyKind.Shot, TargetKind.Subject, Alice, count: 2), Four).Single();
        var sip = engine.Handle(Request(PenaltyKind.Sip, TargetKind.Subject, Alice), Four).Single();

        Assert.Equal(20, shots.Tipsy);
        Assert.Equal(1, sip.Tipsy); // missing kind falls back to the default weight
        Assert.Equal(21, engine.TipsyOf(Alice));
        Assert.Equal(0, engine.TipsyOf(Bob));
    }

    [Fact]
    public void Water_and_cancelled_penalties_give_no_tipsy()
    {
        var engine = NewEngine();
        engine.GrantImmunity(Bob);

        engine.Handle(new TriggerOccurrence(BuiltInTriggers.RestSiteEntered.Id), Four);
        engine.Handle(Request(PenaltyKind.Shot, TargetKind.Subject, Bob), Four);

        Assert.Equal(0, engine.TipsyOf(Alice));
        Assert.Equal(0, engine.TipsyOf(Bob));
    }

    [Fact]
    public void Downgraded_penalties_give_tipsy_for_what_was_actually_drunk()
    {
        var engine = NewEngine();
        var shot = Request(PenaltyKind.Shot, TargetKind.Subject, Alice);
        for (var i = 0; i < 3; i++)
        {
            engine.Handle(shot, Four);
        }

        var capped = engine.Handle(shot, Four).Single();

        Assert.Equal(TipsyWeights.Defaults[PenaltyKind.Drink], capped.Tipsy);
    }

    [Fact]
    public void Persisted_state_resumes_identically()
    {
        var original = NewEngine();
        original.AddRedirects(Cara, 2);
        for (var i = 0; i < 5; i++)
        {
            original.Handle(Request(PenaltyKind.Shot, TargetKind.RandomPlayer), Four);
        }

        var restoredState = RunPenaltyStateCodec.FromJson(RunPenaltyStateCodec.ToJson(original.State))!;
        var restored = NewEngine(state: restoredState);

        Assert.All(Four.Members, m => Assert.Equal(original.TipsyOf(m.Id), restored.TipsyOf(m.Id)));
        Assert.Equal(original.State.Tally.WorstMoments, restored.State.Tally.WorstMoments);

        for (var i = 0; i < 10; i++)
        {
            var request = Request(PenaltyKind.Shot, TargetKind.RandomPlayer);
            Assert.Equal(original.Handle(request, Four), restored.Handle(request, Four), AssignmentComparer.Instance);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"schema\": 99, \"data\": {}}")]
    public void Unreadable_state_is_rejected(string json) => Assert.Null(RunPenaltyStateCodec.FromJson(json));

    private sealed class AssignmentComparer : IEqualityComparer<PenaltyAssignment>
    {
        public static readonly AssignmentComparer Instance = new();

        public bool Equals(PenaltyAssignment? x, PenaltyAssignment? y) =>
            x is not null && y is not null
            && x.Player == y.Player && x.Penalty == y.Penalty && x.OriginalPlayer == y.OriginalPlayer
            && x.Tipsy == y.Tipsy && x.Notes.SequenceEqual(y.Notes);

        public int GetHashCode(PenaltyAssignment obj) => HashCode.Combine(obj.Player, obj.Penalty);
    }
}
