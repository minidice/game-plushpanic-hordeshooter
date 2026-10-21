using Godot;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Big-monster body motions picked by name from the boss data (content/data/bosses/&lt;id&gt;.json, "pose" of a skill or
/// phase; doc/mode-weapon-direction.md 8.1). Each one is laid on top of the frame's normal animation from the parts
/// every model has (whole body, head, feet) and its arm pivots (boss_leftArm / boss_rightArm), so new bosses reuse
/// them without new code.
/// </summary>
public partial class CharacterRig
{
    /// <summary>Every pose a boss file may name.</summary>
    public static readonly IReadOnlySet<string> BossPoses = new HashSet<string>
    {
        "crouch_leap", "land", "rear_roar", "sweep_aim", "recoil", "shake_off", "paw_ground", "gallop",
    };

    /// <summary>
    /// Adds pose <paramref name="name"/> at <paramref name="t"/> (0 → 1 through the move; for "gallop" it counts
    /// strides). <paramref name="lift"/> raises the body off the ground (a leap). An empty name does nothing.
    /// </summary>
    public void ApplyBossPose(string name, float t, float lift = 0)
    {
        if (name == "") return;
        if (!_enemyPoseInitialized) InitializeEnemyAttackPose();
        float h = Definition.ColliderHeight;
        float bell = Mathf.Sin(Mathf.Pi * Mathf.Clamp(t, 0, 1));
        switch (name)
        {
            case "crouch_leap":
            {
                // Sink down while both arms go up over the head, then spring up stretched.
                float crouch = t < .55f ? Mathf.SmoothStep(0, 1, t / .55f) : 1 - Mathf.SmoothStep(0, 1, (t - .55f) / .1f);
                float arms = Mathf.SmoothStep(0, 1, Mathf.Clamp((t - .1f) / .45f, 0, 1));
                Pose.Position += Vector3.Up * (lift - crouch * .1f * h);
                Pose.Rotation += new Vector3(-.22f * arms, 0, 0);
                BossSquash(crouch * .35f - (lift > .05f ? .18f : 0));
                BossArms(-2.6f * arms, .25f * arms);
                break;
            }
            case "land":
            {
                // The smash: arms slam down in front, the body flattens and comes back up.
                float hit = 1 - Mathf.Clamp(t, 0, 1);
                Pose.Rotation += new Vector3(.35f * hit, 0, 0);
                BossSquash(.55f * hit);
                BossArms(-.5f * hit, -.15f * hit);
                break;
            }
            case "rear_roar":
                // Rear back, head up, arms flung wide, chest puffed, trembling at the top.
                Pose.Rotation += new Vector3(-.45f * bell, 0, Mathf.Sin(t * 80) * .05f * bell);
                Pose.Position += Vector3.Up * .05f * h * bell;
                if (Head != null) Head.Rotation += new Vector3(-.5f * bell * _forwardSign, 0, 0);
                BossArms(-1.7f * bell, 1.1f * bell);
                BossSquash(-.25f * bell);
                break;
            case "sweep_aim":
                // Twist side to side along the fan with the arms out in front, leaning in.
                Pose.Rotation += new Vector3(.12f * bell, Mathf.Sin(t * Mathf.Tau) * .45f, 0);
                BossArms(-1.3f * Mathf.Min(1, t * 4), 0);
                break;
            case "recoil":
            {
                float back = 1 - Mathf.Clamp(t, 0, 1);
                Pose.Rotation += new Vector3(-.4f * back, 0, Mathf.Sin(t * 30) * .08f * back);
                Pose.Position += new Vector3(0, .03f * h * back, -.12f * h * back * _forwardSign);
                BossArms(.4f * back, .3f * back);
                break;
            }
            case "shake_off":
            {
                // A wet-dog shake that dies down.
                float left = 1 - Mathf.Clamp(t, 0, 1);
                Pose.Rotation += new Vector3(-.15f * bell, Mathf.Sin(t * 50) * .12f * left, Mathf.Sin(t * 45) * .22f * left);
                if (Head != null) Head.Rotation += new Vector3(0, Mathf.Sin(t * 55) * .4f * left, 0);
                BossSquash(Mathf.Sin(t * 30) * .12f * left);
                BossArms(-.6f * bell, .5f * bell);
                break;
            }
            case "paw_ground":
            {
                // Scrape a front foot three times, head low, rocking with each scrape.
                float scrape = Mathf.Sin(t * Mathf.Tau * 3);
                Pose.Position += Vector3.Up * Mathf.Abs(scrape) * .03f * h;
                Pose.Rotation += new Vector3(.12f + scrape * .05f, 0, 0);
                RightFoot.Rotation += new Vector3(scrape * .7f * _forwardSign, 0, 0);
                RightFoot.Position += new Vector3(0, Mathf.Max(0, scrape) * .08f, -scrape * .06f * _forwardSign) / Mathf.Max(Definition.ModelScale, .001f);
                break;
            }
            case "gallop":
            {
                // Big bouncing strides: up and down, rocking nose to tail, feet thrown back and forth.
                float stride = Mathf.Sin(t * Mathf.Tau);
                Pose.Position += Vector3.Up * Mathf.Abs(stride) * .09f * h;
                Pose.Rotation += new Vector3(stride * .15f, 0, 0);
                LeftFoot.Rotation += new Vector3(stride * .8f * _forwardSign, 0, 0);
                RightFoot.Rotation += new Vector3(-stride * .8f * _forwardSign, 0, 0);
                BossArms(stride * .5f, .1f);
                break;
            }
        }
    }

    /// <summary>Both arm pivots: <paramref name="raise"/> swings them up (negative) or down, <paramref name="spread"/> out sideways.</summary>
    private void BossArms(float raise, float spread)
    {
        foreach (var arm in _enemyArms)
        {
            float sign = arm.Node.GetParent() == Pose ? 1 : _forwardSign;
            arm.Node.Rotation += new Vector3(raise * sign, 0, arm.Side * spread);
        }
    }

    /// <summary>Squash (positive) or stretch (negative) on top of the frame's own body scale.</summary>
    private void BossSquash(float amount)
    {
        amount = Mathf.Clamp(amount, -.5f, .8f);
        Deform.Scale *= new Vector3(1 + amount * .3f, 1 - amount * .5f, 1 + amount * .3f);
    }
}
