using System.Collections.Generic;
using UnityEngine;

namespace MaekoStudio.Flock
{
    /// <summary>
    /// Maeko Flock v3.
    /// One MaekoFlock = one blob. Drop several controllers in a scene,
    /// each with its own anchor, for multiple murmurations.
    ///
    /// Modes: CalmSwirl (default), Panic (scatter then recover),
    /// DiveAttack (orbit a target, birds take turns diving).
    /// Extras: blob clustering, drifters, wind gusts, flap bob,
    /// procedural head stabilization, obstacle avoidance, banking,
    /// Fly/Glide animator switching.
    ///
    /// Script API:
    ///   flock.SetMode(MaekoFlock.FlockMode.DiveAttack);
    ///   flock.TriggerPanic();
    ///   flock.TriggerPanic(explosionPosition);
    /// Right click the component header for Test Panic while playing.
    /// </summary>
    public class MaekoFlock : MonoBehaviour
    {
        public enum FlockMode { CalmSwirl, Panic, DiveAttack, Hover }

        [Header("Spawn")]
        public GameObject birdPrefab;
        [Range(1, 500)] public int count = 120;
        [Tooltip("Flock home. Move it at runtime and the murmuration follows.")]
        public Transform anchor;
        [Tooltip("How far the blob may travel from the anchor.")]
        public float boundsRadius = 25f;

        [Header("Mode")]
        public FlockMode mode = FlockMode.CalmSwirl;
        [Tooltip("DiveAttack target. Falls back to the anchor.")]
        public Transform diveTarget;
        [Tooltip("Optional panic source. TriggerPanic scatters away from this. Falls back to the anchor.")]
        public Transform panicSource;

        [Header("Patrol")]
        [Tooltip("Assign 2 or more points and the flock flies to each in order, then loops back to the first. Leave empty to stay home around the anchor.")]
        public List<Transform> patrolPoints = new List<Transform>();
        [Tooltip("Travel speed of the flock along the patrol route.")]
        public float patrolSpeed = 6f;

        [Header("Spawn & Despawn")]
        [Tooltip("When the flock appears. On Start = always there. On Trigger Enter = spawns when something tagged with playerTag enters a trigger collider on this object. By Script = call SpawnFlock().")]
        public SpawnTrigger spawnWhen = SpawnTrigger.OnStart;
        [Tooltip("Tag that fires the trigger spawn, usually Player.")]
        public string playerTag = "Player";
        [Tooltip("Birds arrive from and depart toward a point this far away, flying in and out like a real migrating flock.")]
        public float arriveFromDistance = 80f;
        [Tooltip("Untick to spawn instantly in place instead of flying in from far away.")]
        public bool arriveFromFar = true;
        [Tooltip("What makes the flock leave. Departing birds fly far away and despawn out of sight.")]
        public DespawnRule despawnWhen = DespawnRule.Never;
        [Tooltip("Lifetime before leaving (Despawn When = After Seconds).")]
        public float lifeSeconds = 60f;
        [Tooltip("Full patrol loops completed before leaving (Despawn When = After Patrol Loops).")]
        public int patrolLoopsBeforeLeaving = 1;

        public enum SpawnTrigger { OnStart, OnTriggerEnter, ByScript }
        public enum DespawnRule { Never, AfterSeconds, AfterPatrolLoops }

        [Header("Blob")]
        [Tooltip("Personal space per bird. The ball's size GROWS with bird count automatically so spacing stays constant: 500 birds make a visibly bigger ball than 100.")]
        public float birdSpacing = 1.5f;
        [Tooltip("Minimum radius of the ball. The actual radius is whichever is larger: this, or the size needed to fit all birds at Bird Spacing.")]
        public float blobRadius = 7f;
        [Tooltip("How hard birds are pulled back into the blob.")]
        public float blobTightness = 2.2f;
        [Tooltip("How strongly every bird matches the flock's shared direction. This is what makes the blob move as one unit.")]
        public float groupUnity = 1.3f;

        [Header("Organic Motion")]
        [Tooltip("Slow expand and contract of the whole ball, like the flock is breathing. 0 disables.")]
        [Range(0f, 1f)] public float breathe = 0.35f;
        [Tooltip("Slow wandering of the ball's centre so the clump sways instead of gliding on rails. 0 disables.")]
        [Range(0f, 1f)] public float sway = 0.4f;
        [Tooltip("Occasional sharp sideways jerks, like real starlings dodging. 0 disables.")]
        [Range(0f, 1f)] public float twitch = 0.4f;
        [Tooltip("Birds never fly lower than this many units below the anchor. Keeps the flock off the ground.")]
        public float floorBelowAnchor = 6f;

        [Header("Flight")]
        public float minSpeed = 4f;
        public float maxSpeed = 9f;
        public float steerForce = 7f;

