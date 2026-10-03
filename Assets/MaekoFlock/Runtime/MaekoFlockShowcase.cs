using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MaekoStudio.Flock
{
    /// <summary>
    /// Bird showcase demo: the starling up close on a slow turntable in a
    /// white studio. Toggles between the two flight animations by playing
    /// the clips directly, so each preview is locked to its real motion.
    /// The camera auto-frames the bird wherever the turntable sits.
    /// Press H to hide the UI for clean recordings.
    /// </summary>
    [AddComponentMenu("Maeko Studio/Maeko Flock Showcase")]
    public class MaekoFlockShowcase : MonoBehaviour
    {
        [Tooltip("The bird's Animator. Found automatically if left empty.")]
        public Animator bird;
        [Tooltip("Root that spins on the turntable, usually the bird's parent.")]
        public Transform turntable;
        public Camera targetCamera;
        [Tooltip("Slide the TURNTABLE in front of the camera so the bird fills the frame. The camera itself never moves.")]
        public bool autoFrame = true;
        public float turnSpeed = 14f;
        [Tooltip("Seconds each animation plays before auto-switching.")]
        public float switchEvery = 7f;
        [Tooltip("Name shown above the animation info.")]
        public string birdLabel = "Starling";

        [Tooltip("Preferred: clips played directly, immune to controller mistakes. Assigned by the scene creator.")]
        public AnimationClip flyClip;
        public AnimationClip glideClip;

        [Tooltip("Fallback state names, used only when no clips are assigned.")]
        public string flyState = "Maeko_Fly";
        public string glideState = "Maeko_Glide";

        [TextArea] public string flyDescription =
            "Maeko_Fly · the flap cycle. Plays whenever a bird cruises, " +
            "climbs, or panics. In the flock, its speed is synced to each " +
            "bird's airspeed, so fast birds flap faster.";
        [TextArea] public string glideDescription =
            "Maeko_Glide · the wings-out soar. The flock cuts to it " +
            "automatically during dives, steady descents, and short " +
            "bounding-flight pulses, then blends back to the flap.";

        bool _glide;
        float _timer;
        bool _hideUI;
        Texture2D _dim;
        int _tris;
        int _verts;
        int _bones;

        PlayableGraph _graph;
        AnimationClipPlayable _playable;
        AnimationClip _current;
        bool _graphAlive;

        int _flyHash;
        int _glideHash;

        void Start()
        {
            if (bird == null) bird = GetComponentInChildren<Animator>();
            if (bird == null && turntable != null)
                bird = turntable.GetComponentInChildren<Animator>();
            if (targetCamera == null) targetCamera = Camera.main;
            _timer = switchEvery;
            _flyHash = Resolve(flyState);
            _glideHash = Resolve(glideState);
            CountStats();
            if (autoFrame) PositionBird();
            Play(flyClip);
        }

        void OnDestroy()
        {
            if (_graphAlive) _graph.Destroy();
            if (_dim != null) Destroy(_dim);
        }

        void CountStats()
        {
            Transform root = turntable != null ? turntable
                           : (bird != null ? bird.transform : transform);
            _tris = 0;
            _verts = 0;
            _bones = 0;
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.sharedMesh == null) continue;
                _tris += smr.sharedMesh.triangles.Length / 3;
                _verts += smr.sharedMesh.vertexCount;
                _bones += smr.bones.Length;
            }
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                _tris += mf.sharedMesh.triangles.Length / 3;
                _verts += mf.sharedMesh.vertexCount;
            }
        }

        void PositionBird()
        {
            // the camera stays exactly where it was placed; the turntable
            // slides so the bird sits centred in frame at a good distance
            if (targetCamera == null || turntable == null) return;

            var renderers = turntable.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            float radius = b.extents.magnitude;
            float fov = targetCamera.fieldOfView * Mathf.Deg2Rad;
            float distance = radius / Mathf.Tan(fov * 0.5f) * 0.85f + radius;

            Vector3 desiredCenter = targetCamera.transform.position
                                    + targetCamera.transform.forward * distance;
            turntable.position += desiredCenter - b.center;
        }

        int Resolve(string wanted)
        {
            if (bird == null || bird.runtimeAnimatorController == null)
                return Animator.StringToHash(wanted);
            int h = Animator.StringToHash(wanted);
            if (bird.HasState(0, h)) return h;
            foreach (var clip in bird.runtimeAnimatorController.animationClips)
            {
                if (clip == null || !clip.name.EndsWith(wanted)) continue;
                int h2 = Animator.StringToHash(clip.name);
                if (bird.HasState(0, h2)) return h2;
            }
            return h;
        }

        void Play(AnimationClip clip)
        {
            if (bird == null) return;

            if (clip == null)
            {
                // fallback: drive the controller by state name
                bird.CrossFadeInFixedTime(
                    _glide ? _glideHash : _flyHash, 0.25f);
                return;
            }

            if (_graphAlive) _graph.Destroy();
            _graph = PlayableGraph.Create("MaekoShowcase");
            var output = AnimationPlayableOutput.Create(_graph, "bird", bird);
            _playable = AnimationClipPlayable.Create(_graph, clip);
            _playable.SetApplyFootIK(false);
            output.SetSourcePlayable(_playable);
            _graph.Play();
            _graphAlive = true;
            _current = clip;
        }

        void Update()
        {
            if (turntable != null)
                turntable.Rotate(0f, turnSpeed * Time.deltaTime, 0f, Space.World);

            // loop the clip no matter how it was imported
            if (_graphAlive && _current != null
                && _playable.GetTime() >= _current.length)
                _playable.SetTime(0.0);

            _timer -= Time.deltaTime;
            if (_timer <= 0f) Toggle();
        }

        void Toggle()
        {
            _glide = !_glide;
            _timer = switchEvery;
            Play(_glide ? glideClip : flyClip);
        }

        void OnGUI()
        {
            Event e = Event.current;
            if (e != null && e.type == EventType.KeyDown && e.keyCode == KeyCode.H)
            {
                _hideUI = !_hideUI;
                e.Use();
            }
            if (_hideUI) return;
            if (_tris == 0) CountStats();

            if (_dim == null)
            {
                _dim = new Texture2D(1, 1);
                _dim.SetPixel(0, 0, new Color(0.06f, 0.06f, 0.09f, 0.82f));
                _dim.Apply();
            }

            const float w = 580f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 150f;

            GUI.DrawTexture(new Rect(x - 12f, y - 12f, w + 24f, 142f), _dim);

            var name = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            name.normal.textColor = Color.white;
            GUI.Label(new Rect(x, y, w, 26f), birdLabel, name);

            var meta = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            meta.normal.textColor = new Color(0.72f, 0.68f, 1f);
            GUI.Label(new Rect(x, y + 26f, w, 18f),
                _tris + " tris  ·  " + _verts + " verts  ·  " + _bones
                + " bones  ·  " + (_glide ? "Maeko_Glide" : "Maeko_Fly"),
                meta);

            var body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 12,
                wordWrap = true
            };
            body.normal.textColor = new Color(0.92f, 0.92f, 0.95f);
            GUI.Label(new Rect(x + 12f, y + 48f, w - 24f, 46f),
                _glide ? glideDescription : flyDescription, body);

            if (GUI.Button(new Rect(x + (w - 170f) * 0.5f, y + 96f, 170f, 26f),
                    "Switch Animation"))
                Toggle();
        }
    }
}
