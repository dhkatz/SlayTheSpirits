using System.Reflection;
using System.Reflection.Emit;
using SpireKit.Compat;
using SpireKit.Logging;

namespace SpireKit.Tests;

public sealed class FakeRune;

public sealed class FakePlayer;

// Run one at a time: the fake ARAM is static.
[Collection(nameof(HextechInteropTests))]
public sealed class HextechInteropTests : IDisposable
{
    public HextechInteropTests() => HextechRunes.HextechRunesInterop.Reset();

    public void Dispose() => HextechRunes.HextechRunesInterop.Reset();

    private static HextechInterop Bind() => HextechInterop.Bind(typeof(HextechInteropTests).Assembly, typeof(Func<FakePlayer, bool>));

    [Fact]
    public void Passes_rune_metadata_as_the_names_ARAM_parses()
    {
        Func<FakePlayer, bool> filter = _ => true;
        var rune = new HextechRune(typeof(FakeRune), HextechRarity.Prismatic)
        {
            Options = HextechRuneOptions.FirstActExcluded | HextechRuneOptions.ThirdActExcluded,
            Character = HextechCharacter.Necrobinder,
            CharacterOrder = 3,
            Tag = "SUMMON",
        };

        var used = Bind().RegisterPlayerRune(rune, "MyMod", filter);

        var call = Assert.Single(HextechRunes.HextechRunesInterop.Registered);
        Assert.Equal<object?[]>([typeof(FakeRune), "Prismatic", "FirstActExcluded,ThirdActExcluded", "Necrobinder", 3, "SUMMON", "MyMod", filter], call);
        Assert.Equal(rune.Options, used);
        Assert.Empty(HextechRunes.HextechRunesInterop.Labels);
    }

    [Fact]
    public void Defaults_are_nulls()
    {
        Bind().RegisterPlayerRune(new HextechRune(typeof(FakeRune), HextechRarity.Silver), "MyMod", null);

        var call = Assert.Single(HextechRunes.HextechRunesInterop.Registered);
        Assert.Equal<object?[]>([typeof(FakeRune), "Silver", null, null, 0, null, "MyMod", null], call);
    }

    [Fact]
    public void Sets_the_pool_label_after_registering()
    {
        Bind().RegisterPlayerRune(new HextechRune(typeof(FakeRune), HextechRarity.Gold) { PoolLabel = "TAVERN" }, "MyMod", null);

        Assert.Equal((typeof(FakeRune), "TAVERN"), Assert.Single(HextechRunes.HextechRunesInterop.Labels));
    }

    [Fact]
    public void Leaves_out_flags_an_older_ARAM_would_reject()
    {
        HextechRunes.HextechRunesInterop.Version = 1;
        var rune = new HextechRune(typeof(FakeRune), HextechRarity.Gold) { Options = HextechRuneOptions.GuaranteedFirstPick | HextechRuneOptions.Disabled };

        var interop = Bind();
        var used = interop.RegisterPlayerRune(rune, "MyMod", null);

        Assert.Equal(1, interop.ApiVersion);
        Assert.Equal(HextechRuneOptions.Disabled, used);
        Assert.Equal("Disabled", Assert.Single(HextechRunes.HextechRunesInterop.Registered)[2]);
    }

    [Fact]
    public void Rethrows_ARAMs_own_exception()
    {
        HextechRunes.HextechRunesInterop.Throw = new InvalidOperationException("window closed");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            Bind().RegisterPlayerRune(new HextechRune(typeof(FakeRune), HextechRarity.Gold), "MyMod", null));

        Assert.Equal("window closed", ex.Message);
    }

    [Fact]
    public void Registers_the_config_section_title()
    {
        Assert.True(Bind().RegisterConfigSectionTitle("MyMod", "MY_MOD_SECTION"));

        Assert.Equal(("MyMod", "MY_MOD_SECTION"), Assert.Single(HextechRunes.HextechRunesInterop.Sections));
    }

    [Fact]
    public void An_assembly_without_the_API_is_not_supported() =>
        Assert.Throws<NotSupportedException>(() => HextechInterop.Bind(typeof(object).Assembly, typeof(Func<FakePlayer, bool>)));

    [Fact]
    public void A_different_filter_signature_is_not_supported() =>
        Assert.Throws<NotSupportedException>(() => HextechInterop.Bind(typeof(HextechInteropTests).Assembly, typeof(Func<object, bool>)));

    [Fact]
    public void Formats_no_flags_as_null() => Assert.Null(HextechInterop.FormatFlags(HextechRuneOptions.None));
}

public sealed class OptionalAssemblyTests
{
    [Fact]
    public void Runs_straight_away_for_a_loaded_assembly()
    {
        Assembly? seen = null;

        OptionalAssembly.WhenLoaded("SpireKit.Core", a => seen = a, NullModLog.Instance);

        Assert.Same(typeof(OptionalAssembly).Assembly, seen);
    }

    [Fact]
    public void Runs_once_when_the_assembly_loads_later()
    {
        var name = $"SpireKitLateAssembly{Guid.NewGuid():N}";
        var seen = new List<Assembly>();

        OptionalAssembly.WhenLoaded(name, seen.Add, NullModLog.Instance);
        Assert.Empty(seen);

        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);

        Assert.Equal(name, Assert.Single(seen).GetName().Name);
    }

    [Fact]
    public void Logs_a_failing_callback_instead_of_throwing()
    {
        var log = new RecordingLog();

        OptionalAssembly.WhenLoaded("SpireKit.Core", _ => throw new InvalidOperationException("boom"), log);

        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("boom", entry.Exception?.Message);
    }
}
