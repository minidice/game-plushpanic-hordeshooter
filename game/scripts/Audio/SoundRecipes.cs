using System;
using System.Collections.Generic;
using System.Linq;

namespace HordeShooter;

/// <summary>How a sound is played. <see cref="Build"/> synthesizes it; a file in content/sounds overrides it.</summary>
/// <param name="Priority">0 crowd ambience, 1 normal, 2 must be heard. When the pool is full, a sound may take over a lower-or-equal priority voice.</param>
/// <param name="RestartWhenFull">At its voice limit: true restarts its oldest voice (rapid fire), false skips the new one (crowd pings).</param>
/// <param name="Bus">Mixer bus. Cues that must stay clean (ear ring, stingers) bypass the helium/flash effects on SFX.</param>
/// <param name="NearCap">
/// Positional only: never louder than <paramref name="VolumeDb"/>, however close. Godot's logarithmic falloff adds
/// 20·ln(unit size / distance) — about +30 dB at the listener's feet — so without the cap a quiet recipe plays loud
/// up close (I-63/I-64). Enabled by default so authored and caller gains survive near the listener.
/// </param>
public sealed record SoundDef(
    string Id,
    string Description,
    Func<Synth, float[]> Build,
    float VolumeDb = 0,
    float PitchJitter = .06f,
    int MaxVoices = 4,
    bool Loop = false,
    int Priority = 1,
    bool RestartWhenFull = true,
    string Bus = AudioSetup.SfxBus,
    bool NearCap = true,
    int Variants = 1);

/// <summary>
/// Every sound effect as a synthesis recipe. Ids are what gameplay code asks for; see doc/sound-spec.md.
/// Recipes are deterministic (seeded) so the game always sounds the same.
/// </summary>
public static class SoundRecipes
{
    /// <summary>Finite base draw; gameplay scales playback pitch to the species' actual windup.</summary>
    public const float HunterBowDrawSeconds = 1.2f;

    // Recorded files have headroom; their playback gain must not boost a missing-file synth fallback.
    private static float[] FallbackGain(float[] samples, float db)
    {
        float gain = MathF.Pow(10, db / 20);
        for (int i = 0; i < samples.Length; i++) samples[i] *= gain;
        return samples;
    }

