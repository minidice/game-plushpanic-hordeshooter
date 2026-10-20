using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace HordeShooter.Tests;

/// <summary>
/// How loud the synthesized sounds are relative to each other (sound-spec.md): constant or frequent sounds
/// (footsteps, sirens) must stay well under combat cues so they can be noticed without tiring the ear.
/// Level = loudest 50 ms window (RMS) of the samples + the recipe volume. Speakers and the mix bus are not measured.
/// </summary>
public partial class AudioLevelCheck : Node
{
    private int _checks;
    private void Check(bool ok, string message) { if (!ok) throw new Exception(message); _checks++; GD.Print("PASS: " + message); }

    public override async void _Ready()
    {
        bool failed = false;
        try { await Run(); GD.Print("AUDIO_LEVEL_PASS " + _checks); }
        catch (Exception e) { GD.PushError(e.ToString()); GD.Print("AUDIO_LEVEL_FAIL " + e.Message); failed = true; }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }

    private async Task Run()
    {
        var sfx = new Sfx();
        AddChild(sfx);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        float L(string id) => Level(sfx, id);
        foreach (var id in new[] { "shot_rifle", "shot_pistol", "honk", "zombie_hit", "groan", "zombie_step", "step", "alarm_loop", "ambience_loop" })
            GD.Print($"level {id}: {L(id):0.0} dB");

        float rifle = L("shot_rifle"), groan = L("groan"), wind = L("ambience_loop");
        // Sirens: monsters hear them 60 m away; the player only needs to notice (I-62).
        Check(L("alarm_loop") < rifle - 10 && L("alarm_loop") <= groan && L("alarm_loop") > wind,
            "the siren is soft but noticeable: under gunfire and groans, over the night wind");
        // Footsteps: about six a second while walking, right under the listener. Barely audible: under the
        // night wind's peaks, but not lost entirely, so a future surface (water, metal) can still be told apart (I-63).
        Check(L("step") < groan && L("step") > wind, "your own footsteps are under a groan and over the night wind");
        // Zombie shuffles warn of an approaching horde: quieter than a groan, louder than your own feet.
        Check(L("zombie_step") < groan - 6, "zombie shuffles stay a soft warning, under a groan");
        Check(Player.BotStepVolumeDb < 0, "squadmates' footsteps are softer than your own");
        sfx.QueueFree();
    }

    private static float Level(Sfx sfx, string id)
    {
        if (sfx.Stream(id) is not AudioStreamWav wav) return float.NaN;
        var data = wav.Data;
        int n = data.Length / 2, window = Math.Max(1, wav.MixRate / 20);
        double best = 0, sum = 0;
        var squares = new double[n];
        for (int i = 0; i < n; i++)
        {
            double v = (short)(data[2 * i] | data[2 * i + 1] << 8) / 32768.0;
            squares[i] = v * v;
            sum += squares[i];
            if (i >= window) sum -= squares[i - window];
            best = Math.Max(best, sum / Math.Min(i + 1, window));
        }
        return Mathf.LinearToDb((float)Math.Sqrt(best)) + SoundRecipes.All.First(d => d.Id == id).VolumeDb;
    }
}
