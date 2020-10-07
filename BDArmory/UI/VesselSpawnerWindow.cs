﻿using BDArmory.Control;
using BDArmory.Core;
using KSP.Localization;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class VesselSpawnerWindow : MonoBehaviour
    {
        private class SpawnField : MonoBehaviour
        {
            public SpawnField Initialise(double l, double v, double minV = double.MinValue, double maxV = double.MaxValue) { lastUpdated = l; currentValue = v; minValue = minV; maxValue = maxV; return this; }
            public double lastUpdated;
            public string possibleValue = string.Empty;
            private double _value;
            public double currentValue { get { return _value; } set { _value = value; possibleValue = _value.ToString("G6"); } }
            private double minValue;
            private double maxValue;
            private bool coroutineRunning = false;
            private Coroutine coroutine;

            public void tryParseValue(string v)
            {
                if (v != possibleValue)
                {
                    lastUpdated = Time.time;
                    possibleValue = v;
                    if (!coroutineRunning)
                    {
                        coroutine = StartCoroutine(UpdateValueCoroutine());
                    }
                }
            }

            private IEnumerator UpdateValueCoroutine()
            {
                coroutineRunning = true;
                while (Time.time - lastUpdated < 0.5)
                    yield return new WaitForFixedUpdate();
                double newValue;
                if (double.TryParse(possibleValue, out newValue))
                {
                    currentValue = Math.Min(Math.Max(newValue, minValue), maxValue);
                    lastUpdated = Time.time;
                }
                possibleValue = currentValue.ToString("G6");
                coroutineRunning = false;
                yield return new WaitForFixedUpdate();
            }
        }

        #region Fields
        public static VesselSpawnerWindow Instance;
        private int _guiCheckIndex;
        private static readonly float _buttonSize = 20;
        private static readonly float _margin = 5;
        private static readonly float _lineHeight = _buttonSize;
        private float _windowHeight; //auto adjusting
        private float _windowWidth;
        public bool _ready = false;
        private bool _vesselsSpawned = false;
        public bool continuousVesselSpawning = false;
        Dictionary<string, SpawnField> spawnFields;
        #endregion

        #region Styles
        Rect SLineRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth - 2 * _margin, _lineHeight);
        }

        Rect SLeftRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth / 2 - _margin - _margin / 4, _lineHeight);
        }

        Rect SRightRect(float line)
        {
            return new Rect(_windowWidth / 2 + _margin / 4, line * _lineHeight, _windowWidth / 2 - _margin - _margin / 4, _lineHeight);
        }

        Rect SLeftSliderRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, _windowWidth / 2 + _margin / 2, _lineHeight);
        }

        Rect SRightSliderRect(float line)
        {
            return new Rect(_margin + _windowWidth / 2 + _margin / 2, line * _lineHeight, _windowWidth / 2 - 7 / 2 * _margin, _lineHeight);
        }

        Rect SLeftButtonRect(float line)
        {
            return new Rect(_margin, line * _lineHeight, (_windowWidth - 2 * _margin) / 2 - _margin / 4, _lineHeight);
        }

        Rect SRightButtonRect(float line)
        {
            return new Rect(_windowWidth / 2 + _margin / 4, line * _lineHeight, (_windowWidth - 2 * _margin) / 2 - _margin / 4, _lineHeight);
        }

        Rect SQuarterRect(float line, int pos)
        {
            return new Rect(_margin + (pos % 4) * (_windowWidth - 2f * _margin) / 4f, (line + (int)(pos / 4)) * _lineHeight, (_windowWidth - 2.5f * _margin) / 4f, _lineHeight);
        }

        List<Rect> SRight2Rects(float line)
        {
            var rectGap = _margin / 2;
            var rectWidth = ((_windowWidth - 2 * _margin) / 2 - 2 * rectGap) / 2;
            var rects = new List<Rect>();
            rects.Add(new Rect(_windowWidth / 2 + rectGap / 2, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(_windowWidth / 2 + rectWidth + rectGap * 3 / 2, line * _lineHeight, rectWidth, _lineHeight));
            return rects;
        }

        List<Rect> SRight3Rects(float line)
        {
            var rectGap = _margin / 3;
            var rectWidth = ((_windowWidth - 2 * _margin) / 2 - 3 * rectGap) / 3;
            var rects = new List<Rect>();
            rects.Add(new Rect(_windowWidth / 2 + rectGap / 2, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(_windowWidth / 2 + rectWidth + rectGap * 3 / 2, line * _lineHeight, rectWidth, _lineHeight));
            rects.Add(new Rect(_windowWidth / 2 + 2 * rectWidth + rectGap * 5 / 2, line * _lineHeight, rectWidth, _lineHeight));
            return rects;
        }

        GUIStyle leftLabel;
        #endregion

        private void Awake()
        {
            if (Instance)
                Destroy(this);
            Instance = this;
        }

        private void Start()
        {
            _ready = false;
            StartCoroutine(WaitForBdaSettings());

            leftLabel = new GUIStyle();
            leftLabel.alignment = TextAnchor.UpperLeft;
            leftLabel.normal.textColor = Color.white;

            // Spawn fields
            spawnFields = new Dictionary<string, SpawnField> {
                { "lat", gameObject.AddComponent<SpawnField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, -90, 90) },
                { "lon", gameObject.AddComponent<SpawnField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, -180, 180) },
                { "alt", gameObject.AddComponent<SpawnField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, 0) },
            };
        }

        private IEnumerator WaitForBdaSettings()
        {
            while (BDArmorySetup.Instance == null)
                yield return null;

            BDArmorySetup.Instance.hasVesselSpawner = true;
            _guiCheckIndex = Misc.Misc.RegisterGUIRect(new Rect());
            _ready = true;
        }

        private void Update()
        {
        }

        private void OnGUI()
        {
            if (!(_ready && BDArmorySetup.GAME_UI_ENABLED && BDArmorySetup.Instance.showVesselSpawnerGUI))
                return;

            _windowWidth = BDArmorySettings.VESSEL_SPAWNER_WINDOW_WIDTH;

            SetNewHeight(_windowHeight);
            BDArmorySetup.WindowRectVesselSpawner = new Rect(
                BDArmorySetup.WindowRectVesselSpawner.x,
                BDArmorySetup.WindowRectVesselSpawner.y,
                _windowWidth,
                _windowHeight
            );
            BDArmorySetup.WindowRectVesselSpawner = GUI.Window(
                GetInstanceID(), // InstanceID should be unique. FIXME All GUI.Windows should use the same method of generating unique IDs to avoid duplicates.
                BDArmorySetup.WindowRectVesselSpawner,
                WindowVesselSpawner,
                Localizer.Format("#LOC_BDArmory_BDAVesselSpawner_Title"),//"BDA Vessel Spawner"
                BDArmorySetup.BDGuiSkin.window
            );
            Misc.Misc.UpdateGUIRect(BDArmorySetup.WindowRectVesselSpawner, _guiCheckIndex);
        }

        private void SetNewHeight(float windowHeight)
        {
            var previousWindowHeight = BDArmorySetup.WindowRectVesselSpawner.height;
            BDArmorySetup.WindowRectVesselSpawner.height = windowHeight;

            if (BDArmorySettings.STRICT_WINDOW_BOUNDARIES && windowHeight < previousWindowHeight && BDArmorySetup.WindowRectVesselSpawner.y + previousWindowHeight == Screen.height) // Window shrunk while being at edge of screen.
                BDArmorySetup.WindowRectVesselSpawner.y = Screen.height - BDArmorySetup.WindowRectVesselSpawner.height;
            BDGUIUtils.RepositionWindow(ref BDArmorySetup.WindowRectVesselSpawner);
        }

        private void WindowVesselSpawner(int id)
        {
            GUI.DragWindow(new Rect(0, 0, _windowWidth - _buttonSize - _margin, _buttonSize + _margin));
            if (GUI.Button(new Rect(_windowWidth - _buttonSize - (_margin - 2), _margin, _buttonSize - 2, _buttonSize - 2), "X", BDArmorySetup.BDGuiSkin.button))
            {
                BDArmorySetup.Instance.showVesselSpawnerGUI = false;
            }

            float line = 0.25f;

            GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnDistanceFactor")}:  ({BDArmorySettings.VESSEL_SPAWN_DISTANCE})", leftLabel);//Spawn Distance

            //FIX ME BDArmorySettings.VESSEL_SPAWN_DISTANCE = Mathf.Round(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_DISTANCE / 10f, 1f, 10f) * 10f);
            BDArmorySettings.VESSEL_SPAWN_DISTANCE = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_DISTANCE, 1f, 20f);


            GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnEaseInSpeed")}:  ({BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED})", leftLabel);//Spawn Ease In Speed
            BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED = Mathf.Round(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, 0.1f, 1f) * 10f) / 10f;

            GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnConcurrentVessels")}:  ({(BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS > 0 ? BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS.ToString() : "Inf")})", leftLabel);//Max Concurrent Vessels (CS)
            BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_CONCURRENT_VESSELS, 0f, 20f);

            GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_SpawnLivesPerVessel")}:  ({(BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL > 0 ? BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL.ToString() : "Inf")})", leftLabel);//Respawns (CS)
            BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.VESSEL_SPAWN_LIVES_PER_VESSEL, 0f, 20f);

            var outOfAmmoKillTimeStr = "never";
            if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME > -1 && BDArmorySettings.OUT_OF_AMMO_KILL_TIME < 60)
                outOfAmmoKillTimeStr = BDArmorySettings.OUT_OF_AMMO_KILL_TIME.ToString("G0") + "s";
            else if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME > 59 && BDArmorySettings.OUT_OF_AMMO_KILL_TIME < 61)
                outOfAmmoKillTimeStr = "1min";
            else if (BDArmorySettings.OUT_OF_AMMO_KILL_TIME > 60)
                outOfAmmoKillTimeStr = ((int)(BDArmorySettings.OUT_OF_AMMO_KILL_TIME / 60f)).ToString("G0") + "mins";
            GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_OutOfAmmoKillTime")}: ({outOfAmmoKillTimeStr})", leftLabel); // Out of ammo kill timer for continuous spawning mode.
            float outOfAmmoKillTime;
            switch ((int)BDArmorySettings.OUT_OF_AMMO_KILL_TIME)
            {
                case 0:
                    outOfAmmoKillTime = 1f;
                    break;
                case 10:
                    outOfAmmoKillTime = 2f;
                    break;
                case 20:
                    outOfAmmoKillTime = 3f;
                    break;
                case 30:
                    outOfAmmoKillTime = 4f;
                    break;
                case 45:
                    outOfAmmoKillTime = 5f;
                    break;
                case 60:
                    outOfAmmoKillTime = 6f;
                    break;
                case 120:
                    outOfAmmoKillTime = 7f;
                    break;
                case 300:
                    outOfAmmoKillTime = 8f;
                    break;
                default:
                    outOfAmmoKillTime = 9f;
                    break;
            }
            outOfAmmoKillTime = GUI.HorizontalSlider(SRightSliderRect(line), outOfAmmoKillTime, 1f, 9f);
            switch ((int)outOfAmmoKillTime)
            {
                case 1:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 0f; // 0s
                    break;
                case 2:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 10f; // 10s
                    break;
                case 3:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 20f; // 20s
                    break;
                case 4:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 30f; // 30s
                    break;
                case 5:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 45f; // 45s
                    break;
                case 6:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 60f; // 1 min
                    break;
                case 7:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 120f;// 2 mins
                    break;
                case 8:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = 300f; // 5 mins
                    break;
                default:
                    BDArmorySettings.OUT_OF_AMMO_KILL_TIME = -1f; // Never
                    break;
            }

            BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING = GUI.Toggle(SLeftRect(++line), BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING, Localizer.Format("#LOC_BDArmory_Settings_SpawnContinueSingleSpawning"));  // Spawn craft again after single spawn competition finishes.
            BDArmorySettings.VESSEL_SPAWN_DUMP_LOG_EVERY_SPAWN = GUI.Toggle(SRightRect(line), BDArmorySettings.VESSEL_SPAWN_DUMP_LOG_EVERY_SPAWN, Localizer.Format("#LOC_BDArmory_Settings_SpawnDumpLogsEverySpawn")); //Dump logs every spawn.

            if (GUI.Button(SLeftButtonRect(++line), Localizer.Format("#LOC_BDArmory_Settings_VesselSpawnGeoCoords"), BDArmorySetup.BDGuiSkin.button)) //"Vessel Spawning Location"
            {
                Ray ray = new Ray(FlightCamera.fetch.mainCamera.transform.position, FlightCamera.fetch.mainCamera.transform.forward);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, 10000, 1 << 15))
                {
                    BDArmorySettings.VESSEL_SPAWN_GEOCOORDS = FlightGlobals.currentMainBody.GetLatitudeAndLongitude(hit.point);
                    spawnFields["lat"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x;
                    spawnFields["lon"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y;
                }
            }
            var rects = SRight3Rects(line);
            spawnFields["lat"].tryParseValue(GUI.TextField(rects[0], spawnFields["lat"].possibleValue, 8));
            spawnFields["lon"].tryParseValue(GUI.TextField(rects[1], spawnFields["lon"].possibleValue, 8));
            spawnFields["alt"].tryParseValue(GUI.TextField(rects[2], spawnFields["alt"].possibleValue, 8));
            BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x = Math.Min(Math.Max(spawnFields["lat"].currentValue, -90), 90);
            BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y = Math.Min(Math.Max(spawnFields["lon"].currentValue, -180), 180);
            BDArmorySettings.VESSEL_SPAWN_ALTITUDE = Math.Max(0, (float)spawnFields["alt"].currentValue);
            if (GUI.Button(SLineRect(++line), (BDArmorySettings.SHOW_SPAWN_LOCATIONS ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_SpawnLocations"), BDArmorySettings.SHOW_SPAWN_LOCATIONS ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))//Show/hide spawn locations
            {
                BDArmorySettings.SHOW_SPAWN_LOCATIONS = !BDArmorySettings.SHOW_SPAWN_LOCATIONS;
            }
            if (BDArmorySettings.SHOW_SPAWN_LOCATIONS)
            {
                line++;
                int i = 0;
                foreach (var spawnLocation in VesselSpawner.spawnLocations)
                {
                    if (GUI.Button(SQuarterRect(line, i++), spawnLocation.name, BDArmorySetup.BDGuiSkin.button))
                    {
                        BDArmorySettings.VESSEL_SPAWN_GEOCOORDS = spawnLocation.location;
                        spawnFields["lat"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x;
                        spawnFields["lon"].currentValue = BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y;
                        VesselSpawner.Instance.ShowSpawnPoint(BDArmorySettings.VESSEL_SPAWN_GEOCOORDS, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, 20);
                    }
                }
                line += (i - 1) / 4;
            }
            // TODO Add a button for adding in spawn locations in the GUI.

            ++line;
            if (GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_SingleSpawn"), _vesselsSpawned ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                if (!_vesselsSpawned && !continuousVesselSpawning && Event.current.button == 0) // Left click
                {
                    if (BDArmorySettings.VESSEL_SPAWN_CONTINUE_SINGLE_SPAWNING)
                        VesselSpawner.Instance.SpawnAllVesselsOnceContinuously(BDArmorySettings.VESSEL_SPAWN_GEOCOORDS, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, true); // Spawn vessels.
                    else
                        VesselSpawner.Instance.SpawnAllVesselsOnce(BDArmorySettings.VESSEL_SPAWN_GEOCOORDS, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, true); // Spawn vessels.
                    _vesselsSpawned = true;
                }
                else if (Event.current.button == 2) // Middle click, add a new spawn of vessels to the currently spawned vessels.
                {
                    VesselSpawner.Instance.SpawnAllVesselsOnce(BDArmorySettings.VESSEL_SPAWN_GEOCOORDS, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE, BDArmorySettings.VESSEL_SPAWN_EASE_IN_SPEED, false); // Spawn vessels, without killing off other vessels or changing camera positions.
                }
            }
            if (GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_ContinuousSpawning"), continuousVesselSpawning ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button))
            {
                if (!continuousVesselSpawning && !_vesselsSpawned && Event.current.button == 0) // Left click
                {
                    VesselSpawner.Instance.SpawnVesselsContinuously(BDArmorySettings.VESSEL_SPAWN_GEOCOORDS, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, BDArmorySettings.VESSEL_SPAWN_DISTANCE, true); // Spawn vessels continuously at 1km above terrain.
                    continuousVesselSpawning = true;
                }
            }
            if (GUI.Button(SLineRect(++line), Localizer.Format("Runway Project Season 2 Round 3"), _vesselsSpawned ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button)) // FIXME For round 3 only.
            {
                if (!_vesselsSpawned && !continuousVesselSpawning && Event.current.button == 0) // Left click
                {
                    Debug.Log("[VesselSpawner]: Spawning 'Round 3' configuration.");
                    _vesselsSpawned = true;
                    VesselSpawner.Instance.TeamSpawn(
                        new List<VesselSpawner.SpawnConfig> {
                            new VesselSpawner.SpawnConfig(BDArmorySettings.VESSEL_SPAWN_GEOCOORDS, 5, BDArmorySettings.VESSEL_SPAWN_DISTANCE, ""),
                            new VesselSpawner.SpawnConfig(BDArmorySettings.VESSEL_SPAWN_GEOCOORDS, 5000, 300, "Targets")
                        },
                        true, // Start the competition.
                        15d, // Wait 15s for the target planes to get going first.
                        true // Enable startCompetitionNow so the competition starts as soon as the missiles have launched.
                    ); // FIXME, this is temporary
                }
            }
            if (GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_CancelSpawning"), (_vesselsSpawned || continuousVesselSpawning) ? BDArmorySetup.BDGuiSkin.button : BDArmorySetup.BDGuiSkin.box))
            {
                VesselSpawner.Instance.CancelVesselSpawn();
                if (_vesselsSpawned)
                    Debug.Log("[BDArmory]: Resetting spawning vessel button.");
                _vesselsSpawned = false;
                if (continuousVesselSpawning)
                    Debug.Log("[BDArmory]: Resetting continuous spawning button.");
                continuousVesselSpawning = false;
            }

            line += 1.25f; // Bottom internal margin
            _windowHeight = (line * _lineHeight);
        }
    }
}