    public static readonly IReadOnlyList<SoundDef> All = new List<SoundDef>
    {
        // ---------- weapons ----------
        new("shot_pistol", "권총 발사", s => Gunshot(s, .13f, 4200, 1500, 22, 150, .35f), -2, .08f, 6, Priority: 2),
        new("shot_smg", "기관단총 발사", s => Gunshot(s, .12f, 3900, 1300, 24, 135, .4f), -2, .08f, 6, Priority: 2),
        new("shot_rifle", "소총 발사", s => FallbackGain(Gunshot(s, .2f, 3400, 900, 16, 110, .55f), -7), 7, .025f, 6, Priority: 2, NearCap: true, Variants: 3),
        new("shot_lmg", "경기관총 발사", s => Gunshot(s, .16f, 2800, 800, 18, 95, .6f), -1, .07f, 6, Priority: 2),
        new("shot_shotgun", "산탄총 발사", s => Gunshot(s, .42f, 2200, 450, 8, 75, 1f), 2, .05f, 4, Priority: 2),
        new("shot_sniper", "저격총 발사", Sniper, 3, .04f, 3, Priority: 2),
        new("rocket_launch", "로켓 발사", RocketLaunch, 1, .05f, 3, Priority: 2),
        new("mortar_fire", "박격포 발사 (통 소리)", MortarFire, 0, .05f, 3),
        new("mortar_whistle", "박격포탄 낙하 휘파람", MortarWhistle, -6, .03f, 4, Priority: 2),
        // Played at the muzzle beside the listener: without the cap it rode the +3 dB ceiling like a gunshot.
        new("melee_swing", "근접 휘두르기", Swing, -1, .1f, 4, NearCap: true),
        new("pan_bong", "프라이팬 타격", PanBong, -1, .08f, 4),
        new("dry_fire", "빈 탄창 딸깍", s => FallbackGain(Click(s, .06f, 2400), -7), 1, .03f, 2, NearCap: true),
        new("reload_start", "재장전 시작 (탄창 빼기)", s => FallbackGain(ReloadStart(s), -6), 0, .025f, 2, NearCap: true),
        new("reload_end", "재장전 끝 (장전 손잡이)", s => FallbackGain(ReloadEnd(s), -6), 1, .025f, 2, NearCap: true),
        new("throw", "투척", s => Whoosh(s, .22f, 600, 2400), -6, .1f, 3),

        // Hunters stay below player weapons even when a crowd attacks at the listener's feet.
        new("hunter_club_raise", "사냥꾼 몽둥이 들기 (나무 삐걱)", HunterClubRaise, -16, .04f, 2, NearCap: true),
        new("hunter_club_swing", "사냥꾼 몽둥이 휘두르기 (묵직한 바람)", s => Whoosh(s, .28f, 180, 1050), -13, .05f, 3, NearCap: true),
        new("hunter_club_hit", "사냥꾼 몽둥이 타격 (둔탁한 퍽)", HunterClubHit, -12, .05f, 3, NearCap: true),
        new("hunter_knife_ready", "사냥꾼 단검 준비 (짧은 손잡이 마찰)", HunterKnifeReady, -17, .04f, 2, NearCap: true),
        new("hunter_knife_swing", "사냥꾼 단검 베기 (빠른 스침)", s => Whoosh(s, .12f, 1900, 6500), -15, .05f, 3, NearCap: true),
        new("hunter_knife_hit", "사냥꾼 단검 타격 (부드러운 베기)", HunterKnifeHit, -14, .05f, 3, NearCap: true),
        new("hunter_bow_draw", "사냥꾼 나무활 당기기 (장력과 삐걱)", HunterBowDraw, -16, 0, 3, NearCap: true),
        new("hunter_bow_release", "사냥꾼 나무활 발사 (활시위 튕김)", HunterBowRelease, -13, .03f, 3, NearCap: true),
        new("hunter_bow_hit", "사냥꾼 화살 타격 (촉과 나무 진동)", HunterBowHit, -14, .05f, 3, NearCap: true),

        // ---------- explosions & throwables ----------
        // Large blasts add +2 at the call site; keep that maximum at the former +3 near ceiling.
        new("explosion", "폭발", Explosion, 1, .08f, 5, Priority: 2),
        new("glass_break", "화염병 깨짐", GlassBreak, -1, .08f, 3),
        new("fire_loop", "불타는 소리 (반복)", FireLoop, -4, 0, 4, Loop: true, Priority: 0, RestartWhenFull: false),
        new("flash_bang", "섬광탄 터짐", FlashBang, 3, .03f, 2),
        new("ear_ring", "귀 울림 (섬광 실명)", EarRing, -8, 0, 1, Priority: 2, Bus: AudioSetup.CueBus),
        new("lure_beep", "유인 폭탄 삑", s => Beep(s, 1760, .08f), -4, 0, 4, Priority: 2),

        // ---------- player ----------
        // About six a second right under the listener: present but under the swing (I-63). A bare 150 Hz thump
        // vanished on small speakers even when loud, so it carries a short mid-range scuff.
        new("step", "플레이어 발소리 (발바닥 툭 + 스침)", PlayerStep, -12, .12f, 3, NearCap: true),
        new("roll", "구르기", Roll, -4, .08f, 2),
        // Played at the listener's own head: it rode the +3 dB ceiling as loud as a rifle shot. Half as loud now (I-74).
        new("honk", "피격 (짧은 거위 반응)", s => Honk(s, .28f, 330, 260), -1, .04f, 2, Priority: 2, NearCap: true, Variants: 2),
        new("honk_ability", "무기·카드 경적", s => Honk(s, .28f, 330, 260), -3, .1f, 2, Priority: 2, NearCap: true),
        new("honk_long", "쓰러질 때 긴 거위 반응", s => Honk(s, .9f, 360, 180), -2, .03f, 1, Priority: 2, NearCap: true),
        new("heal", "치료 완료", s => Chime(s, new[] { 660f, 880f, 1320f }, .6f, .08f), -6, 0, 1),
        new("pickup", "줍기", s => Chime(s, new[] { 660f, 990f }, .16f, .05f), -8, .03f, 2),

        // ---------- zombies & specials ----------
        new("zombie_step", "좀비 발소리 (질질, 작게)", ZombieStep, -17, .15f, 6, Priority: 0, RestartWhenFull: false, NearCap: true),
        new("groan", "좀비 신음 (목 울림)", Groan, 2, .05f, 3, Priority: 0, RestartWhenFull: false, NearCap: true, Variants: 2),
        new("zombie_hit", "좀비 피격 (짧은 접촉)", s => FallbackGain(ZombieHit(s), -4), -2, .05f, 3, Priority: 0, RestartWhenFull: false, NearCap: true, Variants: 3),
        new("zombie_die", "좀비 쓰러짐 (짧은 발성·낙하)", ZombieDie, -4, .05f, 3, Priority: 0, RestartWhenFull: false, NearCap: true, Variants: 2),
        new("sonar", "소나 파문 (보이지 않는 적)", Sonar, -10, .04f, 4, Priority: 2, RestartWhenFull: false),
        new("balloon_inflate", "풍선 부풂 (공기 압력)", BalloonInflate, 0, .03f, 2, NearCap: true),
        new("balloon_pop", "풍선 터짐 (공기 파열)", BalloonPop, 0, .04f, 3, NearCap: true),
        // Boss telegraphs add up to +6; preserve that distinction without raising the old +3 cap.
        new("bull_snort", "황소 콧김 (돌진 예고)", BullSnort, -3, .06f, 2, Priority: 2),
        new("bull_bonk", "황소 충돌 (둔탁한 타격)", BullBonk, 1, .04f, 3, NearCap: true),

        // ---------- stage ----------
        new("door", "문 미끄러짐", Door, -4, .04f, 2),
        // Heard, not blaring: monsters hear it 60 m away (spawn_rules.json), the player only needs to notice it (I-62).
        new("alarm_loop", "경보 사이렌 (반복, 작게)", Alarm, -20, 0, 1, Loop: true, NearCap: true),
        new("shutter", "셔터 열림", Shutter, -2, 0, 1),
        new("ambience_loop", "밤바람 앰비언스 (반복)", Ambience, -16, 0, 1, Loop: true, Priority: 0, RestartWhenFull: false),
        new("game_over", "게임 오버 (짧은 하강 악구)", SadTrombone, -2, 0, 1, Priority: 2, Bus: AudioSetup.CueBus),
        new("stage_clear", "스테이지 클리어 (부드러운 상승 악구)", Fanfare, -3, 0, 1, Priority: 2, Bus: AudioSetup.CueBus),
        new("ui_click", "UI 클릭", s => Click(s, .05f, 1600), -8, .03f, 2, Bus: AudioSetup.CueBus),
    }.Concat(SurfaceDefinitions()).ToArray();

