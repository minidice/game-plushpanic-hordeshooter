using Godot;

namespace HordeShooter;

/// <summary>
/// Input actions are registered in code so project.godot stays readable and a rebinding UI can
/// replace the defaults later without touching gameplay scripts.
/// </summary>
public static class GameInput
{
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";
    public const string MoveUp = "move_up";
    public const string MoveDown = "move_down";
    public const string Fire = "fire";
    public const string Reload = "reload";
    public const string Interact = "interact";
    public const string Heal = "heal";
    public const string Zoom = "zoom";
    public const string Pause = "pause";
    public const string Confirm = "confirm";
    public const string Slot1 = "slot_1";
    public const string Slot2 = "slot_2";
    public const string Slot3 = "slot_3";
    /// <summary>Hold to aim (with the right mouse button, which is <see cref="AltFire"/>).</summary>
    public const string Aim = "aim";
    public const string Throw = "throw";
    public const string Dodge = "dodge";
    public const string DebugEscape = "debug_escape";
    public const string AltFire = "alt_fire";
    public const string NextThrowable = "next_throwable";
    public const string PrevThrowable = "prev_throwable";

    public static void Ensure()
    {
        Key(MoveLeft, Godot.Key.A);
        Key(MoveRight, Godot.Key.D);
        Key(MoveUp, Godot.Key.W);
        Key(MoveDown, Godot.Key.S);
        Key(Reload, Godot.Key.R);
        Key(Interact, Godot.Key.E);
        Key(Heal, Godot.Key.Q);
        Key(Zoom, Godot.Key.Tab);
        Key(Pause, Godot.Key.Escape);
        Key(Confirm, Godot.Key.Enter);
        Key(Slot1, Godot.Key.Key1);
        Key(Slot2, Godot.Key.Key2);
        Key(Slot3, Godot.Key.Key3);
        Key(Aim, Godot.Key.Ctrl);
        Key(Throw, Godot.Key.G);
        Key(Dodge, Godot.Key.Space);
        Key(DebugEscape, Godot.Key.F8);
        Mouse(Fire, MouseButton.Left);
        Mouse(AltFire, MouseButton.Right);
        Mouse(NextThrowable, MouseButton.WheelDown);
        Mouse(PrevThrowable, MouseButton.WheelUp);
    }

    private static void Mouse(string action, MouseButton button)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventMouseButton { ButtonIndex = button });
    }

    private static void Key(string action, Key key)
    {
        if (InputMap.HasAction(action)) return;
        InputMap.AddAction(action);
        InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = key });
    }
}
