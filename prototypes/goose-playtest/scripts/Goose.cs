using Godot;
using System;

public partial class Goose : CharacterBody3D
{
    public const float Speed=4.2f, Center=1.09f;
    public int Health {get;private set;}=100;
    public bool IsDead=>Health<=0;
    public bool ManualControl {get;set;}
    public Node3D Visual {get;private set;}=null!;
    public RigidBody3D? Corpse {get;private set;}
    public event Action<Vector3,Vector3>? Shot;
    private Node3D _deform=null!,_body=null!,_left=null!,_right=null!,_weapon=null!,_muzzle=null!;
    private Vector3 _bodyHome,_leftHome,_rightHome,_weaponHome,_push;
    private CollisionShape3D _collision=null!;
    private Vector2 _move;
    private Vector3 _aim=new(0,0,-6);
    private bool _shoot;
    private float _clock,_cooldown,_kick,_hurt;
    private Vector3 _spawn;

    public override void _Ready()
    {
        CollisionLayer=2;CollisionMask=5;FloorSnapLength=.25f;
        _spawn=Position;
        _collision=new CollisionShape3D{Shape=new CapsuleShape3D{Radius=.48f,Height=2.18f},Position=Vector3.Up*Center};AddChild(_collision);
        Visual=GD.Load<PackedScene>("res://assets/goose.glb").Instantiate<Node3D>();AddChild(Visual);
        _body=Part("goose_body");_deform=Part("goose_deform");_left=Part("goose_leftFoot");_right=Part("goose_rightFoot");_weapon=Part("goose_weapon");_muzzle=Part("Muzzle");
        _bodyHome=_body.Position;_leftHome=_left.Position;_rightHome=_right.Position;_weaponHome=_weapon.Position;
    }
    private Node3D Part(string name)=>Visual.FindChild(name,true,false) as Node3D??throw new InvalidOperationException("Missing goose node: "+name);
    public static Vector3 MoveVector(Vector2 value){var v=value.LimitLength();return new Vector3(v.X,0,v.Y);}
    public void SetControls(Vector2 move,Vector3 aim,bool shoot){_move=move;_aim=aim;_shoot=shoot;}
    public override void _UnhandledInput(InputEvent input)
    {
        if(ManualControl)return;
        if(input is InputEventKey key&&key.Pressed&&!key.Echo)
        {
            if(key.PhysicalKeycode==Key.H)TakeDamage(25,new Vector3(-1,0,-.4f));
            if(key.PhysicalKeycode==Key.K)TakeDamage(100,new Vector3(.8f,0,-.6f));
            if(key.PhysicalKeycode==Key.R)Respawn(_spawn);
        }
    }
    private void ReadInput()
    {
        if(!GetWindow().HasFocus()){_move=Vector2.Zero;_shoot=false;return;}
        _move=new Vector2((Input.IsPhysicalKeyPressed(Key.D)?1:0)-(Input.IsPhysicalKeyPressed(Key.A)?1:0),(Input.IsPhysicalKeyPressed(Key.S)?1:0)-(Input.IsPhysicalKeyPressed(Key.W)?1:0));
        _shoot=Input.IsMouseButtonPressed(MouseButton.Left);
        var camera=GetViewport().GetCamera3D();
        if(camera!=null)
        {
            var mouse=GetViewport().GetMousePosition();
            var hit=new Plane(Vector3.Up,GlobalPosition.Y).IntersectsRay(camera.ProjectRayOrigin(mouse),camera.ProjectRayNormal(mouse));
            if(hit.HasValue)_aim=hit.Value;
        }
    }
    public override void _PhysicsProcess(double delta)
    {
        float dt=(float)delta;_clock+=dt;_cooldown=Math.Max(0,_cooldown-dt);_kick=Mathf.MoveToward(_kick,0,dt*7);_hurt=Mathf.MoveToward(_hurt,0,dt*3);
        if(IsDead)
        {
            if(Corpse!=null)
            {
                if(Corpse.GetContactCount()>0)_hurt=Math.Max(_hurt,Math.Min(.28f,Corpse.LinearVelocity.Length()*.025f));
                _deform.Scale=new Vector3(1+_hurt*.3f,1-_hurt*.5f,1+_hurt*.3f);
            }
            return;
        }
        if(!ManualControl)ReadInput();
        var movement=MoveVector(_move)*Speed;
        _push=_push.MoveToward(Vector3.Zero,dt*9);
        Velocity=new Vector3(movement.X+_push.X,IsOnFloor()?-.1f:Velocity.Y-18*dt,movement.Z+_push.Z);
        MoveAndSlide();
        var direction=_aim-GlobalPosition;direction.Y=0;
        if(direction.LengthSquared()>.01f)Visual.Rotation=new Vector3(0,Mathf.Atan2(direction.X,direction.Z),0);
        if(_shoot)FireAt(_aim);
        var amount=Math.Min(1,new Vector2(Velocity.X,Velocity.Z).Length()/Speed);
        var step=Mathf.Sin(_clock*13);
        _body.Position=_bodyHome+Vector3.Up*(Math.Abs(step)*.045f*amount);
        _body.Rotation=new Vector3(-_kick*.10f,0,Mathf.Cos(_clock*13)*.025f*amount);
        _left.Position=_leftHome+new Vector3(0,Math.Max(0,step)*.12f,step*.14f)*amount;
        _right.Position=_rightHome+new Vector3(0,Math.Max(0,-step)*.12f,-step*.14f)*amount;
        _weapon.Position=_weaponHome+new Vector3(0,0,-_kick*.09f);
        _deform.Scale=new Vector3(1+_hurt*.05f,1-_hurt*.10f,1+_hurt*.05f);
        if(Position.Y< -8)Respawn(_spawn);
    }
    public bool FireAt(Vector3 point)
    {
        if(IsDead||_cooldown>0)return false;
        var direction=point-GlobalPosition;direction.Y=0;
        if(direction.LengthSquared()<.01f)return false;
        direction=direction.Normalized();
        Visual.Rotation=new Vector3(0,Mathf.Atan2(direction.X,direction.Z),0);
        _cooldown=.16f;_kick=1;
        // Query from the torso, so a barrel protruding through cover cannot bypass it.
        var origin=GlobalPosition+Vector3.Up*.9f;
        var end=origin+direction*32;
        var query=PhysicsRayQueryParameters3D.Create(origin,end,5);
        var result=GetWorld3D().DirectSpaceState.IntersectRay(query);
        if(result.Count>0)
        {
            end=result["position"].AsVector3();
            if(result["collider"].AsGodotObject() is TargetDummy target)target.Hit();
        }
        var muzzle=_muzzle.GlobalPosition;
        Shot?.Invoke(origin.DistanceTo(end)<origin.DistanceTo(muzzle)?origin:muzzle,end);
        return true;
    }
    public void TakeDamage(int damage,Vector3 direction)
    {
        if(IsDead||damage<=0)return;
        direction.Y=0;if(direction.LengthSquared()<.001f)direction=Vector3.Back;direction=direction.Normalized();
        Health=Math.Max(0,Health-damage);_hurt=1;_push=direction*3;
        if(!IsDead)return;
        CollisionLayer=0;CollisionMask=0;_collision.SetDeferred(CollisionShape3D.PropertyName.Disabled,true);Velocity=Vector3.Zero;
        _body.Position=_bodyHome;_body.Rotation=Vector3.Zero;_left.Position=_leftHome;_right.Position=_rightHome;_deform.Scale=Vector3.One;
        Corpse=new RigidBody3D{Name="GooseCorpse",Mass=1,CollisionLayer=2,CollisionMask=5,ContinuousCd=true,ContactMonitor=true,MaxContactsReported=4,LinearDamp=.7f,AngularDamp=1.1f,PhysicsMaterialOverride=new PhysicsMaterial{Friction=.85f,Bounce=.12f}};
        Corpse.AddChild(new CollisionShape3D{Shape=new CapsuleShape3D{Radius=.48f,Height=2.18f}});
        GetParent().AddChild(Corpse);Corpse.GlobalPosition=GlobalPosition+Vector3.Up*Center;
        Visual.Reparent(Corpse);Visual.Position=Vector3.Down*Center;
        Corpse.ApplyCentralImpulse(direction*4.8f+Vector3.Up*5.5f);Corpse.ApplyTorqueImpulse(new Vector3(2.5f,.6f,-1.8f));
    }
    public void Respawn(Vector3 position)
    {
        if(Corpse!=null){Visual.Reparent(this);Corpse.QueueFree();Corpse=null;}
        Visual.Position=Vector3.Zero;Visual.Rotation=Vector3.Zero;
        Position=position;Velocity=Vector3.Zero;Health=100;CollisionLayer=2;CollisionMask=5;
        _collision.SetDeferred(CollisionShape3D.PropertyName.Disabled,false);
        _move=Vector2.Zero;_shoot=false;_push=Vector3.Zero;_kick=_hurt=_cooldown=0;
        _deform.Scale=Vector3.One;_body.Rotation=Vector3.Zero;_body.Position=_bodyHome;_left.Position=_leftHome;_right.Position=_rightHome;_weapon.Position=_weaponHome;
    }
}
