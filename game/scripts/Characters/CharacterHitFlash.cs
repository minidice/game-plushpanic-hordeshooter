using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    private CharacterHitFlashEffect? _hitFlash;

    /// <summary>Call only after accepting positive damage. Repeated hits restart the brief white fade.</summary>
    public void FlashHit()
    {
        if (_hitFlash == null)
        {
            _hitFlash = new CharacterHitFlashEffect { Name = "HitFlash" };
            AddChild(_hitFlash);
        }
        _hitFlash.Play(Pose); // Includes procedural enemy arms attached outside the imported model.
    }
}
