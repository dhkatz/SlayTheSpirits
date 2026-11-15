using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Saves;
using SpireKit.Patching;

namespace SlayTheSpirits.Tavern;

/// <summary>
/// The Tavern's sound: its own music track, and the bartender's voice (the merchant's voice lines, pitched down).
/// </summary>
/// <remarks>
/// <para>
/// The game's music is FMOD, built into its sound banks, so a mod can't add a track there. The Tavern's track is an
/// OGG in the mod's pack, played through Godot's own audio, which runs alongside FMOD. While it plays, the act's FMOD
/// music is stopped; on leaving, <see cref="NRunMusicController.StopCustomMusic"/> brings it back the way the game
/// does after its own event music (Wongos). Without the OGG, the Tavern keeps the game's shop music.
/// </para>
/// <para>
/// Godot's audio doesn't see the game's volume sliders, so the track follows them by hand: every master/music volume
/// change (sliders, mute in background) goes through <see cref="NAudioManager"/>, which <see cref="TavernVolumePatch"/>
/// listens to.
/// </para>
/// </remarks>
internal static class TavernAudio
{
    public const string MusicPath = "res://SlayTheSpirits/audio/tavern_music.ogg";

    /// <summary>FMOD pitch multiplier for the bartender's voice (0.65 is about seven semitones down, and slower).</summary>
    public const float VoicePitch = 0.65f;

    public const string Welcome = "event:/sfx/npcs/merchant/merchant_welcome";
    public const string Thanks = "event:/sfx/npcs/merchant/merchant_thank_yous";
    public const string Disappointed = "event:/sfx/npcs/merchant/merchant_dissapointment";

    /// <summary>The track's level next to the game's music at the same slider settings.</summary>
    private const float MusicGain = 0.8f;
    private const float SilentDb = -60f;

    private static readonly AccessTools.FieldRef<NRunMusicController, Node> RunMusicProxy =
        AccessTools.FieldRefAccess<NRunMusicController, Node>("_proxy");

    private static AudioStream? _music;
    private static bool _musicLoaded;
    private static AudioStreamPlayer? _player;
    private static Tween? _fade;
    private static bool _actMusicStopped;
    private static float? _master;
    private static float? _bgm;

    /// <summary>Starts the Tavern track (if the mod has one) in place of the act's music.</summary>
    public static void StartMusic()
    {
        if (_player is not null || Godot.Engine.GetMainLoop() is not SceneTree tree || LoadMusic() is not { } stream)
        {
            return;
        }

        if (NRunMusicController.Instance is { } runMusic && RunMusicProxy(runMusic) is { } proxy)
        {
            proxy.Call("stop_music");
            _actMusicStopped = true;
        }

        // Parented to the root, not the room, so it can fade out after the room is gone.
        _player = new AudioStreamPlayer { Stream = stream, VolumeDb = SilentDb };
        tree.Root.AddChild(_player);
        _player.Play();
        _fade = _player.CreateTween();
        _fade.TweenProperty(_player, "volume_db", TargetDb(), 2.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Fades the Tavern track out; with <paramref name="restoreActMusic"/>, the act's music comes back.</summary>
    public static void StopMusic(bool restoreActMusic)
    {
        if (_player is { } player)
        {
            _player = null;
            _fade?.Kill();
            _fade = null;
            var fade = player.CreateTween();
            fade.TweenProperty(player, "volume_db", SilentDb, 1.0);
            fade.TweenCallback(Callable.From(player.QueueFree));
        }

        if (_actMusicStopped && restoreActMusic)
        {
            NRunMusicController.Instance?.StopCustomMusic();
        }

        _actMusicStopped = false;
    }

    /// <summary>Plays one of the merchant's voice lines, pitched down for the ghost; at normal pitch if FMOD won't.</summary>
    public static void Voice(string fmodEvent)
    {
        if (Godot.Engine.HasSingleton("FmodServer")
            && Godot.Engine.GetSingleton("FmodServer").Call("create_event_instance", fmodEvent).AsGodotObject() is { } instance
            && instance.HasMethod("set_pitch")
            && instance.HasMethod("start"))
        {
            instance.Call("set_pitch", VoicePitch);
            instance.Call("start");
            if (instance.HasMethod("release"))
            {
                instance.Call("release"); // FMOD frees it once it has finished playing.
            }

            return;
        }

        SfxCmd.Play(fmodEvent);
    }

    internal static void OnMasterVolume(float volume)
    {
        _master = volume;
        ApplyVolume();
    }

    internal static void OnBgmVolume(float volume)
    {
        _bgm = volume;
        ApplyVolume();
    }

    private static void ApplyVolume()
    {
        if (_player is null)
        {
            return;
        }

        // A slider moved mid fade-in: skip the rest of the fade and go straight to the new level.
        _fade?.Kill();
        _fade = null;
        _player.VolumeDb = TargetDb();
    }

    /// <summary>The game squares each slider before handing it to FMOD, and the buses multiply; do the same.</summary>
    private static float TargetDb()
    {
        var settings = SaveManager.Instance?.SettingsSave;
        var master = _master ?? settings?.VolumeMaster ?? 0.5f;
        var bgm = _bgm ?? settings?.VolumeBgm ?? 0.5f;
        var linear = master * master * bgm * bgm * MusicGain;
        return linear <= 0.0001f ? SilentDb : (float)Mathf.LinearToDb(linear);
    }

    private static AudioStream? LoadMusic()
    {
        if (!_musicLoaded)
        {
            _musicLoaded = true;
            if (Godot.FileAccess.FileExists(MusicPath) && AudioStreamOggVorbis.LoadFromFile(MusicPath) is { } ogg)
            {
                ogg.Loop = true;
                _music = ogg;
            }
        }

        return _music;
    }
}

/// <summary>Leaving the shop room the Tavern was in: the Tavern track fades and the act's music returns.</summary>
[HarmonyPatch(typeof(MerchantRoom), nameof(MerchantRoom.Exit))]
[SpireFeature("tavern.music")]
internal static class TavernMusicExitPatch
{
    private static void Postfix() => TavernAudio.StopMusic(restoreActMusic: true);
}

/// <summary>Keeps the Tavern track at the game's master and music volume (including mute in background).</summary>
[HarmonyPatch(typeof(NAudioManager))]
[SpireFeature("tavern.musicVolume")]
internal static class TavernVolumePatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(NAudioManager.SetMasterVol))]
    private static void Master(float volume) => TavernAudio.OnMasterVolume(volume);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NAudioManager.SetBgmVol))]
    private static void Bgm(float volume) => TavernAudio.OnBgmVolume(volume);
}