        [Header("Flocking")]
        public float perception = 5f;
        public float separationDistance = 1.1f;
        public float separationWeight = 2.0f;
        public float alignmentWeight = 0.8f;
        public float cohesionWeight = 0.3f;
        public float wanderWeight = 0.4f;
        [Tooltip("Shared circulation around the anchor so the blob orbits and folds.")]
        public float swirlWeight = 0.8f;

        public enum SwirlPattern { Circle, FigureEight, Racetrack }
        [Tooltip("Path the flock traces around the anchor in Calm Swirl. Ignored while patrolling.")]
        public SwirlPattern swirlPattern = SwirlPattern.FigureEight;
        [Tooltip("Size of the swirl path around the anchor. Smaller keeps the flock tighter on the anchor.")]
        public float swirlRadius = 10f;
        [Tooltip("How fast the flock travels around the swirl path.")]
        public float swirlSpeed = 0.25f;
        [Tooltip("How much the ball stretches along its direction of travel, like liquid. 0 keeps a rigid sphere.")]
        [Range(0f, 1f)] public float stretch = 0.5f;

        [Header("Life")]
        [Tooltip("Chance per bird per second to peel off and drift alone for a moment.")]
        [Range(0f, 0.2f)] public float drifterChance = 0.02f;
        public float drifterTime = 5f;
        [Tooltip("Slow shared wind gusts that push the whole blob. Zero disables.")]
        public float windStrength = 0.5f;
        [Tooltip("Tiny vertical body bob synced to the flap. Zero disables.")]
        public float flapBob = 0.02f;

        [Header("Head")]
        [Tooltip("Name of the head bone from the rig. Leave as Head for the MeshPrep skeleton.")]
        public string headBoneName = "Head";
        [Tooltip("0 = head follows body pitch fully. 1 = head stays perfectly level. 0.7 keeps a natural tilt with climbs and dives.")]
        [Range(0f, 1f)] public float headStabilize = 0.7f;

        [Header("Panic")]
        public float panicDuration = 4f;
        public float panicSpeedBoost = 1.7f;

        [Header("Dive Attack")]
        public float orbitRadius = 6f;
        [Range(1, 20)] public int maxDivers = 3;
        public float diveSpeedBoost = 1.5f;

        [Header("Obstacles")]
        [Tooltip("Layers birds steer around. Set this to your terrain and buildings.")]
        public LayerMask obstacleMask;
        public float avoidDistance = 5f;
        public float avoidWeight = 3.5f;

        [Header("Audio")]
        [Tooltip("Looping flock chatter. Plays as 3D sound from the centre of the ball: pans left and right and fades with distance automatically.")]
        public AudioClip chirpClip;
        [Range(0f, 1f)] public float chirpVolume = 0.8f;
        [Tooltip("Distance at which the chatter fades to silence.")]
        public float chirpMaxDistance = 90f;

        [Header("Feel")]
        public float bankAmount = 50f;
        public float animatorSyncScale = 1.2f;

        [Header("Animator States")]
        public string flyState = "Maeko_Fly";
        public string glideState = "Maeko_Glide";
        [Tooltip("How often birds pulse into a glide during level flight. 0 = almost pure flapping, 1 = frequent soaring.")]
        [Range(0f, 1f)] public float glideAmount = 0.25f;
        [Tooltip("Per-bird wingbeat variation. Every bird keeps its own tempo so the flock never flaps in unison. 0 = all identical.")]
        [Range(0f, 0.6f)] public float flapVariation = 0.3f;

        // ------------------------------------------------------------------

        class Bird
        {
            public Transform t;
            public Vector3 velocity;
            public Animator anim;
            public float personality;
            public float flapTempo;
            public float wanderSeed;
            public float bank;

            // drifter: > 0 drifting away, (-ReturnTime, 0] flying back
            public float driftPhase = -999f;

            public bool diving;
            public float diveCooldown;

            public bool hasGlide;
            public bool gliding;
            public int flyHash;
            public int glideHash;
            public float glidePulse;
            public float glideCooldown;

            // stray station relative to the ball, and twitch timing
            public float driftFwd, driftSide, driftUp;
            public float twitchTimer;

            // flap bob
            public Transform meshRoot;
            public float meshBaseY;
            public float bobPhase;

            // head stabilization
            public Transform head;
            public Quaternion headOffset;
        }

        const float DriftReturnTime = 3f;

        readonly List<Bird> birds = new List<Bird>();
        float panicTimer;
        Vector3 panicPoint;
        FlockMode lastMode;
        int activeDivers;
        Vector3 centroid;
        Vector3 blobTarget;
        float blobRadiusLive;
        Vector3 home;
        int patrolIndex;
        Vector3 swirlPoint;
        AudioSource chirpSource;
        float stretchLive = 1f;
        Vector3 pullTarget;

