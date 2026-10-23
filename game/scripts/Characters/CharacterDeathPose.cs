using Godot;
using System.Collections.Generic;

namespace HordeShooter;

public partial class CharacterRig
{
    internal enum DeathShape { Upright, Bull, Heavy, Round }

    /// <summary>Named death only: object-pivot animation, independent of the fixed corpse record.</summary>
    internal sealed class DeathPose
    {
        private readonly CharacterRig _rig;
        private readonly Dictionary<Node3D, Transform3D> _start = new(), _home = new();
        private readonly List<(Node3D Node, float Side, bool Leg)> _limbs = new();
        private readonly Node3D? _tail;
        public readonly DeathShape Shape;
        private readonly float _side;
        public float Duration => Shape == DeathShape.Heavy ? Effects.NamedDeathMaxSeconds : Shape == DeathShape.Round ? .74f : .7f;

        public DeathPose(CharacterRig rig, string monsterId, float side)
        {
            _rig = rig; _side = side;
            Shape = monsterId switch
            {
                "bull" or "boss_bull_giant" => DeathShape.Bull,
                "boss" => DeathShape.Heavy,
                "balloon" or "spitter" or "boss_balloon_king" or "boss_spitter_mother" => DeathShape.Round,
                _ => DeathShape.Upright,
            };
            Add(rig.Pose); Add(rig.Body); Add(rig.Deform); Add(rig.Head); Add(rig.LeftFoot); Add(rig.RightFoot); Add(rig.Weapon);
            foreach (var child in rig.Model.FindChildren("*", nameof(Node3D), true, false))
            {
                var node = (Node3D)child; string name = node.Name.ToString();
                bool left = name.EndsWith("_leftArm") || name.EndsWith("_leftLeg");
                bool right = name.EndsWith("_rightArm") || name.EndsWith("_rightLeg");
                if (left || right) { Add(node); _limbs.Add((node, left ? -1 : 1, name.EndsWith("Leg"))); }
                if (name == "bull_tail") { _tail = node; Add(node); }
            }
            // Corpse arms no longer aim at a live grip. Restore their real rest meshes so
            // their geometry and the ground support calculation use the same surface.
            if (rig._gripArms != null && rig.HasAnimalArms)
            {
                rig._gripArms.Visible = false;
                foreach (var (node, _, leg) in _limbs) if (!leg) node.Visible = true;
            }
            rig.ResetPose();
            if (rig._gripArms != null && rig.HasAnimalArms) rig._gripArms.Visible = false;
            foreach (var node in _start.Keys) _home[node] = node.Transform;
            foreach (var (node, transform) in _start) node.Transform = transform;
        }
        private void Add(Node3D? node) { if (node != null) _start.TryAdd(node, node.Transform); }
        private static float Ease(float t) => Mathf.SmoothStep(0, 1, Mathf.Clamp(t, 0, 1));

        public Vector3 Tilt(float t)
        {
            float fall = Ease((t - .18f) / .64f);
            float buckle = Mathf.Sin(Mathf.Pi * Mathf.Min(t / .65f, 1));
            float settle = Mathf.Sin(Mathf.Pi * Mathf.Clamp((t - .82f) / .18f, 0, 1)) * .045f;
            return Shape switch
            {
                DeathShape.Bull => new Vector3(.20f * buckle + .1f * fall, 0, _side * (1.50f * fall - settle)),
                DeathShape.Heavy => new Vector3(.28f * buckle - .38f * fall, 0, _side * (1.30f * fall - settle)),
                DeathShape.Round => new Vector3(-.18f * fall, 0, _side * (1.45f * fall - settle)),
                _ => new Vector3(-.55f * fall, 0, _side * (1.25f * fall - settle)),
            };
        }

        public void Sample(float t)
        {
            foreach (var (node, transform) in _home) node.Transform = transform;
            float release = Ease(t / .6f), buckle = Mathf.Sin(Mathf.Pi * Mathf.Min(t / .7f, 1));
            float late = Ease((t - .12f) / .7f), f = _rig._forwardSign;
            if (_rig.Head != null) _rig.Head.Rotation += new Vector3(.32f * f * late, 0, -.15f * _side * late);
            foreach (var (node, side, leg) in _limbs)
            {
                if (leg) node.Rotation += new Vector3(-(.65f * release + .35f * buckle) * f, .12f * side * release, 0);
                else
                {
                    float fold = Shape == DeathShape.Bull ? .8f : Shape == DeathShape.Round ? .35f : .25f;
                    node.Rotation += new Vector3(-fold * f * release, .12f * side * late, -.22f * side * late);
                }
            }
            _rig.LeftFoot.Rotation += new Vector3(.3f * f * release, -.15f * late, -.12f * late);
            _rig.RightFoot.Rotation += new Vector3(.3f * f * release, .15f * late, .12f * late);
            _rig.Weapon.Rotation += new Vector3(.25f * f * release, 0, -.35f * _side * release);
            _rig.Weapon.Position += new Vector3(.12f * _side, -.4f, .1f) * release;
            if (_tail != null) _tail.Rotation += new Vector3(.35f * late, 0, -.3f * _side * late);
            float round = Shape == DeathShape.Round ? 1 : .45f;
            _rig.Deform.Scale *= new Vector3(1 - .12f * round * late, 1 - .20f * round * buckle, 1 + .055f * round * release);
            // Enter from the current attack/walk pose, rather than snapping to the bind pose.
            float blend = Ease(t / .25f);
            foreach (var (node, start) in _start) node.Transform = start.InterpolateWith(node.Transform, blend);
        }
    }
}
