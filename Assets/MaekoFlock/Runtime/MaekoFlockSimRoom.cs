using System.Collections.Generic;
using UnityEngine;

namespace MaekoStudio.Flock
{
    /// <summary>
    /// Simulation room demo: a locked camera on a full flock. Arrow buttons
    /// browse setups (figure eight, racetrack, hover, liquid stretch,
    /// drifters, panic, dive attack, patrol, migration). Page 1 is Full
    /// Auto: it plays the whole tour by itself like a highlight reel.
    /// Press H to hide the UI for clean recordings.
    /// </summary>
    [AddComponentMenu("Maeko Studio/Maeko Flock Sim Room")]
    public class MaekoFlockSimRoom : MonoBehaviour
    {
        public MaekoFlock flock;
        public Camera targetCamera;
        [Tooltip("The one camera position, shared by all setups.")]
        public Vector3 viewPosition = new Vector3(0f, 10f, -46f);
        [Tooltip("The one camera rotation, shared by all setups.")]
        public Vector3 viewEuler = new Vector3(-6f, 0f, 0f);

        [Tooltip("Waypoints used by the Patrol setup only.")]
        public List<Transform> patrolPoints = new List<Transform>();
        [Tooltip("Target used by the Dive Attack setup only.")]
        public Transform diveTarget;
        [Tooltip("Seconds per setup while Full Auto is running.")]
        public float autoSeconds = 10f;

        // page 0 is Full Auto; pages 1..N are the real setups
        static readonly string[] Titles =
        {
            "Full Auto · Highlight Reel",
            "Calm Swirl · Figure Eight",
            "Calm Swirl · Racetrack",
            "Hover · Tight On The Anchor",
            "Liquid Stretch · Fast Travel",
            "Drifters & Twitch",
            "Panic · Scatter And Recover",
            "Dive Attack · Orbit And Strike",
            "Patrol · Fly The Route",
            "Migration · Spawn & Despawn",
        };

        static readonly string[] Descriptions =
        {
            "Sit back. The room tours every setup on its own, a few seconds each. Use the arrows any time to take over.",
            "The default murmuration. The ball traces a lazy figure eight around its anchor, breathing, swaying, and twitching as it goes.",
            "The same calm flock on an oval circuit. Swirl Pattern, Radius, and Speed shape the path.",
            "Hover mode parks the ball directly on the anchor, tighter and denser. Perfect for a landmark or a story beat.",
            "Speed stretches the ball along its direction of travel like liquid, then it rounds back up when it slows. The Stretch slider.",
            "Lone scouts peel off ahead, beside, and above the flock, then sprint home. Drifter Chance and Twitch turned up.",
            "TriggerPanic() scatters the flock climbing hard, then it settles back into the swirl on its own. Fires from any script.",
            "The flock collapses into a tight orbit over a target while birds take turns dive-bombing it and pulling up.",
            "Two or more waypoints turn the flock into a route flier. It loops the course forever, or leaves after N loops.",
            "Flocks stream in from the horizon and depart the same way. Spawn on a trigger zone, a timer, a script, or the end of a route.",
        };

        int _index;
        int _autoIndex = 1;
        float _autoTimer;
        float _panicTimer;
        float _migrateTimer;
        bool _hideUI;

        void Start()
        {
            if (flock == null) flock = Object.FindAnyObjectByType<MaekoFlock>();
            _autoTimer = autoSeconds;
            Apply(0);
        }