        enum LifeState { Empty, Living, Departing }
        LifeState state = LifeState.Empty;
        float lifeTimer;
        int loopsDone;
        Vector3 arriveDir;
        Vector3 departPoint;
        Vector3 lastAvgVel;

        Vector3 Center => anchor != null ? anchor.position : transform.position;
        Vector3 DivePoint => diveTarget != null ? diveTarget.position : Center;

        // radius that fits every bird at birdSpacing: sphere packing,
        // r grows with the cube root of the count
        float BaseRadius
        {
            get
            {
                int n = birds.Count > 0 ? birds.Count : count;
                float packing = 0.62f * birdSpacing * Mathf.Pow(Mathf.Max(1, n), 1f / 3f);
                return Mathf.Max(blobRadius, packing);
            }
        }

        // ------------------------------------------------------------------ API

        public void SetMode(FlockMode m) { mode = m; }

        /// <summary>Live bird count. 0 means despawned or not yet spawned.</summary>
        public int BirdCount => birds.Count;

        public void TriggerPanic()
        {
            TriggerPanic(panicSource != null ? panicSource.position : Center);
        }

        public void TriggerPanic(Vector3 fromPoint)
        {
            mode = FlockMode.Panic;
            panicPoint = fromPoint;
            panicTimer = panicDuration;
        }

        [ContextMenu("Test Panic")]
        void TestPanic() { TriggerPanic(); }

        // ------------------------------------------------------------------

        void Start()
        {
            home = Center;

            // 3D chirp source that lives at the centre of the ball.
            // spatialBlend 1 gives left right panning and distance falloff
            // from the AudioListener for free.
            if (chirpClip != null)
            {
                GameObject audioGo = new GameObject("FlockChirp");
                audioGo.transform.SetParent(transform, false);
                chirpSource = audioGo.AddComponent<AudioSource>();
                chirpSource.clip = chirpClip;
                chirpSource.loop = true;
                chirpSource.playOnAwake = false;
                chirpSource.volume = chirpVolume;
                chirpSource.spatialBlend = 1f;
                chirpSource.dopplerLevel = 0.15f;
                chirpSource.rolloffMode = AudioRolloffMode.Linear;
                chirpSource.minDistance = blobRadius;
                chirpSource.maxDistance = chirpMaxDistance;
            }

            lastMode = mode;

            if (spawnWhen == SpawnTrigger.OnStart)
                SpawnFlock();
        }

        /// <summary>Spawn the flock. Birds fly in from far away unless
        /// arriveFromFar is off. Safe to call from gameplay scripts.</summary>
        [ContextMenu("Spawn Flock")]
        public void SpawnFlock()
        {
            if (birds.Count > 0) return;
            if (birdPrefab == null)
            {
                Debug.LogWarning("MaekoFlock: no bird prefab assigned.");
                return;
            }

            // pick a far away horizon point to stream in from
            Vector2 flat = Random.insideUnitCircle.normalized;
            arriveDir = new Vector3(flat.x, 0f, flat.y);
            Vector3 spawnCenter = home;
            Vector3 inVel = Vector3.zero;
            if (arriveFromFar)
            {
                spawnCenter = home + arriveDir * arriveFromDistance
                              + Vector3.up * arriveFromDistance * 0.12f;
                inVel = (home - spawnCenter).normalized * maxSpeed * 0.9f;
            }

            for (int i = 0; i < count; i++)
            {
                // strung out along the incoming line so they arrive as a
                // stream, not a ball teleported to the horizon
                Vector3 pos = spawnCenter
                              + arriveDir * (arriveFromFar ? Random.Range(0f, BaseRadius * 3f) : 0f)
                              + Random.insideUnitSphere * BaseRadius;
                GameObject go = Instantiate(birdPrefab, pos, Random.rotation, transform);

                Bird b = new Bird
                {
                    t = go.transform,
                    velocity = arriveFromFar
                        ? inVel + Random.insideUnitSphere * (maxSpeed * 0.15f)
                        : Random.onUnitSphere * ((minSpeed + maxSpeed) * 0.5f),
                    anim = go.GetComponentInChildren<Animator>(),
                    personality = Random.Range(0.93f, 1.07f),
                    flapTempo = 1f + Random.Range(-flapVariation, flapVariation),
                    wanderSeed = Random.Range(0f, 1000f),
                    diveCooldown = Random.Range(3f, 15f),
                    bobPhase = Random.Range(0f, 6.28f),
                    twitchTimer = Random.Range(2f, 8f),
                    glideCooldown = Random.Range(0.5f, 7f),
                };

                if (b.anim != null)
                {
                    b.anim.Play(0, 0, Random.value);   // desync the flaps
                    b.flyHash = ResolveState(b.anim, flyState);
                    b.glideHash = ResolveState(b.anim, glideState);
                    b.hasGlide = b.flyHash != 0 && b.glideHash != 0;
                }

                if (b.t.childCount > 0)
                {
                    b.meshRoot = b.t.GetChild(0);
                    b.meshBaseY = b.meshRoot.localPosition.y;
                }

                b.head = FindDeepChild(b.t, headBoneName);
                if (b.head != null)
                    b.headOffset = Quaternion.Inverse(b.t.rotation) * b.head.rotation;

                birds.Add(b);
            }

            lifeTimer = 0f;
            loopsDone = 0;
            state = LifeState.Living;
            if (chirpSource != null) chirpSource.Play();
        }

