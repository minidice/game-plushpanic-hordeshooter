using Godot;
using System;
using System.Linq;

namespace HordeShooter.Tests;

public partial class HunterSounds : Node
{
    public override void _Ready()
    {
        bool failed = false;
        int checks = 0;
        try
        {
            void Check(bool ok, string message)
            {
                if (!ok) throw new Exception(message);
                checks++;
                GD.Print("PASS: " + message);
            }
            HunterSoundChecks.Run(Check);
            foreach (var def in SoundRecipes.All.Where(d => d.Id.StartsWith("hunter_")))
            {
                using var wav = Sfx.Synthesize(def);
                var data = wav.Data;
                int peak = 0;
                for (int i = 0; i < data.Length; i += 2)
                    peak = Math.Max(peak, Math.Abs((int)(short)(data[i] | data[i + 1] << 8)));
                Check(peak is > 3000 and < 30000 && wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled,
                    def.Id + " uses clean non-looping PCM with the runtime seed");
            }
            GD.Print("HUNTER_SOUNDS_PASS " + checks);
        }
        catch (Exception e)
        {
            failed = true;
            GD.PushError(e.ToString());
            GD.Print("HUNTER_SOUNDS_FAIL " + e.Message);
        }
        QuitHygiene.Collect();
        GetTree().Quit(failed ? 1 : 0);
    }
}