    private static IEnumerable<SoundDef> SurfaceDefinitions()
    {
        foreach (string family in new[] { "stone", "metal", "wood", "soft", "soil", "gravel", "grass" })
        {
            if (family != "stone")
                yield return new("surface_step_" + family, family + " 발소리", s => FallbackGain(PlayerStep(s), -2),
                    -10, .06f, 3, Priority: 0, RestartWhenFull: false, NearCap: true, Variants: 2);
            yield return new("surface_hit_" + family, family + " 표면 명중", s => FallbackGain(ZombieHit(s), -6),
                -3, .05f, 3, Priority: 0, RestartWhenFull: false, NearCap: true, Variants: 2);
            if (family is "stone" or "metal" or "wood" or "soft")
                yield return new("surface_break_" + family, family + " 소품 파손", s => FallbackGain(Roll(s), -5),
                    0, .04f, 2, Priority: 0, RestartWhenFull: false, NearCap: true, Variants: 2);
        }
    }

    public static SoundDef? Find(string id) => All.FirstOrDefault(d => d.Id == id);

    // ---------- recipes ----------

    private static float[] Gunshot(Synth s, float length, float bright, float dark, float decay, float thump, float body)
    {
        var buf = Synth.Buffer(length);
        s.Noise(buf, 1);
        Synth.LowPass(buf, t => dark + (bright - dark) * MathF.Exp(-t * 30));
        Synth.Envelope(buf, .001f, decay);
        Synth.Tone(buf, Synth.Glide(thump, thump * .45f, length * .6f), Wave.Sine, body, 0, length * .7f);
        Synth.Envelope(buf, 0, decay * .5f);
        Synth.Drive(buf, 2.2f);
        return Synth.Declick(Synth.Normalize(buf));
    }

    private static float[] Sniper(Synth s)
    {
        var crack = Synth.Buffer(.06f);
        s.Noise(crack, 1);
        Synth.HighPass(crack, 2000);
        Synth.Envelope(crack, 0, 70);
        var boom = Gunshot(s, .9f, 2600, 300, 5, 70, .9f);
        Synth.Mix(boom, crack, .9f);
        return Synth.Declick(Synth.Normalize(boom));
    }

    private static float[] RocketLaunch(Synth s)
    {
        var buf = Synth.Buffer(.75f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, t => 300 + 3200 * MathF.Min(1, t / .25f));
        Synth.Shape(buf, t => MathF.Min(1, t / .04f) * MathF.Exp(-t * 3.5f));
        var pop = Gunshot(s, .2f, 2000, 500, 14, 90, .7f);
        Synth.Mix(buf, pop, .8f);
        return Synth.Declick(Synth.Normalize(buf));
    }