        /// <summary>Send the flock away: birds stream toward a far point
        /// and despawn out of sight. Safe to call from gameplay scripts.</summary>
        [ContextMenu("Despawn Flock")]
        public void DespawnFlock()
        {
            if (birds.Count == 0 || state == LifeState.Departing) return;

            // leave along the current direction of travel if there is one,
            // otherwise back the way they came
            Vector3 d = new Vector3(lastAvgVel.x, 0f, lastAvgVel.z);
            Vector3 dir = d.sqrMagnitude > 0.5f ? d.normalized
                        : (arriveDir.sqrMagnitude > 0.5f ? arriveDir : Vector3.forward);
            departPoint = home + dir * arriveFromDistance
                          + Vector3.up * arriveFromDistance * 0.15f;
            state = LifeState.Departing;
        }

        void OnTriggerEnter(Collider other)
        {
            if (spawnWhen != SpawnTrigger.OnTriggerEnter) return;
            if (birds.Count > 0) return;
            if (!string.IsNullOrEmpty(playerTag) && !other.CompareTag(playerTag)) return;
            SpawnFlock();
        }

        static Transform FindDeepChild(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Transform child in root)
            {
                if (child.name == name) return child;
                Transform found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Find a state by its plain name OR by Blender's
        /// exported clip name ("Object|Action"), which is what Unity
        /// names states when clips are dragged into a controller.</summary>
        static int ResolveState(Animator a, string wanted)
        {
            if (a == null || a.runtimeAnimatorController == null) return 0;

            int h = Animator.StringToHash(wanted);
            if (a.HasState(0, h)) return h;

            foreach (var clip in a.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.EndsWith(wanted)) continue;
                int h2 = Animator.StringToHash(clip.name);
                if (a.HasState(0, h2)) return h2;
            }
            return 0;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || birds.Count == 0) return;

            Vector3 center = Center;

            // ---------- patrol: home travels along the route and the
            // whole system (bounds, floor, swirl) follows it
            if (patrolPoints != null && patrolPoints.Count >= 2)
            {
                Transform wp = patrolPoints[patrolIndex % patrolPoints.Count];
                if (wp != null)
                {
                    home = Vector3.MoveTowards(home, wp.position, patrolSpeed * dt);
                    if ((home - wp.position).sqrMagnitude < 6f)
                    {
                        patrolIndex = (patrolIndex + 1) % patrolPoints.Count;
                        if (patrolIndex == 0)
                        {
                            loopsDone++;
                            if (state == LifeState.Living
                                && despawnWhen == DespawnRule.AfterPatrolLoops
                                && loopsDone >= patrolLoopsBeforeLeaving)
                                DespawnFlock();
                        }
                    }
                    center = home;
                }
            }
            else
            {
                home = center;
            }

            // ---------- lifecycle
            if (state == LifeState.Living)
            {
                lifeTimer += dt;
                if (despawnWhen == DespawnRule.AfterSeconds && lifeTimer >= lifeSeconds)
                    DespawnFlock();
            }
            if (state == LifeState.Departing)
                center = departPoint;   // every homing force now aims far away

            // entering Panic from the inspector still needs a timer and a point
            if (mode == FlockMode.Panic && lastMode != FlockMode.Panic && panicTimer <= 0f)
            {
                panicPoint = panicSource != null ? panicSource.position : center;
                panicTimer = panicDuration;
            }
            lastMode = mode;

            if (mode == FlockMode.Panic)
            {
                panicTimer -= dt;
                if (panicTimer <= 0f) mode = FlockMode.CalmSwirl;   // recover
            }

            // blob center of mass and shared velocity
            centroid = Vector3.zero;
            Vector3 avgVel = Vector3.zero;
            for (int i = 0; i < birds.Count; i++)
            {
                centroid += birds[i].t.position;
                avgVel += birds[i].velocity;
            }
            centroid /= birds.Count;
            avgVel /= birds.Count;
            lastAvgVel = avgVel;

