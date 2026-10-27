using Godot;

namespace HordeShooter;

/// <summary>
/// Ammo, medkits or throwables lying on the floor in Outpost (decisions.md M-25): dropped from a backpack or spilled by a
/// named monster. E puts it in the backpack; what does not fit stays. The host spawns it and tells the clients.
/// </summary>
public partial class LootPickup : Interactable
{
    public InvItem Item { get; private set; } = null!;
    private Node3D _display = null!;
    private float _time;

    public static LootPickup Spawn(Stage stage, Vector3 at, InvItem item, int netId = 0)
    {
        var loot = new LootPickup { Name = "Loot", Position = new Vector3(at.X, 0, at.Z), Radius = 1.4f, NetId = netId, Item = item };
        loot.Prompt = Loc.Format("fmt_use_loot", item.DisplayName, item.Count);
        loot.OnUse = who => stage.TakeLoot(loot, who ?? stage.Player);
        stage.AddChild(loot);
        stage.AddInteractable(loot);
        loot.Build();
        stage.Net?.OnLootSpawn(loot);
        return loot;
    }

    private void Build()
    {
        var color = Item.Kind switch
        {
            ItemKind.Ammo => CaliberDatabase.Get(Item.Id)?.Color ?? new Color("c8c8c8"),
            ItemKind.Medkit => new Color("e8eee6"),
            _ => new Color("8dff9c"),
        };
        GroundMarker.Circle(this, Vector3.Up * .035f, .4f, color, 0, .65f, .03f);
        _display = new Node3D { Position = Vector3.Up * .3f };
        AddChild(_display);
        if (Item.Kind == ItemKind.Throwable && ArmoryCatalog.Throwables.TryGetValue(Item.Id, out var def))
        {
            ThrownItem.BuildLook(_display, def);
            _display.Scale = Vector3.One * 1.3f;
            return;
        }
        // A small crate: ammo tins in the caliber's colour, medkits white with a red cross.
        StageArt.Box(_display, Vector3.Zero, new Vector3(.42f, .24f, .3f), Item.Kind == ItemKind.Medkit ? "d3d8cc" : "4a5446", true);
        Mat.Box(_display, new Vector3(0, .125f, 0), new Vector3(.3f, .015f, .2f),
            Item.Kind == ItemKind.Medkit ? Mat.Glow(new Color("ff4040"), 1.2f) : Mat.Glow(color, .8f));
    }

    /// <summary>What is left after someone took part of it.</summary>
    public void SetCount(int count)
    {
        Item.Count = count;
        Prompt = Loc.Format("fmt_use_loot", Item.DisplayName, count);
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;
        if (_display == null) return;
        _display.Position = Vector3.Up * (.3f + Mathf.Sin(_time * 2.2f) * .05f);
        _display.RotateY((float)delta * .7f);
    }
}
