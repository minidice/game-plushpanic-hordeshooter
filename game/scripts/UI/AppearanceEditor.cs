using Godot;
using System;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>Species-local appearance settings. Preview visibility never changes this profile.</summary>
public partial class AppearanceEditor : VBoxContainer
{
    private CharacterRig? _rig;
    private AppearanceProfile _profile = new();
    private TabContainer? _tabs;
    public event Action? AppearanceChanged;
    public string ProfilePath {get;set;}=AppearanceStore.Path;

    public void RefreshPreview()
    {
        if (_rig == null) return;
        _rig.ApplyAppearance(_profile);
        AppearanceChanged?.Invoke();
    }

    private void Apply()
    {
        if (_rig == null) return;
        AppearanceStore.SaveProfile(_rig.Definition.Id, _profile,ProfilePath);
        RefreshPreview();
    }

    public void ShowRig(CharacterRig rig, int presetIndex = 0)
    {
        int selectedTab = _tabs?.CurrentTab ?? 0;
        foreach (var child in GetChildren()) { RemoveChild(child); child.QueueFree(); }
        _rig = rig;
        _profile = AppearanceStore.ReadProfile(rig.Definition.Id,ProfilePath);
        RefreshPreview();
        AddThemeConstantOverride("separation", 8);

        var presets = new HBoxContainer();
        presets.AddChild(UiTheme.Label(Loc.Text("custom_presets"), 17));
        // Reselect lets the shown preset be applied again to undo later edits.
        var preset = new OptionButton { Name = "PresetChoice", SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = Loc.Text("custom_preset_hint"), AllowReselect = true };
        foreach (var item in AppearancePresets.For(rig.Definition.Id)) preset.AddItem(Loc.Text(item.NameKey));
        if (preset.ItemCount > 0) preset.Select(Math.Clamp(presetIndex, 0, preset.ItemCount - 1));
        // Choosing a preset applies it at once; the editor is rebuilt after the popup's signal returns.
        preset.ItemSelected += chosen =>
        {
            _profile = AppearanceCatalog.CreatePreset(rig.Definition.Id, (int)chosen);
            Apply();
            Callable.From(() => { if (IsInstanceValid(this) && _rig == rig) ShowRig(rig, (int)chosen); }).CallDeferred();
        };
        presets.AddChild(preset);
        AddChild(presets);

        _tabs = new TabContainer { Name = "PartTabs", SizeFlagsVertical = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 270) };
        _tabs.AddThemeFontSizeOverride("font_size", 17);
        AddChild(_tabs);
        var bodies = new Dictionary<string, VBoxContainer>();
        foreach (var key in new[] { "body", "face", "head", "limbs", "tail", "gear" })
        {
            var scroll = new ScrollContainer { Name = "Tab_" + key, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            _tabs.AddChild(scroll);
            _tabs.SetTabTitle(_tabs.GetTabCount() - 1, Loc.Text("custom_tab_" + key));
            var inset = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var edge in new[] { "left", "right", "top", "bottom" }) inset.AddThemeConstantOverride("margin_" + edge, 12);
            scroll.AddChild(inset);
            var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            body.AddThemeConstantOverride("separation", 10);
            inset.AddChild(body);
            bodies[key] = body;
        }
        bool bird = AppearanceCatalog.IsBird(rig.Definition.Id);
        AddChoice(bodies["face"], "FaceStyle", "custom_face_style", _profile.FaceStyle,
            new[] { "custom_face_neutral", "custom_face_alert", "custom_face_sleepy" }, value => _profile.FaceStyle = value);
        AddChoice(bodies["face"], "BeakStyle", bird ? "custom_beak_style" : "custom_snout_style", _profile.BeakStyle,
            new[] { "custom_shape_default", "custom_shape_wide", "custom_shape_short" }, value => _profile.BeakStyle = value);
        if (!bird)
        {
            AddChoice(bodies["head"], "EarStyle", "custom_ear_style", _profile.EarStyle,
                new[] { "custom_ear_default", "custom_ear_round", "custom_ear_pointed", "custom_ear_folded" }, value => _profile.EarStyle = value);
        }
        AddChoice(bodies["head"], "HairStyle", "custom_hair_style", _profile.HairStyle,
            new[] { "custom_hair_none", "custom_hair_short", "custom_hair_swept" }, value => _profile.HairStyle = value);
        foreach (var scale in AppearanceCatalog.Scales)
        {
            if (scale.MammalOnly && bird) continue;
            string label = scale.Key == "beak_size" && !bird ? "custom_snout_size" : scale.LabelKey;
            AddScale(bodies[scale.Tab], scale.Key, label, scale.Min, scale.Max, scale.Unit);
        }
        AddColor(bodies["body"], "body", "custom_body_primary");
        AddColor(bodies["body"], "body2", "custom_body_secondary");
        if (AppearanceCatalog.HasMarking(rig.ToneSpecies)) AddColor(bodies["body"], "body3", "custom_body_marking");
        bodies["body"].AddChild(Hint(Loc.Text("custom_tone_hint_" + rig.ToneSpecies)));
        AddColor(bodies["face"], "eyes");
        AddColor(bodies["face"], "beak", bird ? "custom_beak" : "custom_snout");
        AddColor(bodies["head"], "hair");
        var hairHint = UiTheme.Label(Loc.Text("custom_hair_hint"), 14, UiTheme.Muted);
        hairHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        bodies["head"].AddChild(hairHint);
        bodies["limbs"].AddChild(Hint(Loc.Text("custom_arm_follow_hint")));
        AddColor(bodies["limbs"], "feet");
        AddChoice(bodies["tail"], "TailStyle", "custom_tail_style", _profile.TailStyle,
            new[] { "custom_tail_default", "custom_tail_fluffy", "custom_tail_long", "custom_tail_short", "custom_tail_curled" }, value => _profile.TailStyle = value);
        AddColor(bodies["tail"], "tail");