            // organic motion: the ball slowly breathes and its target sways
            blobRadiusLive = BaseRadius;
            if (breathe > 0f)
                blobRadiusLive *= 1f + breathe * 0.3f * Mathf.Sin(Time.time * 0.7f);
            blobTarget = centroid;
            if (sway > 0f)
            {
                float st = Time.time;
                blobTarget += new Vector3(
                    Mathf.PerlinNoise(st * 0.09f, 3.7f) - 0.5f,
                    (Mathf.PerlinNoise(7.7f, st * 0.07f) - 0.5f) * 0.5f,
                    Mathf.PerlinNoise(st * 0.08f, 11.3f) - 0.5f)
                    * (blobRadiusLive * sway * 1.6f);
            }

            // ---------- where the flock is being led
            if (state == LifeState.Departing)
            {
                swirlPoint = departPoint;   // leaving: chase the horizon
            }
            else if (patrolPoints != null && patrolPoints.Count >= 2)
            {
                swirlPoint = center;   // patrolling: chase the moving home
            }
            else if (mode == FlockMode.Hover)
            {
                swirlPoint = center;   // hover: sit right on the anchor
            }
            else
            {
                float a = Time.time * swirlSpeed;
                Vector3 off;
                switch (swirlPattern)
                {
                    case SwirlPattern.FigureEight:
                        off = new Vector3(Mathf.Sin(a), 0f,
                                          Mathf.Sin(a) * Mathf.Cos(a)) * swirlRadius;
                        break;
                    case SwirlPattern.Racetrack:
                        off = new Vector3(Mathf.Cos(a) * swirlRadius, 0f,
                                          Mathf.Sin(a) * swirlRadius * 0.55f);
                        break;
                    default:
                        off = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * swirlRadius;
                        break;
                }
                off.y = Mathf.Sin(a * 0.7f) * swirlRadius * 0.15f;
                swirlPoint = center + off;
            }

            // hover pulls the ball in tighter on the anchor
            if (mode == FlockMode.Hover)
                blobRadiusLive *= 0.85f;

            // liquid stretch: the faster the flock travels, the longer the
            // ball is allowed to be along its direction of travel
            float flockSpeedNorm = Mathf.Clamp01(avgVel.magnitude / Mathf.Max(maxSpeed, 0.01f));
            stretchLive = 1f + stretch * 1.4f * flockSpeedNorm;

            // pull target sits slightly BEHIND the geometric centre while
            // moving, so the mass settles central instead of nose heavy
            pullTarget = blobTarget;
            if (flockSpeedNorm > 0.05f)
                pullTarget -= avgVel.normalized * blobRadiusLive * 0.25f * flockSpeedNorm;

            // the chirp follows the ball
            if (chirpSource != null)
            {
                chirpSource.transform.position = centroid;
                chirpSource.volume = chirpVolume;
            }

            activeDivers = 0;
            for (int i = 0; i < birds.Count; i++) if (birds[i].diving) activeDivers++;

            // shared wind, slow perlin gusts that push the whole blob together
            Vector3 wind = Vector3.zero;
            if (windStrength > 0f)
            {
                float wt = Time.time * 0.15f;
                wind = new Vector3(
                    Mathf.PerlinNoise(wt, 0.3f) - 0.5f,
                    (Mathf.PerlinNoise(0.7f, wt) - 0.5f) * 0.4f,
                    Mathf.PerlinNoise(wt, 7.1f) - 0.5f) * (windStrength * 2f);
            }

            Vector3 divePoint = DivePoint;

            for (int i = 0; i < birds.Count; i++)
                UpdateBird(birds[i], i, dt, center, divePoint, wind, avgVel);

            // departing birds vanish once they reach the horizon point
            if (state == LifeState.Departing)
            {
                float goneSq = blobRadius * 2f;
                goneSq *= goneSq;
                for (int i = birds.Count - 1; i >= 0; i--)
                {
                    if ((birds[i].t.position - departPoint).sqrMagnitude < goneSq)
                    {
                        Destroy(birds[i].t.gameObject);
                        birds.RemoveAt(i);
                    }
                }
                if (birds.Count == 0)
                {
                    state = LifeState.Empty;
                    if (chirpSource != null) chirpSource.Stop();
                }
            }
        }

        // ------------------------------------------------------------------

