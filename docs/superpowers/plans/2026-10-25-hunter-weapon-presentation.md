# Hunter weapon presentation implementation plan

User approved club, knife and woodbow motion/sound work on 2026-10-25. Use bounded subagents for shared rig presentation and Studio; primary integrates gameplay, audio, verification and records. Retain shared workspace and existing combat balance. No commit/push requested.

## Design contract

- Club: lifted anticipation, downward strike, weighted recovery. Knife: compact lateral slash and quick return. Bow: visible nock, two string segments drawn to hand, release and reset.
- Shared API: `CharacterRig.SetHunterAttackPose(string weapon, float elapsed, float windup, float recovery)`. Negative elapsed restores idle; impact/release is exactly at windup. Called after base Animate, replacing generic enemy shove for these weapons. Pose must be deterministic and usable by Studio.
- Melee presentation follows existing melee time. Bow presentation follows its own visual elapsed clock on host and puppet, releases only on actual projectile event, cancels on interruption. No damage/range/cooldown/speed changes.
- Dedicated deterministic synthesized cues: hunter_club_raise/swing/hit, hunter_knife_ready/swing/hit, hunter_bow_draw/release/hit. Spatial voice caps, near volume caps. No hit cue for a miss. Draw cue stops on interruption.
- Studio uses shared runtime rigs and pose, offers cached attack frame playback and explicit sound preview. This is a visual preview, not simulated combat.

## Checklist

- [x] Implement shared rig poses and articulated bow geometry.
- [x] Wire authoritative and puppet timelines, cancellation and dedicated sound recipes.
- [x] Add Studio attack playback using shared pose and sound catalog.
- [x] Verify distinct poses, impact timing, interruption reset, real combat and rendered frames; run related regression checks.
- [x] Update decisions/issues, feature checklist and relevant specs with actual evidence and remaining limitations.
