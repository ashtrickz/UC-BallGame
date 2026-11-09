using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Config;
using Game.Gameplay.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Game.Gameplay.Editor
{
    /// <summary>Additive art installation. Keeps all gameplay roots, references, hinges and the pool target.</summary>
    public static class CanyonEnvironmentInstaller
    {
        public const string Root = "Assets/_Game/Art/Environment/Canyon";
        private const string Dressing = "Canyon Dressing";
        private static readonly string[] Names = { "GroundTile", "RockWide", "RockSmall", "RockTall", "CliffChunk",
            "TrailBank", "SucculentA", "SucculentB", "GrassTuft", "ColonyGateFrame", "ColonyDoorLeft",
            "ColonyDoorRight", "ColonyBasin", "StartMedallion" };

        [MenuItem("Tools/Ball Game/Environment/Install Blender Canyon")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before installing art.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != GameplaySceneInstaller.ScenePath)
            {
                if (scene.isDirty) throw new InvalidOperationException("Save the current scene before opening Game.");
                scene = EditorSceneManager.OpenScene(GameplaySceneInstaller.ScenePath);
            }
            var stage = Object.FindFirstObjectByType<GameplayStage>();
            if (!stage) throw new InvalidOperationException("Game scene has no GameplayStage.");
            Apply(stage);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[Canyon] Blender kit installed; gameplay anchors and rules preserved. " + ValidateAssets());
        }

        public static void Apply(GameplayStage stage)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(CanyonModelImporter.Models + "GroundTile.fbx")) return;
            Directory.CreateDirectory(Root + "/Materials");
            Directory.CreateDirectory(Root + "/Prefabs");
            var surface = Surface("Canyon Vertex Surface", Color.white, 0.025f);
            var background = Surface("Distant Sand", Hex("DFB47F"), 0.035f);
            var lane = Surface("Route Sand", Hex("F5CE97"), 0.01f);
            foreach (var name in Names) MakePrefab(name, surface);
            Remove(stage.transform.Find(Dressing));
            var dressing = Group(Dressing, stage.transform);
            var old = stage.transform.Find("Environment");
            if (old)
            {
                foreach (var renderer in old.GetComponentsInChildren<MeshRenderer>(true)) renderer.enabled = false;
                var backdrop = old.Find("Backdrop floor");
                if (backdrop)
                {
                    backdrop.GetComponent<MeshRenderer>().enabled = true;
                    backdrop.GetComponent<MeshRenderer>().sharedMaterial = background;
                    // Original backdrop cube is 0.2m thick: its top must stay below the Y=0 route.
                    backdrop.localPosition = new Vector3(0,-0.20f,16);
                }
            }
            var config = AssetDatabase.LoadAssetAtPath<SlimeGameConfig>(GameplayAssetsBuilder.GameConfigPath);
            var route = (float)config.CreateSnapshot().FinishZ;
            if (config.Campaign != null)
                foreach (var level in config.Campaign.levels) route = Mathf.Max(route,level.routeLength);
            for (var z=-12f; z<route+14; z+=6) Put("GroundTile",dressing,new Vector3(0,0,z));
            Put("StartMedallion",dressing,Vector3.zero);

            var rng = new System.Random(43691);
            float R(float a,float b) => Mathf.Lerp(a,b,(float)rng.NextDouble());
            // Camera is on +X. The near bank stays low; the tall silhouettes sit on the far bank (-X).
            for (var z=-6f;z<route+9;z+=4)
            {
                foreach (var side in new[] {-1,1})
                {
                    Put("TrailBank",dressing,new Vector3(side*5.5f,-0.02f,z),side<0?180:0,new Vector3(1,1,1.12f));
                    Put(z%8==0?"RockSmall":"RockWide",dressing,new Vector3(side*R(5.6f,6.3f),0,z+R(.1f,.8f)),R(0,360),Vector3.one*R(.65f,1.12f));
                    Put(rng.Next(2)==0?"SucculentA":"SucculentB",dressing,new Vector3(side*R(4.95f,5.8f),0,z+R(1.8f,3.5f)),R(0,360),Vector3.one*R(.65f,1));
                    Put("GrassTuft",dressing,new Vector3(side*R(4.8f,6.2f),0,z+R(.2f,3.8f)),R(0,360),Vector3.one*R(.7f,1.2f));
                }
            }
            for(var z=-5f;z<route+10;z+=7.8f)
            {
                Put("CliffChunk",dressing,new Vector3(-8.2f,0,z),R(-25,25),new Vector3(R(.9f,1.3f),R(.6f,.95f),R(1,1.5f)));
                Put("RockTall",dressing,new Vector3(-6.7f,0,z+1.9f),R(0,360),Vector3.one*R(.8f,1.2f));
                Put("RockWide",dressing,new Vector3(8.0f,0,z+2),R(0,360),new Vector3(1.8f,1.1f,1.8f));
            }
            // A few quiet low props frame the route without resembling shootable crystals.
            for(var z=2f;z<route;z+=6.5f)
            {
                Put("RockSmall",dressing,new Vector3(-3.6f,0,z),R(0,360),Vector3.one*.55f);
                Put("GrassTuft",dressing,new Vector3(3.8f,0,z+1.3f),R(0,360),Vector3.one*.7f);
            }
            var laneSurface=stage.transform.Find("Lane/Lane surface");
            if(laneSurface) laneSurface.GetComponent<Renderer>().sharedMaterial=lane;
            var doors = stage.Doors.transform;
            foreach(var r in doors.GetComponentsInChildren<MeshRenderer>(true)) r.enabled=false;
            Remove(doors.Find("Canyon Gate Frame"));
            var frame=Put("ColonyGateFrame",doors,Vector3.zero); frame.name="Canyon Gate Frame";
            foreach(var side in new[] {"L","R"})
            {
                var hinge=doors.Find("Hinge "+side);
                if(!hinge) throw new InvalidOperationException("Missing existing door hinge "+side);
                Remove(hinge.Find("Canyon Leaf"));
                var leaf=Put(side=="L"?"ColonyDoorLeft":"ColonyDoorRight",hinge,Vector3.zero);
                leaf.name="Canyon Leaf";
            }
            var colony=stage.FinishMarker;
            var basin=colony.Find("Colony basin");
            if(basin) basin.GetComponent<Renderer>().enabled=false;
            Remove(colony.Find("Canyon Colony Garden"));
            var garden=Group("Canyon Colony Garden",colony);
            Put("ColonyBasin",garden,new Vector3(0,0,1.2f));
            Put("SucculentA",garden,new Vector3(-1.8f,0,1.7f),-30,Vector3.one*.85f);
            Put("SucculentB",garden,new Vector3(1.8f,0,1.6f),30,Vector3.one*.7f);
            Put("RockSmall",garden,new Vector3(-1.65f,0,.6f),45,Vector3.one*.55f);
            Put("RockSmall",garden,new Vector3(1.7f,0,2.3f),-30,Vector3.one*.7f);

            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Hex("C8DEEF")*.8f;
            RenderSettings.ambientEquatorColor=Hex("C4AD91")*.72f;
            RenderSettings.ambientGroundColor=Hex("6D6476")*.6f;
            var light=GameObject.Find("Gameplay Key Light")?.GetComponent<Light>();
            if(light) { light.shadows=LightShadows.Soft; light.shadowStrength=.75f; }
            var camera=stage.CameraRig.GetComponent<Camera>();
            camera.backgroundColor=Hex("DFB47F");
            // Also make the saved Edit-mode scene legible. The controller replaces these with each level's anchors.
            if (!EditorApplication.isPlaying)
            {
                var rules=config.CreateSnapshot();
                stage.Doors.Place((float)rules.DoorZ);
                stage.FinishMarker.position=new Vector3(0,0,(float)rules.FinishZ+.4f);
                stage.Track.Build(-1.5f,(float)rules.DoorZ,(float)(rules.CorridorHalfWidth(rules.D0)*2));
            }
            EditorSceneManager.MarkSceneDirty(stage.gameObject.scene);
        }

        private static Material Surface(string name,Color tint,float variation)
        {
            var path=Root+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m)
            {
                var shader=Shader.Find("BallGame/Environment/Canyon Surface");
                if(!shader) throw new InvalidOperationException("Canyon Surface shader has not imported.");
                m=new Material(shader){name=name}; AssetDatabase.CreateAsset(m,path);
            }
            m.SetColor("_BaseColor",tint); m.SetColor("_ShadowTint",new Color(.64f,.72f,.84f,1));
            m.SetFloat("_Brightness",1); m.SetFloat("_Variation",variation); EditorUtility.SetDirty(m); return m;
        }

        private static void MakePrefab(string name,Material material)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(CanyonModelImporter.Models+name+".fbx");
            if(!source) throw new InvalidOperationException("Missing Blender model "+name);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                instance.name=name;
                foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                    renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
                PrefabUtility.SaveAsPrefabAsset(instance,Root+"/Prefabs/"+name+".prefab");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        private static Transform Group(string name,Transform parent)
        { var go=new GameObject(name); go.transform.SetParent(parent,false); return go.transform; }
        private static void Remove(Transform t) { if(t) Object.DestroyImmediate(t.gameObject); }
        private static Transform Put(string name,Transform parent,Vector3 position,float yaw=0,Vector3? scale=null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            go.transform.localPosition=position; go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            go.transform.localScale=scale??Vector3.one;
            return go.transform;
        }
        private static Color Hex(string s) { ColorUtility.TryParseHtmlString("#"+s,out var c); return c; }

        [Serializable] private sealed class Manifest { public Entry[] assets; }
        [Serializable] private sealed class Entry { public string name; public float[] min,max; public int triangles; }
        [Serializable] private sealed class ValidationReport
        {
            public int models, triangles;
            public string axes;
            public float unitScale;
            public string[] errors;
        }

        [MenuItem("Tools/Ball Game/Environment/Validate Blender Axis And Models")]
        public static void ValidateFromMenu() => Debug.Log("[Canyon] "+ValidateAssets());

        public static string ValidateAssets()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Validate imported mesh data outside Play mode.");
            var manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Art/Environment/Canyon/v01/asset-manifest.json"));
            var errors=new List<string>(); var tris=0;
            foreach(var entry in manifest.assets)
            {
                var path=CanyonModelImporter.Models+entry.name+".fbx";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!asset) { errors.Add(entry.name+": missing"); continue; }
                var filters=asset.GetComponentsInChildren<MeshFilter>(true);
                if(filters.Length!=1) { errors.Add(entry.name+": expected a single mesh"); continue; }
                var f=filters[0]; var mesh=f.sharedMesh; var bounds=mesh.bounds;
                for(var k=0;k<3;k++)
                    if(Mathf.Abs(bounds.min[k]-entry.min[k])>.002f || Mathf.Abs(bounds.max[k]-entry.max[k])>.002f)
                        errors.Add(entry.name+": axis/scale mismatch on "+"XYZ"[k]);
                foreach(var t in asset.GetComponentsInChildren<Transform>(true))
                    if(t.localPosition.sqrMagnitude>.000001f || Quaternion.Angle(t.localRotation,Quaternion.identity)>.01f
                        || (t.localScale-Vector3.one).sqrMagnitude>.000001f)
                        errors.Add(entry.name+": nonidentity FBX transform "+t.name);
                if(mesh.colors.Length!=mesh.vertexCount) errors.Add(entry.name+": missing vertex palette");
                var count=(int)mesh.GetIndexCount(0)/3; tris+=count;
                if(count!=entry.triangles) errors.Add(entry.name+": unexpected triangle count");
                if(asset.GetComponentsInChildren<Collider>(true).Length>0) errors.Add(entry.name+": decorative collider");
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);
                if(!importer.bakeAxisConversion || importer.globalScale!=1) errors.Add(entry.name+": import policy mismatch");
                if(entry.name=="GroundTile" && mesh.normals.Any(n=>n.y<.99f)) errors.Add("GroundTile: normals must point +Y");
            }
            var probe=AssetDatabase.LoadAssetAtPath<GameObject>(CanyonModelImporter.Models+"AxisProbe.fbx").GetComponentInChildren<MeshFilter>().sharedMesh.bounds;
            if((probe.min-new Vector3(-.05f,-.05f,-.05f)).sqrMagnitude>.00001f
                || (probe.max-new Vector3(1.05f,3.05f,2.05f)).sqrMagnitude>.00001f) errors.Add("Signed axis probe failed");
            var report=new ValidationReport { models=manifest.assets.Length, triangles=tris, axes="X right / Y up / +Z forward", unitScale=1, errors=errors.ToArray() };
            var json=JsonUtility.ToJson(report,true);
            File.WriteAllText("Art/Environment/Canyon/v01/unity-validation.json",json);
            if(errors.Count>0) throw new InvalidOperationException(json);
            return json;
        }
    }
}