        void UpdateBird(Bird b, int index, float dt, Vector3 center,
                        Vector3 divePoint, Vector3 wind, Vector3 avgVel)
        {
            bool calm = mode == FlockMode.CalmSwirl;
            bool hover = mode == FlockMode.Hover;
            bool calmLike = calm || hover;
            bool panic = mode == FlockMode.Panic;
            bool diveMode = mode == FlockMode.DiveAttack;

            // ---------- drifter lifecycle (calm and hover only)
            if (calmLike)
            {
                if (b.driftPhase <= -DriftReturnTime
                    && Random.value < drifterChance * dt)
                {
                    b.driftPhase = drifterTime * Random.Range(0.6f, 1.4f);
                    // pick a station anywhere around the ball: ahead,
                    // beside, above, so strays lead as often as they trail
                    b.driftFwd = Random.Range(0.1f, 1.4f);
                    b.driftSide = Random.Range(-1.5f, 1.5f);
                    b.driftUp = Random.Range(-0.2f, 0.6f);
                }
                else
                    b.driftPhase -= dt;
            }
            else
            {
                b.driftPhase = -999f;
            }

            bool drifting = calmLike && b.driftPhase > 0f;
            bool returning = calmLike && !drifting && b.driftPhase > -DriftReturnTime;

            // ---------- neighbours
            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            int neighbours = 0;

            for (int j = 0; j < birds.Count; j++)
            {
                if (j == index) continue;
                Vector3 offset = birds[j].t.position - b.t.position;
                float dist = offset.magnitude;
                if (dist > perception || dist < 0.0001f) continue;

                neighbours++;
                alignment += birds[j].velocity;
                cohesion += birds[j].t.position;
                if (dist < Mathf.Max(separationDistance, birdSpacing * 0.7f))
                    separation -= offset / (dist * dist);
            }

            Vector3 steer = wind;
            float paceBoost = 1f;   // catch-up speed licence for stragglers

            float sepW = separationWeight * (panic ? 2.2f : 1f);
            float aliW = alignmentWeight * (drifting ? 0.1f : panic ? 0.4f : 1f);
            float cohW = cohesionWeight * (drifting ? 0f : 1f);

            if (neighbours > 0)
            {
                alignment = (alignment / neighbours).normalized * maxSpeed - b.velocity;
                cohesion = ((cohesion / neighbours) - b.t.position).normalized * maxSpeed - b.velocity;
                steer += separation * sepW;
                steer += alignment * aliW;
                steer += cohesion * cohW;
            }

            // ---------- blob pull, one dense evenly spaced ball
            if (!drifting && !panic && !diveMode)
            {
                Vector3 toBlob = pullTarget - b.t.position;
                // stretched distance metric: while the flock travels, the
                // along-travel axis counts as shorter, so the ball is
                // allowed to elongate like liquid in that direction
                float d;
                if (stretchLive > 1.001f && avgVel.sqrMagnitude > 0.01f)
                {
                    Vector3 tdir = avgVel.normalized;
                    float along = Vector3.Dot(toBlob, tdir) / stretchLive;
                    Vector3 lat = toBlob - tdir * Vector3.Dot(toBlob, tdir);
                    d = Mathf.Sqrt(along * along + lat.sqrMagnitude);
                }
                else
                {
                    d = toBlob.magnitude;
                }

                float tight = blobTightness * (hover ? 1.4f : 1f);
                // spring with a smaller free core, so centering reaches
                // deeper into the ball and the mass stays central
                float spring = Mathf.InverseLerp(blobRadiusLive * 0.35f, blobRadiusLive, d);
                float pull = spring * tight;
                // hard wall past the radius so nobody lingers outside
                float edge = Mathf.InverseLerp(blobRadiusLive, blobRadiusLive * 1.4f, d);
                pull += edge * tight * 4f;
                steer += toBlob.normalized * maxSpeed * pull * (returning ? 3f : 1f);

                if (avgVel.sqrMagnitude > 0.01f)
                {
                    Vector3 travel = avgVel.normalized;
                    float lag = Vector3.Dot(blobTarget - b.t.position, travel);
                    // birds toward the front feel the group pull less, so
                    // the mass settles around the centre instead of the nose
                    float ahead = Mathf.Clamp01(-lag / Mathf.Max(blobRadiusLive, 0.01f));
                    steer += (travel * maxSpeed - b.velocity)
                             * groupUnity * (1f - 0.65f * ahead);

                    if (lag > blobRadiusLive * 0.25f)
                    {
                        // trailing birds steer forward AND get a real speed
                        // boost, otherwise their speed cap makes catching
                        // up physically impossible
                        steer += travel * maxSpeed * (lag / blobRadiusLive) * 1.4f;
                        paceBoost = 1f + Mathf.Clamp01(lag / blobRadiusLive) * 0.5f;
                    }
                    else if (lag < -blobRadiusLive * 0.35f)
                    {
                        // birds pressing on the front edge ease off so the
                        // ball stops packing against its own nose
                        paceBoost = 0.87f;
                    }
                }
            }
            else if (drifting && avgVel.sqrMagnitude > 0.01f)
            {
                // strays hold a station that travels WITH the ball:
                // ahead of it, out to the side, or above
                Vector3 travel = avgVel.normalized;
                Vector3 side = Vector3.Cross(Vector3.up, travel);
                Vector3 spot = blobTarget
                               + (travel * b.driftFwd
                                  + side * b.driftSide
                                  + Vector3.up * b.driftUp) * blobRadiusLive * 1.3f;
                steer += (spot - b.t.position).normalized * maxSpeed * 0.9f;
            }

            // ---------- mode forces
            if (calmLike)
            {
                // chase the moving lead point: this traces the figure
                // eight or racetrack, or follows the patrol route
                Vector3 toLead = swirlPoint - b.t.position;
                if (toLead.sqrMagnitude > 1f)
                    steer += toLead.normalized * swirlWeight * maxSpeed
                             * (drifting ? 0.3f : 0.9f);
            }
            else if (panic)
            {
                Vector3 away = (b.t.position - panicPoint).normalized;
                away.y = Mathf.Abs(away.y) * 0.5f + 0.35f;   // panicked birds climb
                steer += away.normalized * maxSpeed * 3f;
            }
            else if (diveMode)
            {
                if (!b.diving)
                {
                    b.diveCooldown -= dt;
                    if (b.diveCooldown <= 0f && activeDivers < maxDivers)
                    {
                        b.diving = true;
                        activeDivers++;
                    }

                    Vector3 fromT = b.t.position - divePoint;
                    Vector3 flat = new Vector3(fromT.x, 0f, fromT.z);
                    if (flat.sqrMagnitude < 0.01f) flat = Vector3.forward;
                    Vector3 ringPoint = divePoint
                                        + flat.normalized * orbitRadius
                                        + Vector3.up * (orbitRadius * 0.6f);
                    steer += (ringPoint - b.t.position) * 1.5f;
                    steer += Vector3.Cross(Vector3.up, flat).normalized * maxSpeed * 1.2f;
                }
                else
                {
                    Vector3 toT = divePoint - b.t.position;
                    if (toT.magnitude < orbitRadius * 0.35f)
                    {
                        b.diving = false;
                        b.diveCooldown = Random.Range(6f, 14f);
                        steer += (Vector3.up + b.velocity.normalized) * maxSpeed * 2f;
                    }
                    else
                    {
                        steer += toT.normalized * maxSpeed * 2.5f;
                    }
                }
            }

            // ---------- wander
            float s = b.wanderSeed;
            Vector3 wander = new Vector3(
                Mathf.PerlinNoise(Time.time * 0.3f, s) - 0.5f,
                (Mathf.PerlinNoise(s, Time.time * 0.3f) - 0.5f) * 0.6f,
                Mathf.PerlinNoise(Time.time * 0.3f + s, s) - 0.5f);
            steer += wander * wanderWeight * maxSpeed * (drifting ? 1.5f : 1f);

            // ---------- twitch, sharp little sideways dodges
            if (twitch > 0f && !panic)
            {
                b.twitchTimer -= dt;
                if (b.twitchTimer <= 0f)
                {
                    b.twitchTimer = Random.Range(3f, 9f) * (1.2f - twitch);
                    Vector3 kick = b.t.right * Random.Range(-1f, 1f)
                                   + Vector3.up * Random.Range(-0.3f, 0.3f);
                    b.velocity += kick.normalized * maxSpeed * 0.45f * twitch;
                }
            }

            // ---------- soft bounds around the anchor
            Vector3 fromHome = b.t.position - center;
            float outside = fromHome.magnitude / Mathf.Max(boundsRadius, 0.01f);
            if (outside > 0.95f && !panic)
                steer += (-fromHome.normalized * maxSpeed - b.velocity)
                         * Mathf.InverseLerp(0.95f, 1.25f, outside) * 2.5f;

            // drifters stay on a short leash from the ball instead of
            // roaming the whole bounds. Fewer and closer strays.
            if (drifting)
            {
                Vector3 fromBlob = b.t.position - blobTarget;
                if (fromBlob.magnitude > blobRadiusLive * 2f)
                    steer += (-fromBlob.normalized * maxSpeed - b.velocity) * 2f;
            }

            // ---------- floor: never sink toward the ground
            float floorY = center.y - floorBelowAnchor;
            if (b.t.position.y < floorY)
                steer += Vector3.up * maxSpeed
                         * (2f + (floorY - b.t.position.y) * 0.5f);

            // ---------- obstacle avoidance
            if (obstacleMask.value != 0 && b.velocity.sqrMagnitude > 0.01f &&
                Physics.SphereCast(b.t.position, 0.4f, b.velocity.normalized,
                                   out RaycastHit hit, avoidDistance, obstacleMask))
            {
                float urgency = 1f - (hit.distance / avoidDistance);
                Vector3 away = Vector3.Reflect(b.velocity.normalized, hit.normal);
                steer += (away + hit.normal) * avoidWeight * maxSpeed * (0.5f + urgency);
            }

            // ---------- integrate
            Vector3 accel = Vector3.ClampMagnitude(steer, steerForce * (panic ? 1.6f : 1f));
            b.velocity += accel * dt;

            float boost = (panic ? panicSpeedBoost
                                 : (b.diving ? diveSpeedBoost : 1f)) * paceBoost;
            float speed = Mathf.Clamp(b.velocity.magnitude,
                                      minSpeed * b.personality,
                                      maxSpeed * b.personality * boost);
            b.velocity = b.velocity.normalized * speed;
            b.t.position += b.velocity * dt;

            // ---------- orientation and banking
            if (b.velocity.sqrMagnitude > 0.001f)
            {
                float lateral = Vector3.Dot(accel, b.t.right) / Mathf.Max(steerForce, 0.01f);
                b.bank = Mathf.Lerp(b.bank, -lateral * bankAmount, dt * 4f);

                Quaternion look = Quaternion.LookRotation(b.velocity.normalized, Vector3.up)
                                  * Quaternion.Euler(0f, 0f, b.bank);
                b.t.rotation = Quaternion.Slerp(b.t.rotation, look, dt * 6f);
            }

            // ---------- animation speed and glide switching
            float animSpeed = 1f;
            if (b.anim != null)
            {
                animSpeed = Mathf.Lerp(0.7f, animatorSyncScale,
                                       Mathf.InverseLerp(minSpeed, maxSpeed, speed))
                            * (panic ? 1.4f : 1f)
                            * b.flapTempo
                            // slow per-bird drift so tempos never lock together
                            * (1f + 0.12f * flapVariation
                               * Mathf.Sin(Time.time * 0.23f + b.wanderSeed));
                b.anim.speed = animSpeed;

                if (b.hasGlide)
                {
                    // bounding flight: mostly flapping, with occasional
                    // short glides. Glide Amount sets how often.
                    if (b.glidePulse > 0f)
                        b.glidePulse -= dt;
                    else
                    {
                        b.glideCooldown -= dt;
                        if (b.glideCooldown <= 0f)
                        {
                            b.glideCooldown = Mathf.Lerp(20f, 4f, glideAmount)
                                              * Random.Range(0.7f, 1.3f);
                            b.glidePulse = Random.Range(0.35f, 0.8f);
                        }
                    }

                    bool wantGlide = b.diving
                                     || (glideAmount > 0f && b.glidePulse > 0f)
                                     || b.velocity.y < -0.2f * speed;
                    if (panic) wantGlide = b.diving;   // panicked birds flap
                    if (wantGlide != b.gliding)
                    {
                        b.gliding = wantGlide;
                        b.anim.CrossFadeInFixedTime(
                            wantGlide ? b.glideHash : b.flyHash, 0.2f);
                    }
                }
            }

            // ---------- flap bob, tiny body bounce synced to the wingbeat
            if (flapBob > 0f && b.meshRoot != null)
            {
                b.bobPhase += dt * 6.28f * animSpeed;
                float amp = b.gliding ? flapBob * 0.2f : flapBob;
                Vector3 lp = b.meshRoot.localPosition;
                lp.y = b.meshBaseY + Mathf.Sin(b.bobPhase) * amp;
                b.meshRoot.localPosition = lp;
            }
        }

