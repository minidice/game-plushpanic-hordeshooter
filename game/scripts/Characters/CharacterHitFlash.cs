using Godot;

namespace HordeShooter;

public partial class CharacterRig
{
    private CharacterHitFlashEffect? _hitFlash;
    private DamageBurstEffect? _hitBurst;
    public bool HitFlashing => _hitFlash?.Active == true;

    /// <summary>Call only after accepting positive damage. Normal hits flash white, critical hits yellow.</summary>
    public void FlashHit(bool critical = false)
    {
        FlashBody(critical);
        if (IsInstanceValid(_hitBurst) && !_hitBurst!.IsQueuedForDeletion()) _hitBurst.Restart(critical);
        else _hitBurst = DamageBurstEffect.Play(this, GlobalPosition + Vector3.Up * Definition.ColliderHeight * .55f,
            Definition.ColliderHeight, Definition.ColliderRadius, false, critical);
    }

    /// <summary>Death uses the killing hit's color and replaces the small rays with a world-space pop.</summary>
    public void FlashDeath(bool critical = false)
    {
        FlashBody(critical);
        if (IsInstanceValid(_hitBurst)) _hitBurst!.QueueFree();
        _hitBurst = null;
    }

    private void FlashBody(bool critical)
    {
        if (_hitFlash == null)
        {
            _hitFlash = new CharacterHitFlashEffect { Name = "HitFlash" };
            AddChild(_hitFlash);
        }
        _hitFlash.Play(Pose, critical); // Includes procedural enemy arms attached outside the imported model.
    }
}