    private static float[] MortarFire(Synth s)
    {
        var buf = Synth.Buffer(.4f);
        Synth.Tone(buf, Synth.Glide(95, 38, .3f), Wave.Sine, 1);
        s.Noise(buf, .5f, 0, .07f);
        Synth.LowPass(buf, 900);
        Synth.Envelope(buf, .002f, 9);
        Synth.Drive(buf, 2);
        return Synth.Declick(Synth.Normalize(buf));
    }

    private static float[] MortarWhistle(Synth s)
    {
        var buf = Synth.Buffer(1.25f);
        Synth.Tone(buf, Synth.Vibrato(Synth.Glide(1900, 520, 1.25f), 7, .01f), Wave.Sine, 1);
        s.Noise(buf, .08f);
        Synth.Shape(buf, t => MathF.Min(1, t / .2f) * (t > 1.15f ? (1.25f - t) / .1f : 1));
        return Synth.Declick(Synth.Normalize(buf, .7f));
    }

    private static float[] Swing(Synth s) => Whoosh(s, .2f, 500, 3000);

    private static float[] HunterClubRaise(Synth s)
    {
        var buf = WoodCreak(s, .36f, 125, 205);
        Synth.Mix(buf, Whoosh(s, .22f, 250, 950), .2f, .05f);
        return Synth.Declick(Synth.Normalize(buf, .7f));
    }

    private static float[] WoodCreak(Synth s, float length, float from, float to)
    {
        var buf = Synth.Buffer(length);
        Synth.Tone(buf, Synth.Vibrato(Synth.Glide(from, to, length), 19, .08f), Wave.Saw, .45f);
        s.Noise(buf, .45f);
        Synth.LowPass(buf, t => 420 + 650 * t / length);
        Synth.HighPass(buf, 100);
        Synth.Shape(buf, t => MathF.Sin(MathF.PI * t / length) *
            (.62f + .22f * MathF.Sin(MathF.Tau * 23 * t) + .16f * MathF.Sin(MathF.Tau * 37 * t)));
        return Synth.Declick(Synth.Normalize(buf, .65f));
    }

    private static float[] HunterClubHit(Synth s)
    {
        var buf = Synth.Buffer(.30f);
        Synth.Tone(buf, Synth.Glide(145, 58, .20f), Wave.Sine, 1);
        Synth.Tone(buf, 330, Wave.Triangle, .22f);
        s.Noise(buf, .7f);
        Synth.LowPass(buf, 950);
        Synth.Envelope(buf, .002f, 22);
        return Synth.Declick(Synth.Normalize(buf, .78f));
    }

    private static float[] HunterKnifeReady(Synth s)
    {
        var buf = Whoosh(s, .10f, 850, 2600);
        Synth.Mix(buf, Click(s, .035f, 1700), .15f);
        return Synth.Declick(Synth.Normalize(buf, .6f));
    }

    private static float[] HunterKnifeHit(Synth s)
    {
        var buf = Synth.Buffer(.13f);
        s.Noise(buf, 1);
        Synth.HighPass(buf, 1100);
        Synth.LowPass(buf, 4200);
        Synth.Envelope(buf, .003f, 42);
        var body = Synth.Buffer(.09f);
        Synth.Tone(body, Synth.Glide(210, 115, .09f), Wave.Sine, .3f);
        Synth.Envelope(body, .002f, 40);
        Synth.Mix(buf, body);
        return Synth.Declick(Synth.Normalize(buf, .7f));
    }

    private static float[] HunterBowDraw(Synth s)
    {
        var buf = WoodCreak(s, HunterBowDrawSeconds, 95, 270);
        var tension = Synth.Buffer(HunterBowDrawSeconds);
        Synth.Tone(tension, Synth.Vibrato(Synth.Glide(165, 360, HunterBowDrawSeconds), 7, .018f), Wave.Triangle, .25f);
        Synth.Shape(tension, t => MathF.Min(1, t / .20f) * MathF.Min(1, (HunterBowDrawSeconds - t) / .10f) *
            (.3f + .7f * t / HunterBowDrawSeconds));
        Synth.Mix(buf, tension);
        return Synth.Declick(Synth.Normalize(buf, .65f), .012f);
    }

