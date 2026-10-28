using System;
using System.Threading.Tasks;
using Godot;

namespace HordeShooter;

/// <summary>
/// The scenario's stage change (decisions.md L-14), after the user's reference clip: hold the hatch → a circle closes on
/// the survivor → black, the next stage's number and name → the next stage loads → the circle opens on the survivor at
/// the new stage's arrival hatch while the camera eases from the old view into the new one (<see cref="CameraRig.Arrive"/>).
/// The overlay lives on the tree root so it stays up while the stage scene is replaced.
/// </summary>
public partial class StageTransition : CanvasLayer
{
    public const float CloseSeconds = .9f;
    public const float TitleSeconds = 1.5f;
    public const float OpenSeconds = 1.1f;
    /// <summary>The camera's ease from the last stage's view; it starts while the screen is still black.</summary>
    public const float ArriveSeconds = 2.8f;

    /// <summary>A stage change is under way (from the hatch until the circle is open again).</summary>
    public static bool Busy => _current != null;
    /// <summary>The stage being built now came down a hatch: start in <see cref="FromView"/> and reveal it.</summary>
    public static bool Arriving { get; private set; }
    public static CameraView FromView { get; private set; } = CameraView.Default;
    /// <summary>Checks: replaces reloading the game scene (the test builds the next stage itself).</summary>
    public static Action<SceneTree>? LoadOverride { get; set; }
    /// <summary>How far the circle is closed (0 open … 1 black) and whether the title card shows (checks, captures).</summary>
    public static float Closed => _current?._closed ?? 0;
    public static bool TitleShown => _current is { } t && t._title.Modulate.A > .5f;

    private static StageTransition? _current;
    private ColorRect _iris = null!;
    private ShaderMaterial _shader = null!;
    private Control _title = null!;
    private float _closed;
    private Stage? _stage;
    private bool _cancelled;

    public static void Cancel()
    {
        if (_current is { } current)
        {
            current._cancelled = true;
            current.Hide();
            current.QueueFree();
        }
        _current = null; Arriving = false; GameSession.Carry = null;
    }

    private const string IrisShader = @"
shader_type canvas_item;
uniform vec2 center = vec2(0.5);
uniform float radius = 2.0;
uniform float aspect = 1.7778;
void fragment() {
    vec2 d = (UV - center) * vec2(aspect, 1.0);
    float edge = 0.004;
    COLOR = vec4(0.0, 0.0, 0.0, smoothstep(radius - edge, radius + edge, length(d)));
}";

    /// <summary>Leave <paramref name="stage"/> for scenario stage <paramref name="next"/>, carrying the survivor's kit.</summary>
    public static void Leave(Stage stage, int next, StageCarry? carry = null)
    {
        if (Busy) return;
        var t = new StageTransition { Name = "StageTransition", Layer = 90, ProcessMode = ProcessModeEnum.Always };
        _current = t;
        stage.GetTree().Root.AddChild(t);
        t.Run(stage, next, carry);
    }

    /// <summary>The next stage is built: drop the title and open the circle on its survivor.</summary>
    public static void Reveal(Stage stage)
    {
        if (_current is { } t) t.Open(stage);
        else Arriving = false;
    }

    public override void _Ready()
    {
        _shader = new ShaderMaterial { Shader = new Shader { Code = IrisShader } };
        _iris = new ColorRect { Material = _shader, MouseFilter = Control.MouseFilterEnum.Ignore };
        _iris.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_iris);
        _title = BuildTitle();
        AddChild(_title);
        SetClosed(0, new Vector2(.5f, .5f));
    }

    private Control BuildTitle()
    {
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.AddThemeConstantOverride("separation", 6);
        var number = new Label { Name = "Number", HorizontalAlignment = HorizontalAlignment.Center };
        number.AddThemeFontSizeOverride("font_size", 34);
        number.AddThemeColorOverride("font_color", new Color("9dffa8"));
        var name = new Label { Name = "Name", HorizontalAlignment = HorizontalAlignment.Center };
        name.AddThemeFontSizeOverride("font_size", 64);
        name.AddThemeColorOverride("font_color", new Color("f4ecd8"));
        box.AddChild(number);
        box.AddChild(name);
        return box;
    }

    private async void Run(Stage stage, int next, StageCarry? carry)
    {
        _stage = stage;
        FromView = stage.Map.View;
        GameSession.Carry = carry ?? StageCarry.From(stage.Player);
        // The survivor stands still at the hatch while the circle closes.
        stage.Player.ManualControl = true;
        stage.Player.SetControls(Vector2.Zero, stage.Player.AimPoint, false);
        if (!await Tween(CloseSeconds, k => SetClosed(Ease(k), Focus(stage)))) return;
        SetClosed(1, new Vector2(.5f, .5f));
        _title.GetNode<Label>("Number").Text = Loc.Format("ui_transition_stage_number", next);
        _title.GetNode<Label>("Name").Text = Loc.Text($"ui_transition_stage{next}");
        if (!await Tween(.35f, k => _title.Modulate = new Color(1, 1, 1, k))) return;
        if (!await Tween(TitleSeconds, _ => { })) return;
        GameSession.ScenarioStage = next;
        Arriving = true;
        _stage = null;
        var tree = GetTree();
        if (LoadOverride != null) LoadOverride(tree);
        else tree.ReloadCurrentScene();
    }

    private async void Open(Stage stage)
    {
        _stage = stage;
        // Let the new stage settle a frame so the survivor's screen spot is right.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_cancelled) return;
        if (!await Tween(.3f, k => _title.Modulate = new Color(1, 1, 1, 1 - k))) return;
        if (!await Tween(OpenSeconds, k => SetClosed(1 - Ease(k), Focus(stage)))) return;
        Arriving = false;
        _current = null;
        QueueFree();
    }

    private static float Ease(float k) => k * k * (3 - 2 * k);

    /// <summary>The survivor's chest on screen (0..1), where the circle closes and opens.</summary>
    private Vector2 Focus(Stage stage)
    {
        if (!IsInstanceValid(stage) || !IsInstanceValid(stage.Player) || !IsInstanceValid(stage.Cam)) return new Vector2(.5f, .5f);
        var size = GetViewport().GetVisibleRect().Size;
        var at = stage.Cam.UnprojectPosition(stage.Player.GlobalPosition + Vector3.Up * .8f);
        return new Vector2(Mathf.Clamp(at.X / size.X, 0, 1), Mathf.Clamp(at.Y / size.Y, 0, 1));
    }

    private void SetClosed(float closed, Vector2 center)
    {
        _closed = closed;
        var size = GetViewport().GetVisibleRect().Size;
        float aspect = size.Y > 0 ? size.X / size.Y : 16f / 9f;
        // Open = a circle wide enough to clear the far corner from wherever the centre is.
        float far = new Vector2(Mathf.Max(center.X, 1 - center.X) * aspect, Mathf.Max(center.Y, 1 - center.Y)).Length() + .02f;
        _shader.SetShaderParameter("center", center);
        _shader.SetShaderParameter("aspect", aspect);
        _shader.SetShaderParameter("radius", far * (1 - closed));
        _iris.Visible = closed > 0;
    }

    /// <summary>Runs <paramref name="step"/> with 0..1 over <paramref name="seconds"/> of real frames (pause-proof).</summary>
    private async Task<bool> Tween(float seconds, Action<float> step)
    {
        float t = 0;
        while (t < seconds)
        {
            step(t / seconds);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (_cancelled) return false;
            t += (float)GetProcessDeltaTime();
        }
        step(1);
        return true;
    }
}