        void Update()
        {
            if (flock == null) return;

            int active = _index == 0 ? _autoIndex : _index;

            // full auto advances itself
            if (_index == 0)
            {
                _autoTimer -= Time.deltaTime;
                if (_autoTimer <= 0f)
                {
                    _autoTimer = autoSeconds;
                    _autoIndex = 1 + (_autoIndex % (Titles.Length - 1));
                    ApplySetup(_autoIndex);
                }
            }

            // panic setup re-fires so the recover is always on show
            if (active == 6)
            {
                _panicTimer -= Time.deltaTime;
                if (_panicTimer <= 0f)
                {
                    _panicTimer = 8f;
                    flock.TriggerPanic();
                }
            }

            // migration setup cycles leave and return
            if (active == 9)
            {
                _migrateTimer -= Time.deltaTime;
                if (flock.BirdCount == 0 && _migrateTimer <= 0f)
                {
                    flock.SpawnFlock();
                    _migrateTimer = 14f;
                }
                else if (flock.BirdCount > 0 && _migrateTimer <= 0f)
                {
                    flock.DespawnFlock();
                    _migrateTimer = 2f;
                }
            }
        }

        void LateUpdate()
        {
            if (targetCamera != null)
                targetCamera.transform.SetPositionAndRotation(
                    viewPosition, Quaternion.Euler(viewEuler));
        }

        public void Apply(int index)
        {
            _index = (index + Titles.Length) % Titles.Length;
            if (_index == 0)
            {
                _autoIndex = 1;
                _autoTimer = autoSeconds;
                ApplySetup(1);
            }
            else
            {
                ApplySetup(_index);
            }
        }

        void ApplySetup(int i)
        {
            if (flock == null) return;

            // baseline every time, so setups never bleed into each other
            flock.SetMode(MaekoFlock.FlockMode.CalmSwirl);
            flock.swirlPattern = MaekoFlock.SwirlPattern.FigureEight;
            flock.swirlRadius = 9f;
            flock.swirlSpeed = 0.25f;
            flock.stretch = 0.5f;
            flock.breathe = 0.35f;
            flock.sway = 0.4f;
            flock.twitch = 0.4f;
            flock.drifterChance = 0.02f;
            flock.diveTarget = null;
            flock.patrolPoints.Clear();
            _panicTimer = 0f;
            _migrateTimer = 0f;
            if (flock.BirdCount == 0 && i != 9) flock.SpawnFlock();

            switch (i)
            {
                case 2:
                    flock.swirlPattern = MaekoFlock.SwirlPattern.Racetrack;
                    flock.swirlSpeed = 0.35f;
                    break;
                case 3:
                    flock.SetMode(MaekoFlock.FlockMode.Hover);
                    break;
                case 4:
                    flock.swirlPattern = MaekoFlock.SwirlPattern.Racetrack;
                    flock.swirlRadius = 16f;
                    flock.swirlSpeed = 0.6f;
                    flock.stretch = 1f;
                    break;
                case 5:
                    flock.drifterChance = 0.09f;
                    flock.twitch = 0.9f;
                    break;
                case 6:
                    _panicTimer = 0.5f;
                    break;
                case 7:
                    flock.SetMode(MaekoFlock.FlockMode.DiveAttack);
                    flock.diveTarget = diveTarget;
                    break;
                case 8:
                    if (patrolPoints != null)
                        flock.patrolPoints.AddRange(patrolPoints);
                    break;
                case 9:
                    _migrateTimer = 0.5f;
                    break;
            }
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

            const float w = 620f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 104f;

            GUI.Box(new Rect(x - 10f, y - 10f, w + 20f, 96f), GUIContent.none);

            if (GUI.Button(new Rect(x, y, 60f, 40f), "<"))
                Apply(_index - 1);
            if (GUI.Button(new Rect(x + w - 60f, y, 60f, 40f), ">"))
                Apply(_index + 1);

            string title = Titles[_index];
            if (_index == 0)
                title = "Full Auto · now showing: " + Titles[_autoIndex];

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            GUI.Label(new Rect(x + 70f, y, w - 140f, 24f),
                (_index + 1) + " / " + Titles.Length + "   " + title, titleStyle);

            var body = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 12,
                wordWrap = true
            };
            string desc = _index == 0 && _autoIndex < Descriptions.Length
                ? Descriptions[_autoIndex]
                : Descriptions[_index];
            GUI.Label(new Rect(x + 70f, y + 26f, w - 140f, 56f), desc, body);
        }
    }
}