        // ------------------------------------------------------------------
        // Head stabilization runs after the Animator so it wins over
        // whatever the fly clip does to the neck.

        void LateUpdate()
        {
            if (headStabilize <= 0f) return;

            for (int i = 0; i < birds.Count; i++)
            {
                Bird b = birds[i];
                if (b.head == null || b.velocity.sqrMagnitude < 0.001f) continue;

                // flatten the look direction by the stabilize amount,
                // so the head stays forward and only tilts partly with climbs and dives
                Vector3 dir = b.velocity.normalized;
                dir.y *= (1f - headStabilize);
                if (dir.sqrMagnitude < 0.0001f) dir = b.t.forward;

                Quaternion stableLook = Quaternion.LookRotation(dir.normalized, b.t.up);
                b.head.rotation = stableLook * b.headOffset;
            }
        }

        void OnDrawGizmosSelected()
        {
            if (patrolPoints != null && patrolPoints.Count >= 2)
            {
                Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.8f);
                for (int i = 0; i < patrolPoints.Count; i++)
                {
                    Transform a = patrolPoints[i];
                    Transform b = patrolPoints[(i + 1) % patrolPoints.Count];
                    if (a != null) Gizmos.DrawWireSphere(a.position, 1f);
                    if (a != null && b != null) Gizmos.DrawLine(a.position, b.position);
                }
            }
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(Center, boundsRadius);
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.5f);
            Gizmos.DrawWireSphere(Application.isPlaying ? centroid : Center, BaseRadius);
            if (mode == FlockMode.DiveAttack)
            {
                Gizmos.color = new Color(1f, 0.5f, 0.3f, 0.6f);
                Gizmos.DrawWireSphere(DivePoint, orbitRadius);
            }
        }
    }
}