    private static float[] HunterBowRelease(Synth s)
    {
        var buf = Synth.Buffer(.32f);
        // Decaying harmonics give a dry string pluck, without a metal bell's long inharmonic ring.
        for (int harmonic = 1; harmonic <= 4; harmonic++)
        {
            var partial = Synth.Buffer(.32f);
            Synth.Tone(partial, Synth.Glide(240 * harmonic, 205 * harmonic, .12f), Wave.Sine, 1f / harmonic);
            Synth.Envelope(partial, .001f, 14 + harmonic * 6);
            Synth.Mix(buf, partial);
        }
        Synth.Mix(buf, Whoosh(s, .08f, 1800, 5000), .16f);
        return Synth.Declick(Synth.Normalize(buf, .75f));
    }

    private static float[] HunterBowHit(Synth s)
    {
        var buf = Synth.Buffer(.20f);
        s.Noise(buf, .7f);
        Synth.LowPass(buf, 2400);
        Synth.Envelope(buf, .001f, 48);
        var shaft = Synth.Buffer(.18f);
        Synth.Tone(shaft, Synth.Glide(460, 310, .18f), Wave.Triangle, .45f);
        Synth.Tone(shaft, 165, Wave.Sine, .3f);
        Synth.Envelope(shaft, .002f, 27);
        Synth.Mix(buf, shaft);
        return Synth.Declick(Synth.Normalize(buf, .72f));
    }

    private static float[] Whoosh(Synth s, float length, float lo, float hi)
    {
        var buf = Synth.Buffer(length);
        s.Noise(buf, 1);
        Synth.LowPass(buf, t => lo + (hi - lo) * MathF.Sin(MathF.PI * MathF.Min(1, t / length)));
        Synth.HighPass(buf, lo * .5f);
        Synth.Shape(buf, t => MathF.Sin(MathF.PI * MathF.Min(1, t / length)));
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }

    private static float[] PanBong(Synth s)
    {
        var buf = Synth.Buffer(1.3f);
        // Inharmonic partials of a struck metal plate, high ones dying first.
        (float f, float a, float d)[] partials = { (523, 1, 3), (1307, .6f, 5), (2151, .45f, 8), (3274, .3f, 12), (4410, .2f, 16) };
        foreach (var (f, a, d) in partials)
        {
            var p = Synth.Buffer(1.3f);
            Synth.Tone(p, f, Wave.Sine, a);
            Synth.Envelope(p, .001f, d);
            Synth.Mix(buf, p);
        }
        s.Noise(buf, .5f, 0, .02f);
        return Synth.Declick(Synth.Normalize(buf));
    }

    private static float[] Click(Synth s, float length, float freq)
    {
        var buf = Synth.Buffer(length);
        s.Noise(buf, .6f, 0, .01f);
        Synth.Tone(buf, freq, Wave.Square, .4f, 0, .012f);
        Synth.HighPass(buf, 800);
        Synth.Envelope(buf, 0, 120);
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }

    private static float[] ReloadStart(Synth s)
    {
        var buf = Synth.Buffer(.32f);
        Synth.Mix(buf, Click(s, .06f, 1900), .9f);
        var slide = Synth.Buffer(.15f);
        s.Noise(slide, .4f);
        Synth.LowPass(slide, 1500);
        Synth.Envelope(slide, .02f, 14);
        Synth.Mix(buf, slide, 1, .08f);
        Synth.Mix(buf, Click(s, .06f, 1200), .6f, .22f);
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    private static float[] ReloadEnd(Synth s)
    {
        var buf = Synth.Buffer(.25f);
        Synth.Mix(buf, Click(s, .06f, 2600), 1);
        Synth.Mix(buf, Click(s, .06f, 1800), .8f, .11f);
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    private static float[] Chime(Synth s, float[] notes, float length, float stagger = 0)
    {
        var buf = Synth.Buffer(length + stagger * notes.Length);
        for (int i = 0; i < notes.Length; i++)
        {
            var n = Synth.Buffer(length);
            Synth.Tone(n, notes[i], Wave.Sine, 1);
            Synth.Tone(n, notes[i] * 2, Wave.Sine, .25f);
            Synth.Envelope(n, .005f, 6);
            Synth.Mix(buf, n, 1, i * stagger);
        }
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }

    private static float[] Explosion(Synth s)
    {
        var buf = Synth.Buffer(1.6f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, t => 160 + 1400 * MathF.Exp(-t * 6));
        Synth.Envelope(buf, .004f, 2.6f);
        var sub = Synth.Buffer(1.0f);
        Synth.Tone(sub, Synth.Glide(62, 28, .8f), Wave.Sine, 1);
        Synth.Envelope(sub, .004f, 3.5f);
        Synth.Mix(buf, sub, .9f);
        s.Crackle(buf, .25f, 30);
        Synth.Drive(buf, 2.5f);
        return Synth.Declick(Synth.Normalize(buf, .95f));
    }

    private static float[] GlassBreak(Synth s)
    {
        var buf = Synth.Buffer(.6f);
        s.Noise(buf, .8f);
        Synth.HighPass(buf, 2500);
        Synth.Envelope(buf, 0, 12);
        var rnd = new Random(7);
        for (int i = 0; i < 9; i++)
        {
            var tink = Synth.Buffer(.25f);
            Synth.Tone(tink, 2500 + (float)rnd.NextDouble() * 4500, Wave.Sine, .4f);
            Synth.Envelope(tink, 0, 22);
            Synth.Mix(buf, tink, 1, (float)rnd.NextDouble() * .3f);
        }
        var whump = Synth.Buffer(.5f);
        s.Noise(whump, 1);
        Synth.LowPass(whump, 400);
        Synth.Envelope(whump, .03f, 5);
        Synth.Mix(buf, whump, .7f, .05f);
        return Synth.Declick(Synth.Normalize(buf));
    }

    private static float[] FireLoop(Synth s)
    {
        var buf = Synth.Buffer(2.2f);
        s.Noise(buf, .5f);
        Synth.LowPass(buf, 380);
        Synth.Shape(buf, t => .75f + .25f * MathF.Sin(MathF.Tau * 1.3f * t) * MathF.Sin(MathF.Tau * .45f * t));
        s.Crackle(buf, .5f, 22);
        return Synth.MakeLoop(Synth.Normalize(buf, .8f), .2f);
    }

    private static float[] FlashBang(Synth s)
    {
        var buf = Synth.Buffer(.7f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, t => 900 + 6000 * MathF.Exp(-t * 20));
        Synth.Envelope(buf, 0, 7);
        Synth.Drive(buf, 3);
        return Synth.Declick(Synth.Normalize(buf, .95f));
    }

    private static float[] EarRing(Synth s)
    {
        var buf = Synth.Buffer(2.6f);
        Synth.Tone(buf, 3600, Wave.Sine, .7f);
        Synth.Tone(buf, 3612, Wave.Sine, .3f);
        Synth.Shape(buf, t => MathF.Min(1, t / .05f) * MathF.Exp(-t * 1.1f));
        return Synth.Declick(Synth.Normalize(buf, .6f), .05f);
    }

    private static float[] Beep(Synth s, float freq, float length)
    {
        var buf = Synth.Buffer(length);
        Synth.Tone(buf, freq, Wave.Square, .6f);
        Synth.Tone(buf, freq * 1.5f, Wave.Sine, .3f);
        Synth.LowPass(buf, 5000);
        Synth.Shape(buf, t => MathF.Min(1, t / .004f) * MathF.Min(1, (length - t) / .01f));
        return Synth.Normalize(buf, .8f);
    }

    private static float[] Step(Synth s, float pitch, float length)
    {
        var buf = Synth.Buffer(length);
        Synth.Tone(buf, Synth.Glide(pitch, pitch * .55f, length), Wave.Sine, 1);
        s.Noise(buf, .5f);
        Synth.LowPass(buf, 700);
        Synth.Envelope(buf, .002f, 45);
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }

    private static float[] Roll(Synth s)
    {
        var buf = Whoosh(s, .38f, 300, 1800);
        var thump = Step(s, 110, .1f);
        Synth.Mix(buf, thump, .9f, .3f);
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    /// <summary>Goose honk: nasal sawtooth with a pitch bend and a little flutter.</summary>
    private static float[] Honk(Synth s, float length, float f0, float f1)
    {
        var buf = Synth.Buffer(length);
        Synth.Tone(buf, Synth.Vibrato(Synth.Glide(f0, f1, length), 18, .025f), Wave.Saw, 1);
        Synth.Tone(buf, Synth.Vibrato(Synth.Glide(f0 * 2.01f, f1 * 2.01f, length), 18, .025f), Wave.Square, .25f);
        Synth.LowPass(buf, 1800);
        Synth.Shape(buf, t => MathF.Min(1, t / .02f) * MathF.Min(1, (length - t) / .06f));
        Synth.Drive(buf, 1.8f);
        Synth.HighPass(buf, 350);
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    /// <summary>The low pat (felt) plus a 0.9–3.2 kHz scuff (heard, even on laptop speakers).</summary>
    private static float[] PlayerStep(Synth s)
    {
        var buf = Step(s, 150, .07f);
        var scuff = Synth.Buffer(.06f);
        s.Noise(scuff, 1);
        Synth.HighPass(scuff, 900);
        Synth.LowPass(scuff, 3200);
        Synth.Envelope(scuff, .002f, 70);
        Synth.Mix(buf, Synth.Normalize(scuff, .8f), .55f);
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }

    private static float[] ZombieStep(Synth s)
    {
        var buf = Synth.Buffer(.22f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, t => 900 - 2500 * t);
        Synth.Shape(buf, t => MathF.Sin(MathF.PI * MathF.Min(1, t / .22f)) * (1 - t * 2));
        Synth.Mix(buf, Step(s, 95, .08f), .7f);
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }

    private static float[] Groan(Synth s)
    {
        var buf = Synth.Buffer(1.1f);
        var pitch = Synth.Vibrato(t => 105 - 25 * t, 4.5f, .05f);
        Synth.Tone(buf, pitch, Wave.Saw, 1);
        Synth.Tone(buf, t => pitch(t) * 1.5f, Wave.Saw, .3f);
        s.Noise(buf, .2f);
        Synth.LowPass(buf, t => 500 + 400 * MathF.Sin(MathF.PI * MathF.Min(1, t / 1.1f)));
        Synth.Shape(buf, t => MathF.Sin(MathF.PI * MathF.Min(1, t / 1.1f)));
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    private static float[] ZombieHit(Synth s)
    {
        var buf = Synth.Buffer(.14f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, 1400);
        Synth.Envelope(buf, .002f, 30);
        Synth.Tone(buf, Synth.Glide(800, 1300, .08f), Wave.Sine, .35f, 0, .08f);
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }

    private static float[] ZombieDie(Synth s)
    {
        var buf = Synth.Buffer(.32f);
        s.Noise(buf, 1, 0, .05f);
        Synth.LowPass(buf, 2200);
        Synth.Envelope(buf, 0, 40, 0, .05f);
        var squeak = Synth.Buffer(.25f);
        Synth.Tone(squeak, Synth.Vibrato(Synth.Glide(1300, 520, .25f), 25, .04f), Wave.Triangle, .7f);
        Synth.Envelope(squeak, .01f, 8);
        Synth.Mix(buf, squeak, 1, .04f);
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    private static float[] Sonar(Synth s)
    {
        // Soft submarine "pong" with a short echo: the sound of things you cannot see.
        var buf = Synth.Buffer(.9f);
        var ping = Synth.Buffer(.6f);
        Synth.Tone(ping, Synth.Glide(620, 590, .6f), Wave.Sine, 1);
        Synth.Tone(ping, Synth.Glide(1240, 1180, .6f), Wave.Sine, .15f);
        Synth.Envelope(ping, .006f, 7);
        Synth.Mix(buf, ping, 1);
        Synth.Mix(buf, ping, .35f, .22f);
        return Synth.Declick(Synth.Normalize(buf, .75f));
    }

    private static float[] BalloonInflate(Synth s)
    {
        var buf = Synth.Buffer(.7f);
        Synth.Tone(buf, Synth.Vibrato(Synth.Glide(380, 1250, .7f), 16, .06f), Wave.Triangle, 1);
        s.Noise(buf, .15f);
        Synth.LowPass(buf, 3000);
        Synth.Shape(buf, t => MathF.Min(1, t / .05f));
        return Synth.Declick(Synth.Normalize(buf, .75f));
    }

    private static float[] BalloonPop(Synth s)
    {
        var buf = Synth.Buffer(.35f);
        s.Noise(buf, 1);
        Synth.HighPass(buf, 600);
        Synth.Envelope(buf, 0, 30);
        Synth.Tone(buf, Synth.Glide(180, 60, .2f), Wave.Sine, .8f, 0, .2f);
        Synth.Drive(buf, 3);
        return Synth.Declick(Synth.Normalize(buf, .95f));
    }

    private static float[] BullSnort(Synth s)
    {
        var buf = Synth.Buffer(.85f);
        for (int i = 0; i < 2; i++)
        {
            var snort = Synth.Buffer(.32f);
            s.Noise(snort, 1);
            Synth.LowPass(snort, 650);
            Synth.Tone(snort, Synth.Glide(90, 70, .3f), Wave.Saw, .5f);
            Synth.Shape(snort, t => MathF.Sin(MathF.PI * MathF.Min(1, t / .32f)));
            Synth.Mix(buf, snort, i == 0 ? .8f : 1f, i * .42f);
        }
        return Synth.Declick(Synth.Normalize(buf));
    }

    private static float[] BullBonk(Synth s)
    {
        var buf = Synth.Buffer(.5f);
        Synth.Tone(buf, Synth.Glide(140, 55, .3f), Wave.Sine, 1);
        s.Noise(buf, .7f, 0, .06f);
        Synth.LowPass(buf, 1200);
        Synth.Envelope(buf, .001f, 8);
        var knock = Synth.Buffer(.2f);
        Synth.Tone(knock, 420, Wave.Triangle, .7f);
        Synth.Envelope(knock, 0, 30);
        Synth.Mix(buf, knock, .8f);
        Synth.Drive(buf, 2);
        return Synth.Declick(Synth.Normalize(buf, .95f));
    }

    private static float[] Door(Synth s)
    {
        var buf = Synth.Buffer(.7f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, 420);
        Synth.Tone(buf, Synth.Vibrato(_ => 85, 11, .1f), Wave.Saw, .3f);
        Synth.Shape(buf, t => MathF.Min(1, t / .05f) * MathF.Exp(-t * 3));
        Synth.Mix(buf, Click(s, .06f, 700), .8f, .62f);
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    private static float[] Alarm(Synth s)
    {
        var buf = Synth.Buffer(1.6f);
        Synth.Tone(buf, t => 700 + 260 * MathF.Sin(MathF.Tau * t / 1.6f - MathF.PI / 2), Wave.Square, .6f);
        Synth.LowPass(buf, 2500);
        return Synth.Normalize(buf, .7f);
    }

    private static float[] Shutter(Synth s)
    {
        var buf = Synth.Buffer(2.2f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, 260);
        Synth.Tone(buf, Synth.Vibrato(_ => 55, 9, .2f), Wave.Saw, .4f);
        s.Crackle(buf, .3f, 18);
        Synth.Shape(buf, t => MathF.Min(1, t / .2f) * MathF.Min(1, (2.2f - t) / .5f));
        return Synth.Declick(Synth.Normalize(buf));
    }

    private static float[] Ambience(Synth s)
    {
        var buf = Synth.Buffer(7f);
        s.Noise(buf, 1);
        Synth.LowPass(buf, t => 160 + 110 * MathF.Sin(MathF.Tau * t / 7f) + 40 * MathF.Sin(MathF.Tau * t / 2.3f));
        Synth.Tone(buf, Synth.Vibrato(_ => 55, .2f, .02f), Wave.Sine, .12f);
        Synth.Shape(buf, t => .7f + .3f * MathF.Sin(MathF.Tau * t / 3.5f));
        return Synth.MakeLoop(Synth.Normalize(buf, .8f), .8f);
    }

    /// <summary>"Wah wah wah wahhh" — the slapstick game over.</summary>
    private static float[] SadTrombone(Synth s)
    {
        float[] notes = { 311, 294, 277, 262 };
        float[] lengths = { .35f, .35f, .35f, 1.2f };
        var buf = Synth.Buffer(2.4f);
        float at = 0;
        for (int i = 0; i < notes.Length; i++)
        {
            float len = lengths[i];
            var n = Synth.Buffer(len);
            var f = i == 3 ? Synth.Vibrato(_ => notes[i], 5.5f, .025f) : (Func<float, float>)(_ => notes[i]);
            Synth.Tone(n, f, Wave.Saw, 1);
            Synth.LowPass(n, t => 500 + 1500 * MathF.Min(1, t / .08f));
            Synth.Shape(n, t => MathF.Min(1, t / .03f) * MathF.Min(1, (len - t) / .08f));
            Synth.Mix(buf, n, 1, at);
            at += len + .02f;
        }
        return Synth.Declick(Synth.Normalize(buf, .85f));
    }

    private static float[] Fanfare(Synth s)
    {
        float[] notes = { 523, 659, 784, 1047 };
        var buf = Synth.Buffer(1.6f);
        for (int i = 0; i < notes.Length; i++)
        {
            float len = i == 3 ? .9f : .18f;
            var n = Synth.Buffer(len);
            Synth.Tone(n, notes[i], Wave.Square, .5f);
            Synth.Tone(n, notes[i] * 2, Wave.Sine, .3f);
            Synth.LowPass(n, 3500);
            Synth.Shape(n, t => MathF.Min(1, t / .01f) * MathF.Min(1, (len - t) / .05f));
            Synth.Mix(buf, n, 1, i * .17f);
        }
        return Synth.Declick(Synth.Normalize(buf, .8f));
    }
}
