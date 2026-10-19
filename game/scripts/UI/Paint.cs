using Godot;
using System;

namespace HordeShooter;

/// <summary>A Control whose drawing is supplied by a delegate (reticle, bars). Redraws every frame.</summary>
public partial class Paint : Control
{
    public Action<Control>? Painter { get; set; }

    public override void _Ready() => MouseFilter = MouseFilterEnum.Ignore;
    public override void _Process(double delta) => QueueRedraw();
    public override void _Draw() => Painter?.Invoke(this);
}
