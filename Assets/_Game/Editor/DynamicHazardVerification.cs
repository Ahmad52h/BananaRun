using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MaekoStudio.Flock;
using Object = UnityEngine.Object;

public static class DynamicHazardVerification
{
    const string Report = "Temp/dynamic-hazards-verification.txt";
    static RunnerMotor motor;
    static PlayerHealth health;
    static RunnerCamera camera;
    static DynamicRunnerHazard hazard;
    static PowerUpManager powers;
    static int scenario, phase, hearts, frame;
    static float started, baselineFov, baselineDistance;
    static double deadline;
    static bool impact, debris, loudSmash;
    static Vector3 flockStart;
    static Transform bird;
    [InitializeOnLoadMethod] static void Register()
    {
        EditorApplication.playModeStateChanged -= State;
        EditorApplication.playModeStateChanged += State;
    }
    [MenuItem("Tools/Banana Run/Verify Dynamic Jungle Hazards")]
    public static void Begin()
    {
        if (EditorApplication.isPlaying) throw new Exception("Stop play mode first.");
        File.WriteAllText(Report,"Dynamic jungle runtime checks\n");
        SessionState.SetBool("DynamicHazardTest",true);
        EditorApplication.isPlaying=true;
    }
    static void State(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("DynamicHazardTest",false)) return;
        SessionState.SetBool("DynamicHazardTest",false);
        Application.runInBackground=true; Time.timeScale=1;
        motor=Object.FindFirstObjectByType<RunnerMotor>(); health=motor.GetComponent<PlayerHealth>();
        powers=motor.GetComponent<PowerUpManager>(); camera=Object.FindFirstObjectByType<RunnerCamera>();
        scenario=phase=0; frame=Time.frameCount; started=Time.time;
        deadline=EditorApplication.timeSinceStartup+100;
        EditorApplication.update+=Tick;
    }
    static void Check(bool value,string message)
    {
        if(!value) throw new Exception(message);
        File.AppendAllText(Report,"PASS: "+message+"\n");
    }
    static void Place()
    {
        var cc=motor.GetComponent<CharacterController>(); motor.enabled=false; cc.enabled=false;
        motor.transform.position=new Vector3(0,-0.22f,100);
        cc.enabled=true; motor.enabled=true; motor.ResetVerticalVelocity(); motor.LockLaneSwitching(0f);
        cc.Move(Vector3.down*0.1f); Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying){EditorApplication.update-=Tick;return;}
        EditorApplication.QueuePlayerLoopUpdate();
        try
        {
            if(EditorApplication.timeSinceStartup>deadline) throw new Exception("Verification timed out.");
            float elapsed=Time.time-started;
            if(phase==0)
            {
                if(elapsed<1) return;
                Check(Time.frameCount>frame+3,"Player frames advance.");
                var flocks=Object.FindObjectsByType<MaekoFlock>(FindObjectsSortMode.None);
                Check(flocks.Length==2 && flocks.Sum(f=>f.BirdCount)==56,"Both Maeko murmurations spawn 56 birds total.");
                Check(flocks.All(f=>f.GetComponentsInChildren<Collider>().Length==0),"Ambient birds have no gameplay colliders.");
                bird=flocks[0].GetComponentInChildren<Animator>().transform; flockStart=bird.position;
                var hazards=Object.FindObjectsByType<DynamicRunnerHazard>(FindObjectsSortMode.None);
                Check(Enum.GetValues(typeof(DynamicRunnerHazard.Motion)).Cast<DynamicRunnerHazard.Motion>().All(k=>hazards.Any(h=>h.Kind==k)),"Course spawns all three dynamic obstacle types.");
                // Isolate each encounter from the authored obstacles along the test lane.
                foreach (var obstacle in Object.FindObjectsByType<RunnerObstacle>(FindObjectsSortMode.None)) obstacle.gameObject.SetActive(false);
                foreach (var pickup in Object.FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None)) pickup.gameObject.SetActive(false);
                foreach (var peel in Object.FindObjectsByType<BananaPeelHazard>(FindObjectsSortMode.None)) peel.gameObject.SetActive(false);
                baselineFov=camera.GetComponent<Camera>().fieldOfView;
                baselineDistance=motor.transform.position.z-camera.transform.position.z;
                phase=1; started=Time.time;
            }
            else if(phase==1)
            {
                if(elapsed<2.2f) return;
                if(scenario==0) Check(Vector3.Distance(flockStart,bird.position)>0.5f,"Maeko birds move through the scene.");
                if(hazard!=null) Object.Destroy(hazard.gameObject);
                Place();
                string name=scenario==1?"ChargingWarthog":scenario==2 || scenario==5?"RollingBoulder":"LowFlyingStarling";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Course/"+name+".prefab");
                var go=Object.Instantiate(prefab,new Vector3(0,-0.3f,scenario==4?101.5f:105),Quaternion.identity);
                hazard=go.GetComponent<DynamicRunnerHazard>();
                Check(go.GetComponent<Rigidbody>().isKinematic && go.GetComponent<BoxCollider>().isTrigger,"Scenario "+scenario+": kinematic trigger configured.");
                hearts=health.Hearts;
                if(scenario==0) Check(motor.Slide(),"Slide starts before low bird.");
                if(scenario==1 || scenario==2 || scenario==4) motor.Jump();
                if(scenario==5){powers.Activate(PowerUpType.Bulldozer);impact=debris=loudSmash=false;}
                started=Time.time; phase=2;
            }
            else if(phase==2)
            {
                if(scenario==5)
                {
                    var effect=Object.FindFirstObjectByType<GiantImpactEffect>();
                    if(effect!=null)
                    {
                        impact=true;
                        debris|=effect.GetComponentsInChildren<ParticleSystem>().Any(p=>p.particleCount>0);
                        loudSmash|=effect.GetComponents<AudioSource>().Any(a=>a.isPlaying && a.volume==1 && !a.loop);
                    }
                }
                if(elapsed<1.5f) return;
                Check(hazard.DistanceTravelled>0.1f,"Scenario "+scenario+": obstacle moves toward runner.");
                if(scenario<=2) Check(health.Hearts==hearts,scenario==0?"Sliding clears Maeko bird without damage.":"Jump clears "+hazard.Kind+" without damage.");
                else if(scenario<=4) Check(health.Hearts==hearts-1,(scenario==3?"Running into bird takes one hit.":"Jumping into bird takes one hit.") + " Hearts: " + hearts + " -> " + health.Hearts);
                else
                {
                    Check(!hazard.gameObject.activeSelf && health.Hearts==hearts,"Giant instantly shatters rolling boulder without damage.");
                    Check(impact && debris && loudSmash,"Crushing plays full-volume smash and dust/debris.");
                    Check(camera.GetComponent<Camera>().fieldOfView>74.5f,"Giant FOV expands from 60 to 75.");
                    Check(motor.transform.position.z-camera.transform.position.z>baselineDistance+3,"Giant camera pulls back.");
                    Check(camera.HeavyStepsPlayed>0,"Grounded giant produces rhythmic heavy steps.");
                    phase=3; started=Time.time; return;
                }
                scenario++; phase=1; started=Time.time;
            }
            else if(phase==3)
            {
                if(powers.IsActive(PowerUpType.Bulldozer) || elapsed<5) return;
                Check(Mathf.Abs(camera.GetComponent<Camera>().fieldOfView-baselineFov)<0.15f,"Camera FOV returns after power expiry.");
                Check(Mathf.Abs(motor.transform.position.z-camera.transform.position.z-baselineDistance)<0.15f,"Camera distance returns after power expiry.");
                File.AppendAllText(Report,"ALL CHECKS PASSED\n");
                EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
            }
        }
        catch(Exception e)
        {
            File.AppendAllText(Report,"FAIL: "+e.Message+"\n");
            EditorApplication.update-=Tick; EditorApplication.isPlaying=false;
        }
    }
}


