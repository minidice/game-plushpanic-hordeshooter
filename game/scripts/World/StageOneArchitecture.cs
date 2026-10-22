using Godot;

namespace HordeShooter;

public sealed partial class StageOneLayout
{
    protected override void DrawWall(Node3D body, Vector3 size)
    {
        float x=body.Position.X;
        var finish=x<10.5f || x>=116 ? WallFinish.Shelter : x<52 ? WallFinish.Masonry : x<84.4f ? WallFinish.Warehouse : WallFinish.Yard;
        ArchitectureArt.Wall(body,size,finish);
    }

    private void DressArchitecture()
    {
        ArchitectureArt.Portal(_stage,10.2f,2.6f,2.65f);
        ArchitectureArt.Portal(_stage,52.2f,5f,3.25f);
        // The tall raised slab keeps its existing animation; its new housing is offset toward the yard.
        ArchitectureArt.Portal(_stage,84.2f,6f,3.85f,shutter:true);
        ArchitectureArt.Portal(_stage,116.2f,2.6f,2.65f);
        foreach(var (x,w,industrial) in new[]{(10.2f,2.6f,false),(52.2f,5f,true),(84.2f,6f,true),(116.2f,2.6f,false)})
            ArchitectureArt.FloorApron(_stage,x,w,industrial);
        foreach(var (x,z) in new[]{(6.8f,-3.96f),(24f,-5.96f),(40f,-5.96f),(63f,-9.96f),(78f,-9.96f),(120f,-3.96f)})
            ArchitectureArt.Services(_stage,new Vector3(x,0,z));

        // Shutter drive and covered take-up drum sit outside the opening's moving slab.
        var drive=ArchitectureArt.Group(_stage,"ShutterDrive",new Vector3(84.2f,0,0));
        StageArt.Box(drive,new(.32f,3.25f,-3.65f),new(.5f,1.05f,.6f),"43575e",true);
        StageArt.Box(drive,new(.32f,4.45f,-3.65f),new(.09f,1.35f,.09f),"7d8e8e");
        StaticArtBatch.Bake(drive);

        // Unreachable service vignettes behind existing solid walls. The black room cutaway remains intentional.
        ServiceBay(new Vector3(29,0,-9.3f),8,4.8f,false);
        ServiceBay(new Vector3(68,0,-13.4f),9.5f,5.4f,true);
        ServiceBay(new Vector3(101,0,-10.6f),7,5f,true);

        // Shared route marker between the two ends of the bunker connection (no added light/interaction).
        ArchitectureArt.Services(_stage,new Vector3(125.95f,0,1.4f),-90);
        var hatch=ArchitectureArt.Group(_stage,"HatchSurround",ExitHatch);
        foreach(float x in new[]{-1.15f,1.15f})
            StageArt.Box(hatch,new(x,.012f,0),new(.07f,.018f,2.1f),"b4a879");
        StaticArtBatch.Bake(hatch);
    }

    private void ServiceBay(Vector3 at,float width,float depth,bool cloth)
    {
        var bay=ArchitectureArt.Group(_stage,"ExteriorServiceBay",at);
        StageArt.Box(bay,new(0,-.16f,0),new(width,.3f,depth),"303e48",true);
        StageArt.Box(bay,new(0,1.25f,-depth/2),new(width,2.5f,.18f),"3c4d55",true);
        StageArt.Box(bay,new(0,2.51f,-depth/2),new(width,.08f,.23f),"71848b");
        var partition=ArchitectureArt.Group(bay,"ServicePartition",new Vector3(-width/2,0,0),90);
        ArchitectureArt.Fence(partition,depth,1.65f,cloth);
        var cabinet=ArchitectureArt.Group(bay,"Cabinet",new Vector3(width*.27f,.8f,-depth*.18f));
        StageArt.Cargo(cabinet,new Vector3(1.5f,1.6f,.8f),"5d706d");
        var crate=ArchitectureArt.Group(bay,"StoredEquipment",new Vector3(-width*.22f,.42f,.1f));
        StageArt.Cargo(crate,new Vector3(1.45f,.84f,1.1f),"776f56");
        StageArt.Carton(bay,new Vector3(-width*.22f,1.12f,.1f),new Vector3(.8f,.55f,.7f));
        foreach(float z in new[]{-.5f,-.17f,.17f})
            StageArt.Box(bay,new(.2f,.14f,z),new(2.6f,.11f,.14f),"40545e",true);
        ArchitectureArt.Services(bay,new Vector3(-width*.23f,0,-depth/2+.1f));
        StaticArtBatch.Bake(bay);
    }
}