        var gear = bodies["gear"];
        AddChoice(gear, "Hat", "custom_hat_choice", _profile.Hat,
            new[] { "custom_no_hat", "custom_helmet", "custom_beret" }, value => _profile.Hat = value, "HatChoice");
        var toggles = new GridContainer { Columns = 2 };
        gear.AddChild(toggles);
        AddToggle(toggles, "WearVest", "custom_wear_vest", _profile.Vest, value => _profile.Vest = value);
        AddToggle(toggles, "WearPack", "custom_wear_pack", _profile.Pack, value => _profile.Pack = value);
        AddToggle(toggles, "WearGoggles", "custom_goggles", _profile.Goggles, value => _profile.Goggles = value);
        AddToggle(toggles, "WearHeadset", "custom_headset", _profile.Headset, value => _profile.Headset = value);
        AddChoice(gear, "VestStyle", "custom_vest_style", _profile.VestStyle,
            new[] { "custom_vest_pouches", "custom_vest_padded" }, value => _profile.VestStyle = value, "VestStyle");
        foreach (var channel in new[] { "hat", "vest", "pack" }) AddColor(gear, channel);
        _tabs.CurrentTab = Math.Clamp(selectedTab, 0, _tabs.GetTabCount() - 1);

        var footer = new HBoxContainer();
        var saved = UiTheme.Label(Loc.Text("custom_saved"), 13, UiTheme.Muted);
        saved.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        saved.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        footer.AddChild(saved);
        var reset = new Button { Name = "ResetAppearance", Text = Loc.Text("custom_reset") };
        reset.AddThemeFontSizeOverride("font_size", 14);
        reset.Pressed += () => { _profile = new AppearanceProfile(); Apply(); ShowRig(rig); };
        footer.AddChild(reset);
        AddChild(footer);
    }

    private HBoxContainer Row(VBoxContainer parent, string labelKey)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);
        var label = UiTheme.Label(Loc.Text(labelKey), 16);
        label.CustomMinimumSize = new Vector2(135, 0);
        row.AddChild(label);
        parent.AddChild(row);
        return row;
    }

    private void AddChoice(VBoxContainer parent, string key, string label, int selected, string[] choices, Action<int> setter, string? name = null)
    {
        var row = Row(parent, label);
        var choice = new OptionButton { Name = name ?? "Choice_" + key, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 34) };
        foreach (var option in choices) choice.AddItem(Loc.Text(option));
        choice.Select(selected);
        choice.ItemSelected += value => { setter((int)value); Apply(); };
        row.AddChild(choice);
    }

    private static Label Hint(string text)
    {
        var hint = UiTheme.Label(text, 14, UiTheme.Muted);
        hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return hint;
    }

    private void AddScale(VBoxContainer parent, string key, string labelKey, float min, float max, string unit = "×")
    {
        var row = Row(parent, labelKey);
        string Show(double v) => key=="marking_softness"?$"{v*100:0}%":unit == "°" ? $"{v:+0;-0;0}°" : $"{v:0.00}{unit}";
        var valueLabel = UiTheme.Label(Show(_profile.ScaleFor(key)), 15, UiTheme.Muted);
        valueLabel.CustomMinimumSize = new Vector2(58, 0);
        var slider = new HSlider { Name = "Scale_" + key, MinValue = min, MaxValue = max, Step = unit == "°" ? 1 : .01, Value = _profile.ScaleFor(key), SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(100, 32) };
        if(key=="marking_softness")slider.TooltipText=Loc.Text("custom_marking_softness_hint");
        slider.ValueChanged += value => { _profile.SetScale(key, (float)value); valueLabel.Text = Show(value); Apply(); };
        row.AddChild(slider);
        row.AddChild(valueLabel);
    }

    private void AddColor(VBoxContainer parent, string channel, string? labelKey = null)
    {
        if (_rig == null) return;
        var row = Row(parent, labelKey ?? "custom_" + channel);
        string chosen = _profile.ColorFor(channel);
        bool tint = channel == "tail";
        var picker = new ColorPickerButton
        {
            Name = "Color_" + channel,
            Color = chosen.Length > 0 ? new Color(chosen) : tint ? Colors.White : _rig.ChannelColor(channel),
            EditAlpha = false, CustomMinimumSize = new Vector2(100, 32), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Disabled = !_rig.HasColorChannel(channel), TooltipText = Loc.Text(tint ? "custom_tint_hint" : "custom_color_hint")
        };
        picker.ColorChanged += color => { _profile.SetColor(channel, "#" + color.ToHtml(false)); Apply(); };
        row.AddChild(picker);
    }

    private void AddToggle(Node parent, string name, string labelKey, bool value, Action<bool> setter)
    {
        var toggle = new CheckButton { Name = name, Text = Loc.Text(labelKey), ButtonPressed = value };
        toggle.Toggled += enabled => { setter(enabled); Apply(); };
        parent.AddChild(toggle);
    }
}
