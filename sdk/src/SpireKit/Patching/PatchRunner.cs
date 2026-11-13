using System.Reflection;
using HarmonyLib;
using SpireKit.Diagnostics;
using SpireKit.Logging;

namespace SpireKit.Patching;

/// <summary>Names the feature a Harmony patch class belongs to, for health reports.</summary>
/// <param name="feature">Short id, e.g. <c>events.combat</c>.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SpireFeatureAttribute(string feature) : Attribute
{
    public string Feature { get; } = feature;

    /// <summary>When true, failure to apply disables the whole mod rather than just this feature.</summary>
    public bool Critical { get; init; }
}

/// <summary>
/// Applies Harmony patch classes one at a time so that a game update that breaks one patch
/// (renamed method, changed signature) disables only that feature, reported via <see cref="FeatureHealthRegistry"/>.
/// </summary>
/// <remarks>Replaces <c>Harmony.PatchAll</c>, which aborts on the first failure.</remarks>
public sealed class PatchRunner(Harmony harmony, FeatureHealthRegistry health, string owner, IModLog log)
{
    /// <summary>Applies every <c>[HarmonyPatch]</c> class in <paramref name="assemblies"/>.</summary>
    /// <returns>False if a <see cref="SpireFeatureAttribute.Critical"/> patch failed.</returns>
    public bool ApplyAll(IEnumerable<Assembly> assemblies)
    {
        var ok = true;
        foreach (var type in assemblies.Distinct().SelectMany(SafeGetTypes).Where(IsPatchClass).OrderBy(static t => t.FullName, StringComparer.Ordinal))
        {
            ok &= Apply(type);
        }

        return ok;
    }

    /// <summary>Applies one patch class.</summary>
    public bool Apply(Type patchClass)
    {
        ArgumentNullException.ThrowIfNull(patchClass);
        var meta = patchClass.GetCustomAttribute<SpireFeatureAttribute>();
        var feature = meta?.Feature ?? patchClass.Name;
        try
        {
            var patched = harmony.CreateClassProcessor(patchClass).Patch();
            if (patched is null || patched.Count == 0)
            {
                throw new InvalidOperationException("No target methods were patched.");
            }

            health.Report(owner, feature, FeatureStatus.Healthy);
            return true;
        }
#pragma warning disable CA1031 // Fail-soft by design.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            var reason = (ex.InnerException ?? ex).Message;
            health.Report(owner, feature, FeatureStatus.Disabled, reason);
            log.Error($"Patch '{patchClass.Name}' ({feature}) failed; feature disabled.", ex);
            return meta?.Critical != true;
        }
    }

    private static bool IsPatchClass(Type type) =>
        type.IsClass && type.GetCustomAttributes<HarmonyPatch>(inherit: false).Any();

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }
}
