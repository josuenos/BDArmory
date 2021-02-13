using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using BDArmory.Bullets;
using BDArmory.Competition;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.CounterMeasure;
using BDArmory.FX;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.Parts;
using BDArmory.Radar;
using UnityEngine;
using KSP.Localization;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class BDArmorySetup : MonoBehaviour
    {
        public static bool SMART_GUARDS = true;
        public static bool showTargets = true;

        //=======Window position settings Git Issue #13
        [BDAWindowSettingsField] public static Rect WindowRectToolbar;
        [BDAWindowSettingsField] public static Rect WindowRectGps;
        [BDAWindowSettingsField] public static Rect WindowRectSettings;
        [BDAWindowSettingsField] public static Rect WindowRectRadar;
        [BDAWindowSettingsField] public static Rect WindowRectRwr;
        [BDAWindowSettingsField] public static Rect WindowRectVesselSwitcher;
        [BDAWindowSettingsField] public static Rect WindowRectWingCommander = new Rect(45, 75, 240, 800);
        [BDAWindowSettingsField] public static Rect WindowRectTargetingCam;

        [BDAWindowSettingsField] public static Rect WindowRectRemoteOrchestration;// = new Rect(45, 100, 200, 200);
        [BDAWindowSettingsField] public static Rect WindowRectVesselSpawner;

        //reflection field lists
        static FieldInfo[] iFs;

        static FieldInfo[] inputFields
        {
            get
            {
                if (iFs == null)
                {
                    iFs = typeof(BDInputSettingsFields).GetFields();
                }
                return iFs;
            }
        }

        //dependency checks
        bool ModuleManagerLoaded = false;
        bool PhysicsRangeExtenderLoaded = false;

        //EVENTS
        public delegate void VolumeChange();

        public static event VolumeChange OnVolumeChange;

        public delegate void SavedSettings();

        public static event SavedSettings OnSavedSettings;

        public delegate void PeaceEnabled();

        public static event PeaceEnabled OnPeaceEnabled;

        //particle optimization
        public static int numberOfParticleEmitters = 0;
        public static BDArmorySetup Instance;
        public static bool GAME_UI_ENABLED = true;
        public string Version { get; private set; } = "Unknown";

        //settings gui
        public static bool windowSettingsEnabled;
        public string fireKeyGui;

        //editor alignment
        public static bool showWeaponAlignment;

        // Gui Skin
        public static GUISkin BDGuiSkin = HighLogic.Skin;

        //toolbar gui
        public static bool hasAddedButton = false;
        public static bool windowBDAToolBarEnabled;
        float toolWindowWidth = 300;
        float toolWindowHeight = 100;
        bool showWeaponList;
        bool showGuardMenu;
        bool showModules;
        int numberOfModules;
        bool showWindowGPS;

        //gps window
        public bool showingWindowGPS
        {
            get { return showWindowGPS; }
        }

        bool maySavethisInstance = false;
        float gpsEntryCount;
        float gpsEntryHeight = 24;
        float gpsBorder = 5;
        bool editingGPSName;
        int editingGPSNameIndex;
        bool hasEnteredGPSName;
        string newGPSName = String.Empty;

        public MissileFire ActiveWeaponManager;
        public bool missileWarning;
        public float missileWarningTime = 0;

        //load range stuff
        VesselRanges combatVesselRanges = new VesselRanges();
        float physRangeTimer;

        public static List<CMFlare> Flares = new List<CMFlare>();

        //gui styles
        GUIStyle centerLabel;
        GUIStyle centerLabelRed;
        GUIStyle centerLabelOrange;
        GUIStyle centerLabelBlue;
        GUIStyle leftLabel;
        GUIStyle leftLabelRed;
        GUIStyle rightLabelRed;
        GUIStyle leftLabelGray;
        GUIStyle rippleSliderStyle;
        GUIStyle rippleThumbStyle;
        GUIStyle kspTitleLabel;
        GUIStyle middleLeftLabel;
        GUIStyle middleLeftLabelOrange;
        GUIStyle targetModeStyle;
        GUIStyle targetModeStyleSelected;
        GUIStyle waterMarkStyle;
        GUIStyle redErrorStyle;
        GUIStyle redErrorShadowStyle;

        public SortedList<string, BDTeam> Teams = new SortedList<string, BDTeam>
        {
            { "Neutral", new BDTeam("Neutral", neutral: true) }
        };



        //competition mode
        string compDistGui = "1000";

        #region Textures

        public static string textureDir = "BDArmory/Textures/";

        bool drawCursor;
        Texture2D cursorTexture = GameDatabase.Instance.GetTexture(textureDir + "aimer", false);

        private Texture2D dti;

        public Texture2D directionTriangleIcon
        {
            get { return dti ? dti : dti = GameDatabase.Instance.GetTexture(textureDir + "directionIcon", false); }
        }

        private Texture2D cgs;

        public Texture2D crossedGreenSquare
        {
            get { return cgs ? cgs : cgs = GameDatabase.Instance.GetTexture(textureDir + "crossedGreenSquare", false); }
        }

        private Texture2D dlgs;

        public Texture2D dottedLargeGreenCircle
        {
            get
            {
                return dlgs
                    ? dlgs
                    : dlgs = GameDatabase.Instance.GetTexture(textureDir + "dottedLargeGreenCircle", false);
            }
        }

        private Texture2D ogs;

        public Texture2D openGreenSquare
        {
            get { return ogs ? ogs : ogs = GameDatabase.Instance.GetTexture(textureDir + "openGreenSquare", false); }
        }

        private Texture2D gdott;

        public Texture2D greenDotTexture
        {
            get { return gdott ? gdott : gdott = GameDatabase.Instance.GetTexture(textureDir + "greenDot", false); }
        }

        private Texture2D gdt;

        public Texture2D greenDiamondTexture
        {
            get { return gdt ? gdt : gdt = GameDatabase.Instance.GetTexture(textureDir + "greenDiamond", false); }
        }

        private Texture2D lgct;

        public Texture2D largeGreenCircleTexture
        {
            get { return lgct ? lgct : lgct = GameDatabase.Instance.GetTexture(textureDir + "greenCircle3", false); }
        }

        private Texture2D gct;

        public Texture2D greenCircleTexture
        {
            get { return gct ? gct : gct = GameDatabase.Instance.GetTexture(textureDir + "greenCircle2", false); }
        }

        private Texture2D gpct;

        public Texture2D greenPointCircleTexture
        {
            get
            {
                if (gpct == null)
                {
                    gpct = GameDatabase.Instance.GetTexture(textureDir + "greenPointCircle", false);
                }
                return gpct;
            }
        }

        private Texture2D gspct;

        public Texture2D greenSpikedPointCircleTexture
        {
            get
            {
                return gspct ? gspct : gspct = GameDatabase.Instance.GetTexture(textureDir + "greenSpikedCircle", false);
            }
        }

        private Texture2D wSqr;

        public Texture2D whiteSquareTexture
        {
            get { return wSqr ? wSqr : wSqr = GameDatabase.Instance.GetTexture(textureDir + "whiteSquare", false); }
        }

        private Texture2D oWSqr;

        public Texture2D openWhiteSquareTexture
        {
            get
            {
                return oWSqr ? oWSqr : oWSqr = GameDatabase.Instance.GetTexture(textureDir + "openWhiteSquare", false);
                ;
            }
        }

        private Texture2D tDir;

        public Texture2D targetDirectionTexture
        {
            get
            {
                return tDir
                    ? tDir
                    : tDir = GameDatabase.Instance.GetTexture(textureDir + "targetDirectionIndicator", false);
            }
        }

        private Texture2D hInd;

        public Texture2D horizonIndicatorTexture
        {
            get
            {
                return hInd ? hInd : hInd = GameDatabase.Instance.GetTexture(textureDir + "horizonIndicator", false);
            }
        }

        private Texture2D si;

        public Texture2D settingsIconTexture
        {
            get { return si ? si : si = GameDatabase.Instance.GetTexture(textureDir + "settingsIcon", false); }
        }

        #endregion Textures

        public static bool GameIsPaused
        {
            get { return PauseMenu.isOpen || Time.timeScale == 0; }
        }

        void Awake()
        {
            Instance = this;

            // Create settings file if not present.
            if (ConfigNode.Load(BDArmorySettings.settingsConfigURL) == null)
            {
                var node = new ConfigNode();
                node.AddNode("BDASettings");
                node.Save(BDArmorySettings.settingsConfigURL);
            }

            // window position settings
            WindowRectToolbar = new Rect(Screen.width - toolWindowWidth - 40, 150, toolWindowWidth, toolWindowHeight);
            // Default, if not in file.
            WindowRectGps = new Rect(0, 0, WindowRectToolbar.width - 10, 0);
            SetupSettingsSize();
            BDAWindowSettingsField.Load();
            CheckIfWindowsSettingsAreWithinScreen();

            WindowRectGps.width = WindowRectToolbar.width - 10;

            // Load settings
            LoadConfig();
        }

        void Start()
        {
            //wmgr toolbar
            if (HighLogic.LoadedSceneIsFlight)
                maySavethisInstance = true;     //otherwise later we should NOT save the current window positions!

            // // Create settings file if not present.
            // if (ConfigNode.Load(BDArmorySettings.settingsConfigURL) == null)
            // {
            //     var node = new ConfigNode();
            //     node.AddNode("BDASettings");
            //     node.Save(BDArmorySettings.settingsConfigURL);
            // }

            // // window position settings
            // WindowRectToolbar = new Rect(Screen.width - toolWindowWidth - 40, 150, toolWindowWidth, toolWindowHeight);
            // // Default, if not in file.
            // WindowRectGps = new Rect(0, 0, WindowRectToolbar.width - 10, 0);
            // SetupSettingsSize();
            // BDAWindowSettingsField.Load();
            // CheckIfWindowsSettingsAreWithinScreen();

            // WindowRectGps.width = WindowRectToolbar.width - 10;

            // //settings
            // LoadConfig();

            physRangeTimer = Time.time;
            GAME_UI_ENABLED = true;
            fireKeyGui = BDInputSettingsFields.WEAP_FIRE_KEY.inputString;

            //setup gui styles
            centerLabel = new GUIStyle();
            centerLabel.alignment = TextAnchor.UpperCenter;
            centerLabel.normal.textColor = Color.white;

            centerLabelRed = new GUIStyle();
            centerLabelRed.alignment = TextAnchor.UpperCenter;
            centerLabelRed.normal.textColor = Color.red;

            centerLabelOrange = new GUIStyle();
            centerLabelOrange.alignment = TextAnchor.UpperCenter;
            centerLabelOrange.normal.textColor = XKCDColors.BloodOrange;

            centerLabelBlue = new GUIStyle();
            centerLabelBlue.alignment = TextAnchor.UpperCenter;
            centerLabelBlue.normal.textColor = XKCDColors.AquaBlue;

            leftLabel = new GUIStyle();
            leftLabel.alignment = TextAnchor.UpperLeft;
            leftLabel.normal.textColor = Color.white;

            middleLeftLabel = new GUIStyle(leftLabel);
            middleLeftLabel.alignment = TextAnchor.MiddleLeft;

            middleLeftLabelOrange = new GUIStyle(middleLeftLabel);
            middleLeftLabelOrange.normal.textColor = XKCDColors.BloodOrange;

            targetModeStyle = new GUIStyle();
            targetModeStyle.alignment = TextAnchor.MiddleRight;
            targetModeStyle.fontSize = 9;
            targetModeStyle.normal.textColor = Color.white;

            targetModeStyleSelected = new GUIStyle(targetModeStyle);
            targetModeStyleSelected.normal.textColor = XKCDColors.BloodOrange;

            waterMarkStyle = new GUIStyle(middleLeftLabel);
            waterMarkStyle.normal.textColor = XKCDColors.LightBlueGrey;

            leftLabelRed = new GUIStyle();
            leftLabelRed.alignment = TextAnchor.UpperLeft;
            leftLabelRed.normal.textColor = Color.red;

            rightLabelRed = new GUIStyle();
            rightLabelRed.alignment = TextAnchor.UpperRight;
            rightLabelRed.normal.textColor = Color.red;

            leftLabelGray = new GUIStyle();
            leftLabelGray.alignment = TextAnchor.UpperLeft;
            leftLabelGray.normal.textColor = Color.gray;

            rippleSliderStyle = new GUIStyle(BDGuiSkin.horizontalSlider);
            rippleThumbStyle = new GUIStyle(BDGuiSkin.horizontalSliderThumb);
            rippleSliderStyle.fixedHeight = rippleThumbStyle.fixedHeight = 0;

            kspTitleLabel = new GUIStyle();
            kspTitleLabel.normal.textColor = BDGuiSkin.window.normal.textColor;
            kspTitleLabel.font = BDGuiSkin.window.font;
            kspTitleLabel.fontSize = BDGuiSkin.window.fontSize;
            kspTitleLabel.fontStyle = BDGuiSkin.window.fontStyle;
            kspTitleLabel.alignment = TextAnchor.UpperCenter;

            redErrorStyle = new GUIStyle(BDGuiSkin.label);
            redErrorStyle.normal.textColor = Color.red;
            redErrorStyle.fontStyle = FontStyle.Bold;
            redErrorStyle.fontSize = 22;
            redErrorStyle.alignment = TextAnchor.UpperCenter;

            redErrorShadowStyle = new GUIStyle(redErrorStyle);
            redErrorShadowStyle.normal.textColor = new Color(0, 0, 0, 0.75f);
            //

            using (var a = AppDomain.CurrentDomain.GetAssemblies().ToList().GetEnumerator())
                while (a.MoveNext())
                {
                    string name = a.Current.FullName.Split(new char[1] { ',' })[0];
                    switch (name)
                    {
                        case "ModuleManager":
                            ModuleManagerLoaded = true;
                            break;

                        case "PhysicsRangeExtender":
                            PhysicsRangeExtenderLoaded = true;
                            break;

                        case "BDArmory":
                            Version = a.Current.GetName().Version.ToString();
                            break;
                    }
                }

            if (HighLogic.LoadedSceneIsFlight)
            {
                SaveVolumeSettings();

                GameEvents.onHideUI.Add(HideGameUI);
                GameEvents.onShowUI.Add(ShowGameUI);
                GameEvents.onVesselGoOffRails.Add(OnVesselGoOffRails);
                GameEvents.OnGameSettingsApplied.Add(SaveVolumeSettings);

                GameEvents.onVesselChange.Add(VesselChange);
            }

            BulletInfo.Load();
            RocketInfo.Load();

            // Spawn fields
            spawnFields = new Dictionary<string, SpawnField> {
                { "lat", gameObject.AddComponent<SpawnField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.x, -90, 90) },
                { "lon", gameObject.AddComponent<SpawnField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_GEOCOORDS.y, -180, 180) },
                { "alt", gameObject.AddComponent<SpawnField>().Initialise(0, BDArmorySettings.VESSEL_SPAWN_ALTITUDE, 0) },
            };
            compDistGui = BDArmorySettings.COMPETITION_DISTANCE.ToString();
        }

        private void CheckIfWindowsSettingsAreWithinScreen()
        {
            BDGUIUtils.UseMouseEventInRect(WindowRectSettings);
            BDGUIUtils.RepositionWindow(ref WindowRectToolbar);
            BDGUIUtils.RepositionWindow(ref WindowRectSettings);
            BDGUIUtils.RepositionWindow(ref WindowRectRwr);
            BDGUIUtils.RepositionWindow(ref WindowRectVesselSwitcher);
            BDGUIUtils.RepositionWindow(ref WindowRectWingCommander);
            BDGUIUtils.RepositionWindow(ref WindowRectTargetingCam);
        }

        void Update()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                if (missileWarning && Time.time - missileWarningTime > 1.5f)
                {
                    missileWarning = false;
                }

                if (Input.GetKeyDown(KeyCode.KeypadMultiply))
                {
                    windowBDAToolBarEnabled = !windowBDAToolBarEnabled;
                }
            }
            else if (HighLogic.LoadedSceneIsEditor)
            {
                if (Input.GetKeyDown(KeyCode.F2))
                {
                    showWeaponAlignment = !showWeaponAlignment;
                }
            }

            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                if (Input.GetKeyDown(KeyCode.B))
                {
                    ToggleWindowSettings();
                }
            }
        }

        void ToggleWindowSettings()
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING || HighLogic.LoadedScene == GameScenes.LOADINGBUFFER)
            {
                return;
            }

            windowSettingsEnabled = !windowSettingsEnabled;
            if (windowSettingsEnabled)
            {
                // LoadConfig(); // Don't reload settings, since they're already loaded and mess with other settings windows.
            }
            else
            {
                SaveConfig();
            }
        }

        void LateUpdate()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                //UpdateCursorState();
            }
        }

        public void UpdateCursorState()
        {
            if (ActiveWeaponManager == null)
            {
                drawCursor = false;
                //Screen.showCursor = true;
                Cursor.visible = true;
                return;
            }

            if (!GAME_UI_ENABLED || CameraMouseLook.MouseLocked)
            {
                drawCursor = false;
                Cursor.visible = false;
                return;
            }

            drawCursor = false;
            if (!MapView.MapIsEnabled && !Misc.Misc.CheckMouseIsOnGui() && !PauseMenu.isOpen)
            {
                if (ActiveWeaponManager.selectedWeapon != null && ActiveWeaponManager.weaponIndex > 0 &&
                    !ActiveWeaponManager.guardMode)
                {
                    if (ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Gun ||
                        ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket ||
                        ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.DefenseLaser)
                    {
                        ModuleWeapon mw =
                            ActiveWeaponManager.selectedWeapon.GetPart().FindModuleImplementing<ModuleWeapon>();
                        if (mw.weaponState == ModuleWeapon.WeaponStates.Enabled && mw.maxPitch > 1 && !mw.slaved &&
                            !mw.aiControlled)
                        {
                            //Screen.showCursor = false;
                            Cursor.visible = false;
                            drawCursor = true;
                            return;
                        }
                    }
                }
            }

            //Screen.showCursor = true;
            Cursor.visible = true;
        }

        void VesselChange(Vessel v)
        {
            if (v.isActiveVessel)
            {
                GetWeaponManager();
                Instance.UpdateCursorState();
            }
        }

        void GetWeaponManager()
        {
            using (List<MissileFire>.Enumerator mf = FlightGlobals.ActiveVessel.FindPartModulesImplementing<MissileFire>().GetEnumerator())
                while (mf.MoveNext())
                {
                    if (mf.Current == null) continue;
                    ActiveWeaponManager = mf.Current;
                    return;
                }
            ActiveWeaponManager = null;
            return;
        }

        public static void LoadConfig()
        {
            try
            {
                Debug.Log("[BDArmory]=== Loading settings.cfg ===");

                BDAPersistantSettingsField.Load();
                BDInputSettingsFields.LoadSettings();
            }
            catch (NullReferenceException)
            {
                Debug.Log("[BDArmory]=== Failed to load settings config ===");
            }
        }

        public static void SaveConfig()
        {
            try
            {
                Debug.Log("[BDArmory] == Saving settings.cfg ==	");

                BDAPersistantSettingsField.Save();

                BDInputSettingsFields.SaveSettings();

                if (OnSavedSettings != null)
                {
                    OnSavedSettings();
                }
            }
            catch (NullReferenceException)
            {
                Debug.Log("[BDArmory]: === Failed to save settings.cfg ====");
            }
        }

        #region GUI

        void OnGUI()
        {
            if (!GAME_UI_ENABLED) return;
            if (windowSettingsEnabled)
            {
                WindowRectSettings = GUI.Window(129419, WindowRectSettings, WindowSettings, GUIContent.none);
            }

            if (drawCursor)
            {
                //mouse cursor
                int origDepth = GUI.depth;
                GUI.depth = -100;
                float cursorSize = 40;
                Vector3 cursorPos = Input.mousePosition;
                Rect cursorRect = new Rect(cursorPos.x - (cursorSize / 2), Screen.height - cursorPos.y - (cursorSize / 2), cursorSize, cursorSize);
                GUI.DrawTexture(cursorRect, cursorTexture);
                GUI.depth = origDepth;
            }

            if (!windowBDAToolBarEnabled || !HighLogic.LoadedSceneIsFlight) return;
            WindowRectToolbar = GUI.Window(321, WindowRectToolbar, WindowBDAToolbar, Localizer.Format("#LOC_BDArmory_WMWindow_title") + "          ", BDGuiSkin.window);//"BDA Weapon Manager"
            BDGUIUtils.UseMouseEventInRect(WindowRectToolbar);
            if (showWindowGPS && ActiveWeaponManager)
            {
                //gpsWindowRect = GUI.Window(424333, gpsWindowRect, GPSWindow, "", GUI.skin.box);
                BDGUIUtils.UseMouseEventInRect(WindowRectGps);
                List<GPSTargetInfo>.Enumerator coord =
                  BDATargetManager.GPSTargetList(ActiveWeaponManager.Team).GetEnumerator();
                while (coord.MoveNext())
                {
                    BDGUIUtils.DrawTextureOnWorldPos(coord.Current.worldPos, Instance.greenDotTexture, new Vector2(8, 8), 0);
                }
                coord.Dispose();
            }

            // big error messages for missing dependencies
            if (ModuleManagerLoaded && PhysicsRangeExtenderLoaded) return;
            string message = (ModuleManagerLoaded ? "Physics Range Extender" : "Module Manager") + " is missing. BDA will not work properly.";
            GUI.Label(new Rect(0 + 2, Screen.height / 6 + 2, Screen.width, 100),
              message, redErrorShadowStyle);
            GUI.Label(new Rect(0, Screen.height / 6, Screen.width, 100),
              message, redErrorStyle);
        }

        public bool hasVesselSwitcher = false;
        public bool hasVesselSpawner = false;
        public bool showVesselSwitcherGUI = false;
        public bool showVesselSpawnerGUI = false;

        float rippleHeight;
        float weaponsHeight;
        float guardHeight;
        float modulesHeight;
        float gpsHeight;
        bool toolMinimized;

        void WindowBDAToolbar(int windowID)
        {
            float line = 0;
            float leftIndent = 10;
            float contentWidth = (toolWindowWidth) - (2 * leftIndent);
            float contentTop = 10;
            float entryHeight = 20;
            float _buttonSize = 26;
            float _windowMargin = 4;

            GUI.DragWindow(new Rect(_windowMargin + _buttonSize, 0, toolWindowWidth - 2 * _windowMargin - 4 * _buttonSize, _windowMargin + _buttonSize));

            line += 1.25f;
            line += 0.25f;

            // Version.
            GUI.Label(new Rect(toolWindowWidth - _windowMargin - 3 * _buttonSize - 57, 23, 57, 10), Version, waterMarkStyle);

            //SETTINGS BUTTON
            if (!BDKeyBinder.current &&
                GUI.Button(new Rect(toolWindowWidth - _windowMargin - _buttonSize, _windowMargin, _buttonSize, _buttonSize), settingsIconTexture, BDGuiSkin.button))
            {
                ToggleWindowSettings();
            }

            //vesselswitcher button
            if (hasVesselSwitcher)
            {
                GUIStyle vsStyle = showVesselSwitcherGUI ? BDGuiSkin.box : BDGuiSkin.button;
                if (GUI.Button(new Rect(toolWindowWidth - _windowMargin - 2 * _buttonSize, _windowMargin, _buttonSize, _buttonSize), "VS", vsStyle))
                {
                    showVesselSwitcherGUI = !showVesselSwitcherGUI;
                }
            }

            //VesselSpawner button
            if (hasVesselSpawner)
            {
                GUIStyle vsStyle = showVesselSpawnerGUI ? BDGuiSkin.box : BDGuiSkin.button;
                if (GUI.Button(new Rect(toolWindowWidth - _windowMargin - 3 * _buttonSize, _windowMargin, _buttonSize, _buttonSize), "Sp", vsStyle))
                {
                    showVesselSpawnerGUI = !showVesselSpawnerGUI;
                    if (!showVesselSpawnerGUI)
                        SaveConfig();
                }
            }

            if (ActiveWeaponManager != null)
            {
                //MINIMIZE BUTTON
                toolMinimized = GUI.Toggle(new Rect(_windowMargin, _windowMargin, _buttonSize, _buttonSize), toolMinimized, "_",
                    toolMinimized ? BDGuiSkin.box : BDGuiSkin.button);

                GUIStyle armedLabelStyle;
                Rect armedRect = new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth / 2, entryHeight);
                if (ActiveWeaponManager.guardMode)
                {
                    if (GUI.Button(armedRect, "- " + Localizer.Format("#LOC_BDArmory_WMWindow_GuardModebtn") + " -", BDGuiSkin.box))//Guard Mode
                    {
                        showGuardMenu = true;
                    }
                }
                else
                {
                    string armedText = Localizer.Format("#LOC_BDArmory_WMWindow_ArmedText");//"Trigger is "
                    if (ActiveWeaponManager.isArmed)
                    {
                        armedText += Localizer.Format("#LOC_BDArmory_WMWindow_ArmedText_ARMED");//"ARMED."
                        armedLabelStyle = BDGuiSkin.box;
                    }
                    else
                    {
                        armedText += Localizer.Format("#LOC_BDArmory_WMWindow_ArmedText_DisArmed");//"disarmed."
                        armedLabelStyle = BDGuiSkin.button;
                    }
                    if (GUI.Button(armedRect, armedText, armedLabelStyle))
                    {
                        ActiveWeaponManager.ToggleArm();
                    }
                }

                GUIStyle teamButtonStyle = BDGuiSkin.box;
                string teamText = $"{Localizer.Format("#LOC_BDArmory_WMWindow_TeamText")}: {ActiveWeaponManager.Team.Name}";//Team

                if (GUI.Button(new Rect(leftIndent + (contentWidth / 2), contentTop + (line * entryHeight), contentWidth / 2, entryHeight), teamText, teamButtonStyle))
                {
                    if (Event.current.button == 1)
                    {
                        BDTeamSelector.Instance.Open(ActiveWeaponManager, new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
                    }
                    else
                    {
                        ActiveWeaponManager.NextTeam();
                    }
                }
                line++;
                line += 0.25f;
                string weaponName = ActiveWeaponManager.selectedWeaponString;
                // = ActiveWeaponManager.selectedWeapon == null ? "None" : ActiveWeaponManager.selectedWeapon.GetShortName();
                string selectionText = Localizer.Format("#LOC_BDArmory_WMWindow_selectionText", weaponName);//Weapon: <<1>>
                GUI.Label(new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth, entryHeight * 1.25f), selectionText, BDGuiSkin.box);
                line += 1.25f;
                line += 0.1f;
                //if weapon can ripple, show option and slider.
                if (ActiveWeaponManager.hasLoadedRippleData && ActiveWeaponManager.canRipple)
                {
                    if (ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Gun
                        || ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.Rocket
                        || ActiveWeaponManager.selectedWeapon.GetWeaponClass() == WeaponClasses.DefenseLaser) //remove rocket ripple slider - moved to editor
                    {
                        string rippleText = ActiveWeaponManager.rippleFire
                            ? Localizer.Format("#LOC_BDArmory_WMWindow_rippleText1", ActiveWeaponManager.gunRippleRpm.ToString("0"))//"Barrage: " +  + " RPM"
                            : Localizer.Format("#LOC_BDArmory_WMWindow_rippleText2");//"Salvo"
                        GUIStyle rippleStyle = ActiveWeaponManager.rippleFire
                            ? BDGuiSkin.box
                            : BDGuiSkin.button;
                        if (
                            GUI.Button(
                                new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth / 2, entryHeight * 1.25f),
                                rippleText, rippleStyle))
                        {
                            ActiveWeaponManager.ToggleRippleFire();
                        }

                        rippleHeight = Mathf.Lerp(rippleHeight, 1.25f, 0.15f);
                    }
                    else
                    {
                        string rippleText = ActiveWeaponManager.rippleFire
                            ? Localizer.Format("#LOC_BDArmory_WMWindow_rippleText3", ActiveWeaponManager.rippleRPM.ToString("0"))//"Ripple: " +  + " RPM"
                            : Localizer.Format("#LOC_BDArmory_WMWindow_rippleText4");//"Ripple: OFF"
                        GUIStyle rippleStyle = ActiveWeaponManager.rippleFire
                            ? BDGuiSkin.box
                            : BDGuiSkin.button;
                        if (
                            GUI.Button(
                                new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth / 2, entryHeight * 1.25f),
                                rippleText, rippleStyle))
                        {
                            ActiveWeaponManager.ToggleRippleFire();
                        }
                        if (ActiveWeaponManager.rippleFire)
                        {
                            Rect sliderRect = new Rect(leftIndent + (contentWidth / 2) + 2,
                                contentTop + (line * entryHeight) + 6.5f, (contentWidth / 2) - 2, 12);
                            ActiveWeaponManager.rippleRPM = GUI.HorizontalSlider(sliderRect,
                                ActiveWeaponManager.rippleRPM, 100, 1600, rippleSliderStyle, rippleThumbStyle);
                        }
                        rippleHeight = Mathf.Lerp(rippleHeight, 1.25f, 0.15f);
                    }
                }
                else
                {
                    rippleHeight = Mathf.Lerp(rippleHeight, 0, 0.15f);
                }
                //line += 1.25f;
                line += rippleHeight;
                line += 0.1f;

                if (!toolMinimized)
                {
                    showWeaponList =
                        GUI.Toggle(new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth / 3, entryHeight),
                            showWeaponList, Localizer.Format("#LOC_BDArmory_WMWindow_ListWeapons"), showWeaponList ? BDGuiSkin.box : BDGuiSkin.button);//"Weapons"
                    showGuardMenu =
                        GUI.Toggle(
                            new Rect(leftIndent + (contentWidth / 3), contentTop + (line * entryHeight), contentWidth / 3,
                                entryHeight), showGuardMenu, Localizer.Format("#LOC_BDArmory_WMWindow_GuardMenu"),//"Guard Menu"
                            showGuardMenu ? BDGuiSkin.box : BDGuiSkin.button);
                    showModules =
                        GUI.Toggle(
                            new Rect(leftIndent + (2 * contentWidth / 3), contentTop + (line * entryHeight), contentWidth / 3,
                                entryHeight), showModules, Localizer.Format("#LOC_BDArmory_WMWindow_ModulesToggle"),//"Modules"
                            showModules ? BDGuiSkin.box : BDGuiSkin.button);
                    line++;
                }

                float weaponLines = 0;
                if (showWeaponList && !toolMinimized)
                {
                    line += 0.25f;
                    Rect weaponListGroupRect = new Rect(5, contentTop + (line * entryHeight), toolWindowWidth - 10,
                        ((float)ActiveWeaponManager.weaponArray.Length + 0.1f) * entryHeight);
                    GUI.BeginGroup(weaponListGroupRect, GUIContent.none, BDGuiSkin.box); //darker box
                    weaponLines += 0.1f;
                    for (int i = 0; i < ActiveWeaponManager.weaponArray.Length; i++)
                    {
                        GUIStyle wpnListStyle;
                        GUIStyle tgtStyle;
                        if (i == ActiveWeaponManager.weaponIndex)
                        {
                            wpnListStyle = middleLeftLabelOrange;
                            tgtStyle = targetModeStyleSelected;
                        }
                        else
                        {
                            wpnListStyle = middleLeftLabel;
                            tgtStyle = targetModeStyle;
                        }
                        string label;
                        string subLabel;
                        if (ActiveWeaponManager.weaponArray[i] != null)
                        {
                            label = ActiveWeaponManager.weaponArray[i].GetShortName();
                            subLabel = ActiveWeaponManager.weaponArray[i].GetSubLabel();
                        }
                        else
                        {
                            label = Localizer.Format("#LOC_BDArmory_WMWindow_NoneWeapon");//"None"
                            subLabel = String.Empty;
                        }
                        Rect weaponButtonRect = new Rect(leftIndent, (weaponLines * entryHeight),
                            weaponListGroupRect.width - (2 * leftIndent), entryHeight);

                        GUI.Label(weaponButtonRect, subLabel, tgtStyle);

                        if (GUI.Button(weaponButtonRect, label, wpnListStyle))
                        {
                            ActiveWeaponManager.CycleWeapon(i);
                        }

                        if (i < ActiveWeaponManager.weaponArray.Length - 1)
                        {
                            BDGUIUtils.DrawRectangle(
                                new Rect(weaponButtonRect.x, weaponButtonRect.y + weaponButtonRect.height,
                                    weaponButtonRect.width, 1), Color.white);
                        }
                        weaponLines++;
                    }
                    weaponLines += 0.1f;
                    GUI.EndGroup();
                }
                weaponsHeight = Mathf.Lerp(weaponsHeight, weaponLines, 0.15f);
                line += weaponsHeight;

                float guardLines = 0;
                if (showGuardMenu && !toolMinimized)
                {
                    line += 0.25f;
                    GUI.BeginGroup(
                        new Rect(5, contentTop + (line * entryHeight), toolWindowWidth - 10, 8.45f * entryHeight),
                        GUIContent.none, BDGuiSkin.box);
                    guardLines += 0.1f;
                    contentWidth -= 16;
                    leftIndent += 3;
                    string guardButtonLabel = Localizer.Format("#LOC_BDArmory_WMWindow_NoneWeapon", (ActiveWeaponManager.guardMode ? Localizer.Format("#LOC_BDArmory_Generic_On") : Localizer.Format("#LOC_BDArmory_Generic_Off")));//"Guard Mode " + "ON""Off"
                    if (GUI.Button(new Rect(leftIndent, (guardLines * entryHeight), contentWidth, entryHeight),
                        guardButtonLabel, ActiveWeaponManager.guardMode ? BDGuiSkin.box : BDGuiSkin.button))
                    {
                        ActiveWeaponManager.ToggleGuardMode();
                    }
                    guardLines += 1.25f;

                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_WMWindow_FiringInterval"), leftLabel);//"Firing Interval"
                    ActiveWeaponManager.targetScanInterval =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + (90), (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            ActiveWeaponManager.targetScanInterval, 1, 60);
                    ActiveWeaponManager.targetScanInterval = Mathf.Round(ActiveWeaponManager.targetScanInterval);
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.targetScanInterval.ToString(), leftLabel);
                    guardLines++;

                    // extension for feature_engagementenvelope: set the firing burst length
                    string burstLabel = Localizer.Format("#LOC_BDArmory_WMWindow_BurstLength");//"Burst Length"
                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), burstLabel, leftLabel);
                    ActiveWeaponManager.fireBurstLength =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + (90), (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            ActiveWeaponManager.fireBurstLength, 0, 60);
                    ActiveWeaponManager.fireBurstLength = Mathf.Round(ActiveWeaponManager.fireBurstLength * 2) / 2;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.fireBurstLength.ToString(), leftLabel);
                    guardLines++;

                    // extension for feature_engagementenvelope: set the firing accuracy tolarance
                    var oldAutoFireCosAngleAdjustment = ActiveWeaponManager.AutoFireCosAngleAdjustment;
                    string accuracyLabel = Localizer.Format("#LOC_BDArmory_WMWindow_FiringTolerance");//"Firing Angle"
                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), accuracyLabel, leftLabel);
                    ActiveWeaponManager.AutoFireCosAngleAdjustment =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + (90), (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            ActiveWeaponManager.AutoFireCosAngleAdjustment, 0, 2);
                    ActiveWeaponManager.AutoFireCosAngleAdjustment = Mathf.Round(ActiveWeaponManager.AutoFireCosAngleAdjustment * 20) / 20;
                    if (ActiveWeaponManager.AutoFireCosAngleAdjustment != oldAutoFireCosAngleAdjustment)
                        ActiveWeaponManager.OnAFCAAUpdated(null, null);
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.AutoFireCosAngleAdjustment.ToString(), leftLabel);
                    guardLines++;

                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_WMWindow_FieldofView"),//"Field of View"
                        leftLabel);
                    float guardAngle = ActiveWeaponManager.guardAngle;
                    guardAngle =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            guardAngle, 10, 360);
                    guardAngle = guardAngle / 10;
                    guardAngle = Mathf.Round(guardAngle);
                    ActiveWeaponManager.guardAngle = guardAngle * 10;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.guardAngle.ToString(), leftLabel);
                    guardLines++;

                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_WMWindow_VisualRange"), leftLabel);//"Visual Range"
                    float guardRange = ActiveWeaponManager.guardRange;
                    guardRange =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            guardRange, 100, BDArmorySettings.MAX_GUARD_VISUAL_RANGE);
                    guardRange = guardRange / 100;
                    guardRange = Mathf.Round(guardRange);
                    ActiveWeaponManager.guardRange = guardRange * 100;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.guardRange.ToString(), leftLabel);
                    guardLines++;

                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_WMWindow_GunsRange"), leftLabel);//"Guns Range"
                    float gRange = ActiveWeaponManager.gunRange;
                    gRange =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            gRange, 0, BDArmorySettings.MAX_BULLET_RANGE);
                    gRange /= 100f;
                    gRange = Mathf.Round(gRange);
                    gRange *= 100f;
                    ActiveWeaponManager.gunRange = gRange;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.gunRange.ToString(), leftLabel);
                    guardLines++;

                    GUI.Label(new Rect(leftIndent, (guardLines * entryHeight), 85, entryHeight), Localizer.Format("#LOC_BDArmory_WMWindow_MissilesTgt"), leftLabel);//"Missiles/Tgt"
                    float mslCount = ActiveWeaponManager.maxMissilesOnTarget;
                    mslCount =
                        GUI.HorizontalSlider(
                            new Rect(leftIndent + 90, (guardLines * entryHeight), contentWidth - 90 - 38, entryHeight),
                            mslCount, 1, MissileFire.maxAllowableMissilesOnTarget);
                    mslCount = Mathf.Round(mslCount);
                    ActiveWeaponManager.maxMissilesOnTarget = mslCount;
                    GUI.Label(new Rect(leftIndent + (contentWidth - 35), (guardLines * entryHeight), 35, entryHeight),
                        ActiveWeaponManager.maxMissilesOnTarget.ToString(), leftLabel);
                    guardLines++;

                    string targetType = Localizer.Format("#LOC_BDArmory_WMWindow_TargetType");//"Target Type: "
                    if (ActiveWeaponManager.targetMissiles)
                    {
                        targetType += Localizer.Format("#LOC_BDArmory_WMWindow_TargetType_Missiles");//"Missiles"
                    }
                    else
                    {
                        targetType += Localizer.Format("#LOC_BDArmory_WMWindow_TargetType_All");//"All Targets"
                    }

                    if (GUI.Button(new Rect(leftIndent, (guardLines * entryHeight), contentWidth, entryHeight), targetType,
                        BDGuiSkin.button))
                    {
                        ActiveWeaponManager.ToggleTargetType();
                    }
                    guardLines++;
                    GUI.EndGroup();
                    line += 0.1f;
                }
                guardHeight = Mathf.Lerp(guardHeight, guardLines, 0.15f);
                line += guardHeight;

                float moduleLines = 0;
                if (showModules && !toolMinimized)
                {
                    line += 0.25f;
                    GUI.BeginGroup(
                        new Rect(5, contentTop + (line * entryHeight), toolWindowWidth - 10, numberOfModules * entryHeight),
                        GUIContent.none, BDGuiSkin.box);

                    numberOfModules = 0;
                    moduleLines += 0.1f;
                    //RWR
                    if (ActiveWeaponManager.rwr)
                    {
                        numberOfModules++;
                        bool isEnabled = ActiveWeaponManager.rwr.displayRWR;
                        string label = Localizer.Format("#LOC_BDArmory_WMWindow_RadarWarning");//"Radar Warning Receiver"
                        Rect rwrRect = new Rect(leftIndent, +(moduleLines * entryHeight), contentWidth, entryHeight);
                        if (GUI.Button(rwrRect, label, isEnabled ? centerLabelOrange : centerLabel))
                        {
                            if (isEnabled)
                            {
                                //ActiveWeaponManager.rwr.DisableRWR();
                                ActiveWeaponManager.rwr.displayRWR = false;
                            }
                            else
                            {
                                //ActiveWeaponManager.rwr.EnableRWR();
                                ActiveWeaponManager.rwr.displayRWR = true;
                            }
                        }
                        moduleLines++;
                    }

                    //TGP
                    List<ModuleTargetingCamera>.Enumerator mtc = ActiveWeaponManager.targetingPods.GetEnumerator();
                    while (mtc.MoveNext())
                    {
                        if (mtc.Current == null) continue;
                        numberOfModules++;
                        bool isEnabled = (mtc.Current.cameraEnabled);
                        bool isActive = (mtc.Current == ModuleTargetingCamera.activeCam);
                        GUIStyle moduleStyle = isEnabled ? centerLabelOrange : centerLabel; // = mtc
                        string label = mtc.Current.part.partInfo.title;
                        if (isActive)
                        {
                            moduleStyle = centerLabelRed;
                            label = "[" + label + "]";
                        }
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines * entryHeight), contentWidth, entryHeight),
                            label, moduleStyle))
                        {
                            if (isActive)
                            {
                                mtc.Current.ToggleCamera();
                            }
                            else
                            {
                                mtc.Current.EnableCamera();
                            }
                        }
                        moduleLines++;
                    }
                    mtc.Dispose();

                    //RADAR
                    List<ModuleRadar>.Enumerator mr = ActiveWeaponManager.radars.GetEnumerator();
                    while (mr.MoveNext())
                    {
                        if (mr.Current == null) continue;
                        numberOfModules++;
                        GUIStyle moduleStyle = mr.Current.radarEnabled ? centerLabelBlue : centerLabel;
                        string label = mr.Current.radarName;
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines * entryHeight), contentWidth, entryHeight),
                            label, moduleStyle))
                        {
                            mr.Current.Toggle();
                        }
                        moduleLines++;
                    }
                    mr.Dispose();

                    //JAMMERS
                    List<ModuleECMJammer>.Enumerator jammer = ActiveWeaponManager.jammers.GetEnumerator();
                    while (jammer.MoveNext())
                    {
                        if (jammer.Current == null) continue;
                        if (jammer.Current.alwaysOn) continue;

                        numberOfModules++;
                        GUIStyle moduleStyle = jammer.Current.jammerEnabled ? centerLabelBlue : centerLabel;
                        string label = jammer.Current.part.partInfo.title;
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines * entryHeight), contentWidth, entryHeight),
                            label, moduleStyle))
                        {
                            jammer.Current.Toggle();
                        }
                        moduleLines++;
                    }
                    jammer.Dispose();

                    //Other modules
                    using (var module = ActiveWeaponManager.wmModules.GetEnumerator())
                        while (module.MoveNext())
                        {
                            if (module.Current == null) continue;

                            numberOfModules++;
                            GUIStyle moduleStyle = module.Current.Enabled ? centerLabelBlue : centerLabel;
                            string label = module.Current.Name;
                            if (GUI.Button(new Rect(leftIndent, +(moduleLines * entryHeight), contentWidth, entryHeight),
                                label, moduleStyle))
                            {
                                module.Current.Toggle();
                            }
                            moduleLines++;
                        }

                    //GPS coordinator
                    GUIStyle gpsModuleStyle = showWindowGPS ? centerLabelBlue : centerLabel;
                    numberOfModules++;
                    if (GUI.Button(new Rect(leftIndent, +(moduleLines * entryHeight), contentWidth, entryHeight),
                        Localizer.Format("#LOC_BDArmory_WMWindow_GPSCoordinator"), gpsModuleStyle))//"GPS Coordinator"
                    {
                        showWindowGPS = !showWindowGPS;
                    }
                    moduleLines++;

                    //wingCommander
                    if (ActiveWeaponManager.wingCommander)
                    {
                        GUIStyle wingComStyle = ActiveWeaponManager.wingCommander.showGUI
                            ? centerLabelBlue
                            : centerLabel;
                        numberOfModules++;
                        if (GUI.Button(new Rect(leftIndent, +(moduleLines * entryHeight), contentWidth, entryHeight),
                            Localizer.Format("#LOC_BDArmory_WMWindow_WingCommand"), wingComStyle))//"Wing Command"
                        {
                            ActiveWeaponManager.wingCommander.ToggleGUI();
                        }
                        moduleLines++;
                    }

                    GUI.EndGroup();

                    line += 0.1f;
                }
                modulesHeight = Mathf.Lerp(modulesHeight, moduleLines, 0.15f);
                line += modulesHeight;

                float gpsLines = 0;
                if (showWindowGPS && !toolMinimized)
                {
                    line += 0.25f;
                    GUI.BeginGroup(new Rect(5, contentTop + (line * entryHeight), toolWindowWidth, WindowRectGps.height));
                    WindowGPS();
                    GUI.EndGroup();
                    gpsLines = WindowRectGps.height / entryHeight;
                }
                gpsHeight = Mathf.Lerp(gpsHeight, gpsLines, 0.15f);
                line += gpsHeight;
            }
            else
            {
                GUI.Label(new Rect(leftIndent, contentTop + (line * entryHeight), contentWidth, entryHeight),
                   Localizer.Format("#LOC_BDArmory_WMWindow_NoWeaponManager"), BDGuiSkin.box);// "No Weapon Manager found."
                line++;
            }

            toolWindowHeight = Mathf.Lerp(toolWindowHeight, contentTop + (line * entryHeight) + 5, 1);
            var previousWindowHeight = WindowRectToolbar.height;
            WindowRectToolbar.height = toolWindowHeight;
            if (BDArmorySettings.STRICT_WINDOW_BOUNDARIES && toolWindowHeight < previousWindowHeight && Mathf.Round(WindowRectToolbar.y + previousWindowHeight) == Screen.height) // Window shrunk while being at edge of screen.
                WindowRectToolbar.y = Screen.height - WindowRectToolbar.height;
            BDGUIUtils.RepositionWindow(ref WindowRectToolbar);
        }

        bool validGPSName = true;

        //GPS window
        public void WindowGPS()
        {
            GUI.Box(WindowRectGps, GUIContent.none, BDGuiSkin.box);
            gpsEntryCount = 0;
            Rect listRect = new Rect(gpsBorder, gpsBorder, WindowRectGps.width - (2 * gpsBorder),
                WindowRectGps.height - (2 * gpsBorder));
            GUI.BeginGroup(listRect);
            string targetLabel = Localizer.Format("#LOC_BDArmory_WMWindow_GPSTarget") + ": " + ActiveWeaponManager.designatedGPSInfo.name;//GPS Target
            GUI.Label(new Rect(0, 0, listRect.width, gpsEntryHeight), targetLabel, kspTitleLabel);

            // Expand/Collapse Target Toggle button
            if (GUI.Button(new Rect(listRect.width - gpsEntryHeight, 0, gpsEntryHeight, gpsEntryHeight), showTargets ? "-" : "+", BDGuiSkin.button))
                showTargets = !showTargets;

            gpsEntryCount += 0.85f;
            if (ActiveWeaponManager.designatedGPSCoords != Vector3d.zero)
            {
                GUI.Label(new Rect(0, gpsEntryCount * gpsEntryHeight, listRect.width - gpsEntryHeight, gpsEntryHeight),
                    Misc.Misc.FormattedGeoPos(ActiveWeaponManager.designatedGPSCoords, true), BDGuiSkin.box);
                if (
                    GUI.Button(
                        new Rect(listRect.width - gpsEntryHeight, gpsEntryCount * gpsEntryHeight, gpsEntryHeight,
                            gpsEntryHeight), "X", BDGuiSkin.button))
                {
                    ActiveWeaponManager.designatedGPSInfo = new GPSTargetInfo();
                }
            }
            else
            {
                GUI.Label(new Rect(0, gpsEntryCount * gpsEntryHeight, listRect.width - gpsEntryHeight, gpsEntryHeight),
                    Localizer.Format("#LOC_BDArmory_WMWindow_NoTarget"), BDGuiSkin.box);//"No Target"
            }

            gpsEntryCount += 1.35f;
            int indexToRemove = -1;
            int index = 0;
            BDTeam myTeam = ActiveWeaponManager.Team;
            if (showTargets)
            {
                List<GPSTargetInfo>.Enumerator coordinate = BDATargetManager.GPSTargetList(myTeam).GetEnumerator();
                while (coordinate.MoveNext())
                {
                    Color origWColor = GUI.color;
                    if (coordinate.Current.EqualsTarget(ActiveWeaponManager.designatedGPSInfo))
                    {
                        GUI.color = XKCDColors.LightOrange;
                    }

                    string label = Misc.Misc.FormattedGeoPosShort(coordinate.Current.gpsCoordinates, false);
                    float nameWidth = 100;
                    if (editingGPSName && index == editingGPSNameIndex)
                    {
                        if (validGPSName && Event.current.type == EventType.KeyDown &&
                            Event.current.keyCode == KeyCode.Return)
                        {
                            editingGPSName = false;
                            hasEnteredGPSName = true;
                        }
                        else
                        {
                            Color origColor = GUI.color;
                            if (newGPSName.Contains(";") || newGPSName.Contains(":") || newGPSName.Contains(","))
                            {
                                validGPSName = false;
                                GUI.color = Color.red;
                            }
                            else
                            {
                                validGPSName = true;
                            }

                            newGPSName = GUI.TextField(
                              new Rect(0, gpsEntryCount * gpsEntryHeight, nameWidth, gpsEntryHeight), newGPSName, 12);
                            GUI.color = origColor;
                        }
                    }
                    else
                    {
                        if (GUI.Button(new Rect(0, gpsEntryCount * gpsEntryHeight, nameWidth, gpsEntryHeight),
                          coordinate.Current.name,
                          BDGuiSkin.button))
                        {
                            editingGPSName = true;
                            editingGPSNameIndex = index;
                            newGPSName = coordinate.Current.name;
                        }
                    }

                    if (
                      GUI.Button(
                        new Rect(nameWidth, gpsEntryCount * gpsEntryHeight, listRect.width - gpsEntryHeight - nameWidth,
                          gpsEntryHeight), label, BDGuiSkin.button))
                    {
                        ActiveWeaponManager.designatedGPSInfo = coordinate.Current;
                        editingGPSName = false;
                    }

                    if (
                      GUI.Button(
                        new Rect(listRect.width - gpsEntryHeight, gpsEntryCount * gpsEntryHeight, gpsEntryHeight,
                          gpsEntryHeight), "X", BDGuiSkin.button))
                    {
                        indexToRemove = index;
                    }

                    gpsEntryCount++;
                    index++;
                    GUI.color = origWColor;
                }
                coordinate.Dispose();
            }

            if (hasEnteredGPSName && editingGPSNameIndex < BDATargetManager.GPSTargetList(myTeam).Count)
            {
                hasEnteredGPSName = false;
                GPSTargetInfo old = BDATargetManager.GPSTargetList(myTeam)[editingGPSNameIndex];
                if (ActiveWeaponManager.designatedGPSInfo.EqualsTarget(old))
                {
                    ActiveWeaponManager.designatedGPSInfo.name = newGPSName;
                }
                BDATargetManager.GPSTargetList(myTeam)[editingGPSNameIndex] =
                    new GPSTargetInfo(BDATargetManager.GPSTargetList(myTeam)[editingGPSNameIndex].gpsCoordinates,
                        newGPSName);
                editingGPSNameIndex = 0;
                BDATargetManager.Instance.SaveGPSTargets();
            }

            GUI.EndGroup();

            if (indexToRemove >= 0)
            {
                BDATargetManager.GPSTargetList(myTeam).RemoveAt(indexToRemove);
                BDATargetManager.Instance.SaveGPSTargets();
            }

            WindowRectGps.height = (2 * gpsBorder) + (gpsEntryCount * gpsEntryHeight);
        }

        Rect SLineRect(float line, float indentLevel = 0)
        {
            return new Rect(settingsMargin + indentLevel * settingsMargin, line * settingsLineHeight, settingsWidth - 2 * settingsMargin - indentLevel * settingsMargin, settingsLineHeight);
        }

        Rect SLeftRect(float line, float indentLevel = 0)
        {
            return new Rect(settingsMargin + indentLevel * settingsMargin, line * settingsLineHeight, settingsWidth / 2 - settingsMargin - settingsMargin / 4 - indentLevel * settingsMargin, settingsLineHeight);
        }

        Rect SRightRect(float line, float indentLevel = 0)
        {
            return new Rect(settingsWidth / 2 + settingsMargin / 4 + indentLevel * settingsMargin, line * settingsLineHeight, settingsWidth / 2 - settingsMargin - settingsMargin / 4 - indentLevel * settingsMargin, settingsLineHeight);
        }

        Rect SLeftSliderRect(float line, float indentLevel = 0)
        {
            return new Rect(settingsMargin + indentLevel * settingsMargin, line * settingsLineHeight, settingsWidth / 2 + settingsMargin / 2 - indentLevel * settingsMargin, settingsLineHeight);
        }

        Rect SRightSliderRect(float line)
        {
            return new Rect(settingsMargin + settingsWidth / 2 + settingsMargin / 2, line * settingsLineHeight, settingsWidth / 2 - 7 / 2 * settingsMargin, settingsLineHeight);
        }

        Rect SLeftButtonRect(float line)
        {
            return new Rect(settingsMargin, line * settingsLineHeight, (settingsWidth - 2 * settingsMargin) / 2 - settingsMargin / 4, settingsLineHeight);
        }

        Rect SRightButtonRect(float line)
        {
            return new Rect(settingsWidth / 2 + settingsMargin / 4, line * settingsLineHeight, (settingsWidth - 2 * settingsMargin) / 2 - settingsMargin / 4, settingsLineHeight);
        }

        Rect SQuarterRect(float line, int pos)
        {
            return new Rect(settingsMargin + (pos % 4) * (settingsWidth - 2f * settingsMargin) / 4f, (line + (int)(pos / 4)) * settingsLineHeight, (settingsWidth - 2.5f * settingsMargin) / 4f, settingsLineHeight);
        }

        List<Rect> SRight2Rects(float line)
        {
            var rectGap = settingsMargin / 2;
            var rectWidth = ((settingsWidth - 2 * settingsMargin) / 2 - 2 * rectGap) / 2;
            var rects = new List<Rect>();
            rects.Add(new Rect(settingsWidth / 2 + rectGap / 2, line * settingsLineHeight, rectWidth, settingsLineHeight));
            rects.Add(new Rect(settingsWidth / 2 + rectWidth + rectGap * 3 / 2, line * settingsLineHeight, rectWidth, settingsLineHeight));
            return rects;
        }

        List<Rect> SRight3Rects(float line)
        {
            var rectGap = settingsMargin / 3;
            var rectWidth = ((settingsWidth - 2 * settingsMargin) / 2 - 3 * rectGap) / 3;
            var rects = new List<Rect>();
            rects.Add(new Rect(settingsWidth / 2 + rectGap / 2, line * settingsLineHeight, rectWidth, settingsLineHeight));
            rects.Add(new Rect(settingsWidth / 2 + rectWidth + rectGap * 3 / 2, line * settingsLineHeight, rectWidth, settingsLineHeight));
            rects.Add(new Rect(settingsWidth / 2 + 2 * rectWidth + rectGap * 5 / 2, line * settingsLineHeight, rectWidth, settingsLineHeight));
            return rects;
        }

        float settingsWidth;
        float settingsHeight;
        float settingsLeft;
        float settingsTop;
        float settingsLineHeight;
        float settingsMargin;

        bool editKeys;

        void SetupSettingsSize()
        {
            settingsWidth = 420;
            settingsHeight = 480;
            settingsLeft = Screen.width / 2 - settingsWidth / 2;
            settingsTop = 100;
            settingsLineHeight = 22;
            settingsMargin = 12;
            WindowRectSettings = new Rect(settingsLeft, settingsTop, settingsWidth, settingsHeight);
        }

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
        Dictionary<string, SpawnField> spawnFields;

        void WindowSettings(int windowID)
        {
            float line = 0.25f; // Top internal margin.
            GUI.Box(new Rect(0, 0, settingsWidth, settingsHeight), Localizer.Format("#LOC_BDArmory_Settings_Title"));//"BDArmory Settings"
            if (GUI.Button(new Rect(settingsWidth - 18, 2, 16, 16), "X"))
            {
                windowSettingsEnabled = false;
            }
            GUI.DragWindow(new Rect(0, 0, settingsWidth, 25));
            if (editKeys)
            {
                InputSettings();
                return;
            }

            if (GUI.Button(SLineRect(++line), (BDArmorySettings.GENERAL_SETTINGS_TOGGLE ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_GeneralSettingsToggle")))//Show/hide general settings.
            {
                BDArmorySettings.GENERAL_SETTINGS_TOGGLE = !BDArmorySettings.GENERAL_SETTINGS_TOGGLE;
            }
            if (BDArmorySettings.GENERAL_SETTINGS_TOGGLE)
            {
                BDArmorySettings.INSTAKILL = GUI.Toggle(SLeftRect(++line), BDArmorySettings.INSTAKILL, Localizer.Format("#LOC_BDArmory_Settings_Instakill"));//"Instakill"
                BDArmorySettings.INFINITE_AMMO = GUI.Toggle(SRightRect(line), BDArmorySettings.INFINITE_AMMO, Localizer.Format("#LOC_BDArmory_Settings_InfiniteAmmo"));//"Infinite Ammo"
                BDArmorySettings.BULLET_HITS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BULLET_HITS, Localizer.Format("#LOC_BDArmory_Settings_BulletHits"));//"Bullet Hits"
                BDArmorySettings.EJECT_SHELLS = GUI.Toggle(SRightRect(line), BDArmorySettings.EJECT_SHELLS, Localizer.Format("#LOC_BDArmory_Settings_EjectShells"));//"Eject Shells"
                BDArmorySettings.AIM_ASSIST = GUI.Toggle(SLeftRect(++line), BDArmorySettings.AIM_ASSIST, Localizer.Format("#LOC_BDArmory_Settings_AimAssist"));//"Aim Assist"
                BDArmorySettings.DRAW_AIMERS = GUI.Toggle(SRightRect(line), BDArmorySettings.DRAW_AIMERS, Localizer.Format("#LOC_BDArmory_Settings_DrawAimers"));//"Draw Aimers"
                BDArmorySettings.DRAW_DEBUG_LINES = GUI.Toggle(SLeftRect(++line), BDArmorySettings.DRAW_DEBUG_LINES, Localizer.Format("#LOC_BDArmory_Settings_DebugLines"));//"Debug Lines"
                BDArmorySettings.DRAW_DEBUG_LABELS = GUI.Toggle(SRightRect(line), BDArmorySettings.DRAW_DEBUG_LABELS, Localizer.Format("#LOC_BDArmory_Settings_DebugLabels"));//"Debug Labels"
                BDArmorySettings.REMOTE_SHOOTING = GUI.Toggle(SLeftRect(++line), BDArmorySettings.REMOTE_SHOOTING, Localizer.Format("#LOC_BDArmory_Settings_RemoteFiring"));//"Remote Firing"
                BDArmorySettings.BOMB_CLEARANCE_CHECK = GUI.Toggle(SRightRect(line), BDArmorySettings.BOMB_CLEARANCE_CHECK, Localizer.Format("#LOC_BDArmory_Settings_ClearanceCheck"));//"Clearance Check"
                BDArmorySettings.SHOW_AMMO_GAUGES = GUI.Toggle(SLeftRect(++line), BDArmorySettings.SHOW_AMMO_GAUGES, Localizer.Format("#LOC_BDArmory_Settings_AmmoGauges"));//"Ammo Gauges"
                BDArmorySettings.SHELL_COLLISIONS = GUI.Toggle(SRightRect(line), BDArmorySettings.SHELL_COLLISIONS, Localizer.Format("#LOC_BDArmory_Settings_ShellCollisions"));//"Shell Collisions"
                BDArmorySettings.BULLET_DECALS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BULLET_DECALS, Localizer.Format("#LOC_BDArmory_Settings_BulletHoleDecals"));//"Bullet Hole Decals"
                BDArmorySettings.DISABLE_RAMMING = GUI.Toggle(SRightRect(line), BDArmorySettings.DISABLE_RAMMING, Localizer.Format("#LOC_BDArmory_Settings_DisableRamming"));// Disable Ramming
                BDArmorySettings.DEFAULT_FFA_TARGETING = GUI.Toggle(SLeftRect(++line), BDArmorySettings.DEFAULT_FFA_TARGETING, Localizer.Format("#LOC_BDArmory_Settings_DefaultFFATargeting"));// Free-for-all combat style
                BDArmorySettings.DEBUG_RAMMING_LOGGING = GUI.Toggle(SRightRect(line), BDArmorySettings.DEBUG_RAMMING_LOGGING, Localizer.Format("#LOC_BDArmory_Settings_DebugRammingLogging"));// Disable Ramming
                BDArmorySettings.PERFORMANCE_LOGGING = GUI.Toggle(SLeftRect(++line), BDArmorySettings.PERFORMANCE_LOGGING, Localizer.Format("#LOC_BDArmory_Settings_PerformanceLogging"));//"Performance Logging"
                BDArmorySettings.STRICT_WINDOW_BOUNDARIES = GUI.Toggle(SRightRect(line), BDArmorySettings.STRICT_WINDOW_BOUNDARIES, Localizer.Format("#LOC_BDArmory_Settings_StrictWindowBoundaries"));//"Strict Window Boundaries"
                if (BDArmorySettings.TAG_MODE != (BDArmorySettings.TAG_MODE = GUI.Toggle(SLeftRect(++line), BDArmorySettings.TAG_MODE, Localizer.Format("#LOC_BDArmory_Settings_TagMode"))))//"Tag Mode"
                {
                    if (BDACompetitionMode.Instance != null) BDACompetitionMode.Instance.lastTagUpdateTime = Planetarium.GetUniversalTime();
                }
                if (BDArmorySettings.PAINTBALL_MODE != (BDArmorySettings.PAINTBALL_MODE = GUI.Toggle(SRightRect(line), BDArmorySettings.PAINTBALL_MODE, Localizer.Format("#LOC_BDArmory_Settings_PaintballMode"))))//"Paintball Mode"
                    BulletHitFX.SetupShellPool();
                BDArmorySettings.RUNWAY_PROJECT = GUI.Toggle(SLeftRect(++line), BDArmorySettings.RUNWAY_PROJECT, Localizer.Format("#LOC_BDArmory_Settings_RunwayProject"));//Runway Project
                BDArmorySettings.DISABLE_KILL_TIMER = GUI.Toggle(SRightRect(line), BDArmorySettings.DISABLE_KILL_TIMER, Localizer.Format("#LOC_BDArmory_Settings_DisableKillTimer"));//"Disable Kill Timer"
                if (BDArmorySettings.GRAVITY_HACKS != (BDArmorySettings.GRAVITY_HACKS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.GRAVITY_HACKS, Localizer.Format("#LOC_BDArmory_Settings_GravityHacks"))))//"Gravity hacks"
                {
                    if (BDArmorySettings.GRAVITY_HACKS)
                    {
                        BDArmorySettings.COMPETITION_INITIAL_GRACE_PERIOD = 10; // For gravity hacks, we need a shorter grace period.
                        BDArmorySettings.COMPETITION_KILL_TIMER = 1; // and a shorter kill timer.
                    }
                    else
                    {
                        BDArmorySettings.COMPETITION_INITIAL_GRACE_PERIOD = 60; // Reset grace period back to default of 60s.
                        BDArmorySettings.COMPETITION_KILL_TIMER = 15; // Reset kill timer period back to default of 15s.
                        PhysicsGlobals.GraviticForceMultiplier = 1;
                        VehiclePhysics.Gravity.Refresh();
                    }
                }
                BDArmorySettings.BATTLEDAMAGE = GUI.Toggle(SRightRect(line), BDArmorySettings.BATTLEDAMAGE, Localizer.Format("#LOC_BDArmory_Settings_BattleDamage"));
                BDArmorySettings.EXTRA_DAMAGE_SLIDERS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.EXTRA_DAMAGE_SLIDERS, Localizer.Format("#LOC_BDArmory_Settings_ExtraDamageSliders"));
                BDArmorySettings.AUTO_ENABLE_VESSEL_SWITCHING = GUI.Toggle(SRightRect(line), BDArmorySettings.AUTO_ENABLE_VESSEL_SWITCHING, Localizer.Format("#LOC_BDArmory_Settings_AutoEnableVesselSwitching"));
                BDArmorySettings.RESET_HP = GUI.Toggle(SLeftRect(++line), BDArmorySettings.RESET_HP, Localizer.Format("#LOC_BDArmory_Settings_ResetHP"));
                BDArmorySettings.DESTROY_UNCONTROLLED_WMS = GUI.Toggle(SRightRect(line), BDArmorySettings.DESTROY_UNCONTROLLED_WMS, Localizer.Format("#LOC_BDArmory_Settings_DestroyWMWhenNotControlled"));
                if (BDArmorySettings.KERBAL_SAFETY != (BDArmorySettings.KERBAL_SAFETY = GUI.Toggle(SLeftRect(++line), BDArmorySettings.KERBAL_SAFETY, Localizer.Format("#LOC_BDArmory_Settings_KerbalSafety"))))
                {
                    if (BDArmorySettings.KERBAL_SAFETY)
                        KerbalSafetyManager.Instance.EnableKerbalSafety();
                    else
                        KerbalSafetyManager.Instance.DisableKerbalSafety();
                }
                BDArmorySettings.AUTONOMOUS_COMBAT_SEATS = GUI.Toggle(SRightRect(line), BDArmorySettings.AUTONOMOUS_COMBAT_SEATS, Localizer.Format("#LOC_BDArmory_Settings_AutonomousCombatSeats"));
                if (HighLogic.LoadedSceneIsEditor)
                {
                    if (BDArmorySettings.SHOW_CATEGORIES != (BDArmorySettings.SHOW_CATEGORIES = GUI.Toggle(SLeftRect(++line), BDArmorySettings.SHOW_CATEGORIES, Localizer.Format("#LOC_BDArmory_Settings_ShowEditorSubcategories"))))//"Show Editor Subcategories"
                    {
                        KSP.UI.Screens.PartCategorizer.Instance.editorPartList.Refresh();
                    }
                    if (BDArmorySettings.AUTOCATEGORIZE_PARTS != (BDArmorySettings.AUTOCATEGORIZE_PARTS = GUI.Toggle(SRightRect(line), BDArmorySettings.AUTOCATEGORIZE_PARTS, Localizer.Format("#LOC_BDArmory_Settings_AutocategorizeParts"))))//"Autocategorize Parts"
                    {
                        KSP.UI.Screens.PartCategorizer.Instance.editorPartList.Refresh();
                    }
                }
                ++line;
            }
            if (BDArmorySettings.BATTLEDAMAGE)
            {
                if (GUI.Button(SLineRect(++line), (BDArmorySettings.GENERAL_SETTINGS_TOGGLE ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_BDSettingsToggle")))//Show/hide battle damage settings.
                {
                    BDArmorySettings.BATTLEDAMAGE_TOGGLE = !BDArmorySettings.BATTLEDAMAGE_TOGGLE;
                }
                if (BDArmorySettings.BATTLEDAMAGE_TOGGLE)
                {
                    line += 0.2f;
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_BD_Proc")}: ({BDArmorySettings.BD_DAMAGE_CHANCE}%)", leftLabel); //Proc Chance Frequency
                    BDArmorySettings.BD_DAMAGE_CHANCE = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.BD_DAMAGE_CHANCE, 0f, 100);

                    BDArmorySettings.BD_PROPULSION = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BD_PROPULSION, Localizer.Format("#LOC_BDArmory_Settings_BD_Engines"));//"Propulsion Systems Damage"
                    if (BDArmorySettings.BD_PROPULSION)
                    {
                        GUI.Label(SLeftSliderRect(++line, 1f), $"{Localizer.Format("#LOC_BDArmory_Settings_BD_Prop_Dmg_Mult")}:  ({BDArmorySettings.BD_PROP_DAM_RATE}x)", leftLabel); //Propulsion Damage Multiplier
                        BDArmorySettings.BD_PROP_DAM_RATE = (GUI.HorizontalSlider(SRightSliderRect(line), (float)Math.Round(BDArmorySettings.BD_PROP_DAM_RATE, 1), 0, 2));
                        BDArmorySettings.BD_INTAKES = GUI.Toggle(SLeftRect(++line, 1f), BDArmorySettings.BD_INTAKES, Localizer.Format("#LOC_BDArmory_Settings_BD_Intakes"));//"Intake Damage"
                        BDArmorySettings.BD_GIMBALS = GUI.Toggle(SRightRect(line, 1f), BDArmorySettings.BD_GIMBALS, Localizer.Format("#LOC_BDArmory_Settings_BD_Gimbals"));//"Gimbal Damage"
                    }
                    BDArmorySettings.BD_AEROPARTS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BD_AEROPARTS, Localizer.Format("#LOC_BDArmory_Settings_BD_Aero"));//"Flight Systems Damage"
                    if (BDArmorySettings.BD_AEROPARTS)
                    {
                        GUI.Label(SLeftSliderRect(++line, 1f), $"{Localizer.Format("#LOC_BDArmory_Settings_BD_Aero_Dmg_Mult")}:  ({BDArmorySettings.BD_LIFT_LOSS_RATE}x)", leftLabel); //Wing Damage Magnitude
                        BDArmorySettings.BD_LIFT_LOSS_RATE = (GUI.HorizontalSlider(SRightSliderRect(line), (float)Math.Round(BDArmorySettings.BD_LIFT_LOSS_RATE, 1), 0, 5));
                        BDArmorySettings.BD_CTRL_SRF = GUI.Toggle(SLeftRect(++line, 1f), BDArmorySettings.BD_CTRL_SRF, Localizer.Format("#LOC_BDArmory_Settings_BD_CtrlSrf"));//"Ctrl Surface Damage"
                    }
                    BDArmorySettings.BD_COCKPITS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BD_COCKPITS, Localizer.Format("#LOC_BDArmory_Settings_BD_Command"));//"Command & Control Damage"
                    if (BDArmorySettings.BD_COCKPITS)
                    {
                        BDArmorySettings.BD_PILOT_KILLS = GUI.Toggle(SLeftRect(++line, 1f), BDArmorySettings.BD_PILOT_KILLS, Localizer.Format("#LOC_BDArmory_Settings_BD_PilotKill"));//"Crew Fatalities"
                    }
                    BDArmorySettings.BD_TANKS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BD_TANKS, Localizer.Format("#LOC_BDArmory_Settings_BD_Tanks"));//"FuelTank Damage"
                    if (BDArmorySettings.BD_TANKS)
                    {
                        GUI.Label(SLeftSliderRect(++line, 1f), $"{Localizer.Format("#LOC_BDArmory_Settings_BD_Leak_Time")}:  ({BDArmorySettings.BD_TANK_LEAK_TIME}s)", leftLabel); // Leak Duration
                        BDArmorySettings.BD_TANK_LEAK_TIME = (int)(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.BD_TANK_LEAK_TIME, 0, 100));
                        GUI.Label(SLeftSliderRect(++line, 1f), $"{Localizer.Format("#LOC_BDArmory_Settings_BD_Leak_Rate")}:  ({BDArmorySettings.BD_TANK_LEAK_RATE}x)", leftLabel); //Leak magnitude
                        BDArmorySettings.BD_TANK_LEAK_RATE = (GUI.HorizontalSlider(SRightSliderRect(line), (float)Math.Round(BDArmorySettings.BD_TANK_LEAK_RATE, 1), 0, 5));
                    }
                    BDArmorySettings.BD_AMMOBINS = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BD_AMMOBINS, Localizer.Format("#LOC_BDArmory_Settings_BD_Ammo"));//"Ammo Explosions"
                    if (BDArmorySettings.BD_AMMOBINS)
                    {
                        BDArmorySettings.BD_VOLATILE_AMMO = GUI.Toggle(SLineRect(++line, 1f), BDArmorySettings.BD_VOLATILE_AMMO, Localizer.Format("#LOC_BDArmory_Settings_BD_Volatile_Ammo"));//"Ammo Bins Explode When Destroyed"
                    }
                    BDArmorySettings.BD_FIRES_ENABLED = GUI.Toggle(SLeftRect(++line), BDArmorySettings.BD_FIRES_ENABLED, Localizer.Format("#LOC_BDArmory_Settings_BD_Fires"));//"Fires"
                    if (BDArmorySettings.BD_FIRES_ENABLED)
                    {
                        BDArmorySettings.BD_FIRE_DOT = GUI.Toggle(SLeftRect(++line, 1f), BDArmorySettings.BD_FIRE_DOT, Localizer.Format("#LOC_BDArmory_Settings_BD_DoT"));//"Fire Damage"
                        GUI.Label(SLeftSliderRect(++line, 1f), $"{Localizer.Format("#LOC_BDArmory_Settings_BD_Fire_Dmg")}:  ({BDArmorySettings.BD_FIRE_DAMAGE}/s)", leftLabel); // "Fire Damage magnitude"
                        BDArmorySettings.BD_FIRE_DAMAGE = (int)(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.BD_FIRE_DAMAGE, 0f, 20));
                    }
                    ++line;
                }
            }
            if (GUI.Button(SLineRect(++line), (BDArmorySettings.SLIDER_SETTINGS_TOGGLE ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_SliderSettingsToggle")))//Show/hide slider settings.
            {
                BDArmorySettings.SLIDER_SETTINGS_TOGGLE = !BDArmorySettings.SLIDER_SETTINGS_TOGGLE;
            }
            if (BDArmorySettings.SLIDER_SETTINGS_TOGGLE)
            {
                line += 0.2f;
                float dmgMultiplier = BDArmorySettings.DMG_MULTIPLIER <= 100f ? BDArmorySettings.DMG_MULTIPLIER / 10f : BDArmorySettings.DMG_MULTIPLIER / 50f + 8f;
                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_DamageMultiplier")}:  ({BDArmorySettings.DMG_MULTIPLIER})", leftLabel); // Damage Multiplier
                dmgMultiplier = (int)GUI.HorizontalSlider(SRightSliderRect(line), dmgMultiplier, 1f, 28f);
                BDArmorySettings.DMG_MULTIPLIER = dmgMultiplier < 11 ? (int)(dmgMultiplier * 10f) : (int)(50f * (dmgMultiplier - 8f));
                if (BDArmorySettings.EXTRA_DAMAGE_SLIDERS)
                {
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_BallisticDamageMultiplier")}:  ({BDArmorySettings.BALLISTIC_DMG_FACTOR})", leftLabel);
                    BDArmorySettings.BALLISTIC_DMG_FACTOR = (int)(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.BALLISTIC_DMG_FACTOR * 20f, 0f, 60f)) / 20f;
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_ExplosiveDamageMultiplier")}:  ({BDArmorySettings.EXP_DMG_MOD_BALLISTIC_NEW})", leftLabel);
                    BDArmorySettings.EXP_DMG_MOD_BALLISTIC_NEW = (int)(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.EXP_DMG_MOD_BALLISTIC_NEW * 20f, 0f, 30f)) / 20f;
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_MissileExplosiveDamageMultiplier")}:  ({BDArmorySettings.EXP_DMG_MOD_MISSILE})", leftLabel);
                    BDArmorySettings.EXP_DMG_MOD_MISSILE = (int)(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.EXP_DMG_MOD_MISSILE * 4f, 0f, 40f)) / 4f;
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_ImplosiveDamageMultiplier")}:  ({BDArmorySettings.EXP_IMP_MOD})", leftLabel);
                    BDArmorySettings.EXP_IMP_MOD = (int)(GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.EXP_IMP_MOD * 20, 0f, 20f)) / 20f;
                }

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_MaxBulletHoles")}:  ({BDArmorySettings.MAX_NUM_BULLET_DECALS})", leftLabel); // Max Bullet Holes
                if (BDArmorySettings.MAX_NUM_BULLET_DECALS != (BDArmorySettings.MAX_NUM_BULLET_DECALS = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.MAX_NUM_BULLET_DECALS, 1f, 999)))
                    BulletHitFX.AdjustDecalPoolSizes(BDArmorySettings.MAX_NUM_BULLET_DECALS);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_TerrainAlertFrequency")}:  ({BDArmorySettings.TERRAIN_ALERT_FREQUENCY})", leftLabel); // Terrain alert frequency. Note: this is scaled by (int)(1+(radarAlt/500)^2) to avoid wasting too many cycles.
                BDArmorySettings.TERRAIN_ALERT_FREQUENCY = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.TERRAIN_ALERT_FREQUENCY, 1f, 5f);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CameraSwitchFrequency")}:  ({BDArmorySettings.CAMERA_SWITCH_FREQUENCY}s)", leftLabel); // Minimum camera switching frequency
                BDArmorySettings.CAMERA_SWITCH_FREQUENCY = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.CAMERA_SWITCH_FREQUENCY, 1f, 10f);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_DebrisCleanUpDelay")}:  ({BDArmorySettings.DEBRIS_CLEANUP_DELAY}s)", leftLabel); // Debris Clean-up delay
                BDArmorySettings.DEBRIS_CLEANUP_DELAY = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.DEBRIS_CLEANUP_DELAY, 1f, 60f);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionNonCompetitorRemovalDelay")}:  ({(BDArmorySettings.COMPETITION_NONCOMPETITOR_REMOVAL_DELAY > 60 ? "Off" : BDArmorySettings.COMPETITION_NONCOMPETITOR_REMOVAL_DELAY + "s")})", leftLabel); // Non-competitor removal frequency
                BDArmorySettings.COMPETITION_NONCOMPETITOR_REMOVAL_DELAY = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_NONCOMPETITOR_REMOVAL_DELAY, 1f, 61f);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionDuration")}: ({(BDArmorySettings.COMPETITION_DURATION > 0 ? BDArmorySettings.COMPETITION_DURATION + (BDArmorySettings.COMPETITION_DURATION > 1 ? " mins" : " min") : "Unlimited")})", leftLabel);
                BDArmorySettings.COMPETITION_DURATION = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_DURATION, 0f, 15f);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionInitialGracePeriod")}: ({BDArmorySettings.COMPETITION_INITIAL_GRACE_PERIOD}s)", leftLabel);
                BDArmorySettings.COMPETITION_INITIAL_GRACE_PERIOD = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_INITIAL_GRACE_PERIOD, 0f, 60f);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionFinalGracePeriod")}: ({(BDArmorySettings.COMPETITION_FINAL_GRACE_PERIOD > 60 ? "Inf" : BDArmorySettings.COMPETITION_FINAL_GRACE_PERIOD + "s")})", leftLabel);
                BDArmorySettings.COMPETITION_FINAL_GRACE_PERIOD = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_FINAL_GRACE_PERIOD, 0f, 61f);

                GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionKillTimer")}: ({BDArmorySettings.COMPETITION_KILL_TIMER}s, {(BDArmorySettings.DISABLE_KILL_TIMER ? "off" : "on")})", leftLabel); // FIXME the toggle and this slider could be merged
                BDArmorySettings.COMPETITION_KILL_TIMER = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_KILL_TIMER, 1f, 60f);

                if (BDArmorySettings.RUNWAY_PROJECT)
                {
                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionKillerGMGracePeriod")}: ({BDArmorySettings.COMPETITION_KILLER_GM_GRACE_PERIOD}s)", leftLabel);
                    BDArmorySettings.COMPETITION_KILLER_GM_GRACE_PERIOD = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_KILLER_GM_GRACE_PERIOD / 10f, 0, 18) * 10f;

                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionKillerGMFrequency")}: ({(BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY > 60 ? "Off" : BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY + "s")}, {(BDACompetitionMode.Instance.killerGMenabled ? "on" : "off")})", leftLabel);
                    BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY / 10f, 1, 6) * 10f; // For now, don't control the killerGMEnabled flag (it's controlled by right clicking M).
                                                                                                                                                                                              // BDACompetitionMode.Instance.killerGMenabled = !(BDArmorySettings.COMPETITION_KILLER_GM_FREQUENCY > 60);

                    GUI.Label(SLeftSliderRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionKillerGMMaxAltitude")}: ({(BDArmorySettings.COMPETITION_KILLER_GM_MAX_ALTITUDE > 100 ? "Never" : BDArmorySettings.COMPETITION_KILLER_GM_MAX_ALTITUDE + "km")})", leftLabel);
                    BDArmorySettings.COMPETITION_KILLER_GM_MAX_ALTITUDE = (int)GUI.HorizontalSlider(SRightSliderRect(line), BDArmorySettings.COMPETITION_KILLER_GM_MAX_ALTITUDE, 1f, 101f);
                }

                ++line;
            }

            if (GUI.Button(SLineRect(++line), (BDArmorySettings.RADAR_SETTINGS_TOGGLE ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_RadarSettingsToggle"))) // Show/hide radar settings.
            {
                BDArmorySettings.RADAR_SETTINGS_TOGGLE = !BDArmorySettings.RADAR_SETTINGS_TOGGLE;
            }
            if (BDArmorySettings.RADAR_SETTINGS_TOGGLE)
            {
                line += 0.2f;
                GUI.Label(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_RWRWindowScale") + ": " + (BDArmorySettings.RWR_WINDOW_SCALE * 100).ToString("0") + "%", leftLabel); // RWR Window Scale
                float rwrScale = BDArmorySettings.RWR_WINDOW_SCALE;
                rwrScale = Mathf.Round(GUI.HorizontalSlider(SRightRect(line), rwrScale, BDArmorySettings.RWR_WINDOW_SCALE_MIN, BDArmorySettings.RWR_WINDOW_SCALE_MAX) * 100.0f) * 0.01f;
                if (rwrScale.ToString(CultureInfo.InvariantCulture) != BDArmorySettings.RWR_WINDOW_SCALE.ToString(CultureInfo.InvariantCulture))
                {
                    ResizeRwrWindow(rwrScale);
                }

                GUI.Label(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_RadarWindowScale") + ": " + (BDArmorySettings.RADAR_WINDOW_SCALE * 100).ToString("0") + "%", leftLabel); // Radar Window Scale
                float radarScale = BDArmorySettings.RADAR_WINDOW_SCALE;
                radarScale = Mathf.Round(GUI.HorizontalSlider(SRightRect(line), radarScale, BDArmorySettings.RADAR_WINDOW_SCALE_MIN, BDArmorySettings.RADAR_WINDOW_SCALE_MAX) * 100.0f) * 0.01f;
                if (radarScale.ToString(CultureInfo.InvariantCulture) != BDArmorySettings.RADAR_WINDOW_SCALE.ToString(CultureInfo.InvariantCulture))
                {
                    ResizeRadarWindow(radarScale);
                }

                GUI.Label(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_TargetWindowScale") + ": " + (BDArmorySettings.TARGET_WINDOW_SCALE * 100).ToString("0") + "%", leftLabel); // Target Window Scale
                float targetScale = BDArmorySettings.TARGET_WINDOW_SCALE;
                targetScale = Mathf.Round(GUI.HorizontalSlider(SRightRect(line), targetScale, BDArmorySettings.TARGET_WINDOW_SCALE_MIN, BDArmorySettings.TARGET_WINDOW_SCALE_MAX) * 100.0f) * 0.01f;
                if (targetScale.ToString(CultureInfo.InvariantCulture) != BDArmorySettings.TARGET_WINDOW_SCALE.ToString(CultureInfo.InvariantCulture))
                {
                    ResizeTargetWindow(targetScale);
                }

                ++line;
            }

            if (GUI.Button(SLineRect(++line), (BDArmorySettings.OTHER_SETTINGS_TOGGLE ? "Hide " : "Show ") + Localizer.Format("#LOC_BDArmory_Settings_OtherSettingsToggle"))) // Show/hide other settings.
            {
                BDArmorySettings.OTHER_SETTINGS_TOGGLE = !BDArmorySettings.OTHER_SETTINGS_TOGGLE;
            }
            if (BDArmorySettings.OTHER_SETTINGS_TOGGLE)
            {
                line += 0.2f;
                GUI.Label(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_TriggerHold") + ": " + BDArmorySettings.TRIGGER_HOLD_TIME.ToString("0.00") + "s", leftLabel);//Trigger Hold
                BDArmorySettings.TRIGGER_HOLD_TIME = GUI.HorizontalSlider(SRightRect(line), BDArmorySettings.TRIGGER_HOLD_TIME, 0.02f, 1f);

                GUI.Label(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_UIVolume") + ": " + (BDArmorySettings.BDARMORY_UI_VOLUME * 100).ToString("0"), leftLabel);//UI Volume
                float uiVol = BDArmorySettings.BDARMORY_UI_VOLUME;
                uiVol = GUI.HorizontalSlider(SRightRect(line), uiVol, 0f, 1f);
                if (uiVol != BDArmorySettings.BDARMORY_UI_VOLUME && OnVolumeChange != null)
                {
                    OnVolumeChange();
                }
                BDArmorySettings.BDARMORY_UI_VOLUME = uiVol;

                GUI.Label(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_WeaponVolume") + ": " + (BDArmorySettings.BDARMORY_WEAPONS_VOLUME * 100).ToString("0"), leftLabel);//Weapon Volume
                float weaponVol = BDArmorySettings.BDARMORY_WEAPONS_VOLUME;
                weaponVol = GUI.HorizontalSlider(SRightRect(line), weaponVol, 0f, 1f);
                if (uiVol != BDArmorySettings.BDARMORY_WEAPONS_VOLUME && OnVolumeChange != null)
                {
                    OnVolumeChange();
                }
                BDArmorySettings.BDARMORY_WEAPONS_VOLUME = weaponVol;

                if (BDArmorySettings.DRAW_DEBUG_LABELS)
                {
                    if (GUI.Button(SLeftRect(++line), "Run DEBUG checks"))// Run DEBUG checks
                    {
                        BDACompetitionMode.Instance.RunDebugChecks();
                    }
                }
            }

            //competition mode
            if (HighLogic.LoadedSceneIsFlight)
            {
                ++line;
                if (BDArmorySettings.RUNWAY_PROJECT && BDArmorySettings.REMOTE_LOGGING_VISIBLE)
                {
                    bool remoteLoggingEnabled = BDArmorySettings.REMOTE_LOGGING_ENABLED;
                    BDArmorySettings.REMOTE_LOGGING_ENABLED = GUI.Toggle(SLeftRect(++line), remoteLoggingEnabled, Localizer.Format("#LOC_BDArmory_Settings_RemoteLogging"));//"Remote Logging"
                    if (remoteLoggingEnabled)
                    {
                        GUI.Label(SLeftRect(++line), $"{Localizer.Format("#LOC_BDArmory_Settings_CompetitionID")}: ", leftLabel); // Competition hash.
                        BDArmorySettings.COMPETITION_HASH = GUI.TextField(SRightRect(line), BDArmorySettings.COMPETITION_HASH);
                    }
                }
                else
                    BDArmorySettings.REMOTE_LOGGING_ENABLED = false;

                bool origPm = BDArmorySettings.PEACE_MODE;
                BDArmorySettings.PEACE_MODE = GUI.Toggle(SLeftRect(++line), BDArmorySettings.PEACE_MODE, Localizer.Format("#LOC_BDArmory_Settings_PeaceMode"));//"Peace Mode"
                if (BDArmorySettings.PEACE_MODE && !origPm)
                {
                    BDATargetManager.ClearDatabase();
                    if (OnPeaceEnabled != null)
                    {
                        OnPeaceEnabled();
                    }
                }

                ++line;
                GUI.Label(SLineRect(++line), "= " + Localizer.Format("#LOC_BDArmory_Settings_DogfightCompetition") + " =", centerLabel);//Dogfight Competition
                if (!BDACompetitionMode.Instance.competitionStarting)
                {
                    GUI.Label(SLeftRect(++line), Localizer.Format("#LOC_BDArmory_Settings_CompetitionDistance"));//"Competition Distance"
                    float cDist;
                    compDistGui = GUI.TextField(SRightRect(line), compDistGui);
                    if (Single.TryParse(compDistGui, out cDist))
                    {
                        BDArmorySettings.COMPETITION_DISTANCE = (int)cDist;
                    }

                    if (GUI.Button(SLeftButtonRect(++line), "Reset Scores")) // resets competition scores
                    {
                        BDACompetitionMode.Instance.ResetCompetitionScores();
                    }

                    if (GUI.Button(SRightButtonRect(line), Localizer.Format("#LOC_BDArmory_Settings_StartCompetition")))//"Start Competition"
                    {

                        BDArmorySettings.COMPETITION_DISTANCE = Mathf.Max(BDArmorySettings.COMPETITION_DISTANCE, 0);
                        compDistGui = BDArmorySettings.COMPETITION_DISTANCE.ToString();
                        BDACompetitionMode.Instance.StartCompetitionMode(BDArmorySettings.COMPETITION_DISTANCE);
                        SaveConfig();
                        windowSettingsEnabled = false;
                    }
                    if (BDArmorySettings.RUNWAY_PROJECT)
                    {
                        if (GUI.Button(SLeftButtonRect(++line), "Rapid Deploy"))
                        {
                            BDACompetitionMode.Instance.StartRapidDeployment(0);
                            SaveConfig();
                            windowSettingsEnabled = false;
                        }
                        if (BDArmorySettings.REMOTE_LOGGING_ENABLED && GUI.Button(SRightRect(line), "Sync Remote"))
                        {
                            string vesselPath = Environment.CurrentDirectory + $"/AutoSpawn";
                            if (!System.IO.Directory.Exists(vesselPath))
                            {
                                System.IO.Directory.CreateDirectory(vesselPath);
                            }
                            BDAScoreService.Instance.Configure(vesselPath, BDArmorySettings.COMPETITION_HASH);
                            SaveConfig();
                            windowSettingsEnabled = false;
                        }
                    }
                }
                else
                {
                    GUI.Label(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_CompetitionStarting") + " (" + compDistGui + ")");//Starting Competition...
                    if (GUI.Button(SLeftButtonRect(++line), Localizer.Format("#LOC_BDArmory_Generic_Cancel")))//"Cancel"
                    {
                        BDACompetitionMode.Instance.StopCompetition();
                    }
                    if (GUI.Button(SRightButtonRect(line), Localizer.Format("#LOC_BDArmory_Settings_StartCompetitionNow"))) // Start competition NOW button.
                    {
                        BDACompetitionMode.Instance.StartCompetitionNow();
                        SaveConfig();
                        windowSettingsEnabled = false;
                    }
                }
            }

            ++line;
            if (GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Settings_EditInputs")))//"Edit Inputs"
            {
                editKeys = true;
            }
            ++line;
            if (!BDKeyBinder.current && GUI.Button(SLineRect(++line), Localizer.Format("#LOC_BDArmory_Generic_SaveandClose")))//"Save and Close"
            {
                SaveConfig();
                windowSettingsEnabled = false;
            }

            line += 1.5f; // Bottom internal margin
            settingsHeight = (line * settingsLineHeight);
            WindowRectSettings.height = settingsHeight;
            BDGUIUtils.RepositionWindow(ref WindowRectSettings);
            BDGUIUtils.UseMouseEventInRect(WindowRectSettings);
        }

        internal static void ResizeRwrWindow(float rwrScale)
        {
            BDArmorySettings.RWR_WINDOW_SCALE = rwrScale;
            RadarWarningReceiver.RwrDisplayRect = new Rect(0, 0, RadarWarningReceiver.RwrSize * rwrScale,
              RadarWarningReceiver.RwrSize * rwrScale);
            BDArmorySetup.WindowRectRwr =
              new Rect(BDArmorySetup.WindowRectRwr.x, BDArmorySetup.WindowRectRwr.y,
                RadarWarningReceiver.RwrDisplayRect.height + RadarWarningReceiver.BorderSize,
                RadarWarningReceiver.RwrDisplayRect.height + RadarWarningReceiver.BorderSize + RadarWarningReceiver.HeaderSize);
        }

        internal static void ResizeRadarWindow(float radarScale)
        {
            BDArmorySettings.RADAR_WINDOW_SCALE = radarScale;
            VesselRadarData.RadarDisplayRect =
              new Rect(VesselRadarData.BorderSize / 2, VesselRadarData.BorderSize / 2 + VesselRadarData.HeaderSize,
                VesselRadarData.RadarScreenSize * radarScale,
                VesselRadarData.RadarScreenSize * radarScale);
            WindowRectRadar =
              new Rect(WindowRectRadar.x, WindowRectRadar.y,
                VesselRadarData.RadarDisplayRect.height + VesselRadarData.BorderSize + VesselRadarData.ControlsWidth + VesselRadarData.Gap * 3,
                VesselRadarData.RadarDisplayRect.height + VesselRadarData.BorderSize + VesselRadarData.HeaderSize);
        }

        internal static void ResizeTargetWindow(float targetScale)
        {
            BDArmorySettings.TARGET_WINDOW_SCALE = targetScale;
            ModuleTargetingCamera.ResizeTargetWindow();
        }

        private static Vector2 _displayViewerPosition = Vector2.zero;

        void InputSettings()
        {
            float line = 0f;
            int inputID = 0;
            float origSettingsWidth = settingsWidth;
            float origSettingsHeight = settingsHeight;
            float origSettingsMargin = settingsMargin;

            settingsMargin = 10;
            settingsWidth = origSettingsWidth - 2 * settingsMargin;
            settingsHeight = origSettingsHeight - 100;
            Rect viewRect = new Rect(2, 20, settingsWidth + GUI.skin.verticalScrollbar.fixedWidth, settingsHeight);
            Rect scrollerRect = new Rect(0, 0, settingsWidth - GUI.skin.verticalScrollbar.fixedWidth - 1, inputFields != null ? (inputFields.Length + 9) * settingsLineHeight : settingsHeight);

            _displayViewerPosition = GUI.BeginScrollView(viewRect, _displayViewerPosition, scrollerRect, false, true);

            GUI.Label(SLineRect(line), "- " + Localizer.Format("#LOC_BDArmory_InputSettings_Weapons") + " -", centerLabel);//Weapons
            line++;
            InputSettingsList("WEAP_", ref inputID, ref line);
            line++;

            GUI.Label(SLineRect(line), "- " + Localizer.Format("#LOC_BDArmory_InputSettings_TargetingPod") + " -", centerLabel);//Targeting Pod
            line++;
            InputSettingsList("TGP_", ref inputID, ref line);
            line++;

            GUI.Label(SLineRect(line), "- " + Localizer.Format("#LOC_BDArmory_InputSettings_Radar") + " -", centerLabel);//Radar
            line++;
            InputSettingsList("RADAR_", ref inputID, ref line);
            line++;

            GUI.Label(SLineRect(line), "- " + Localizer.Format("#LOC_BDArmory_InputSettings_VesselSwitcher") + " -", centerLabel);//Vessel Switcher
            line++;
            InputSettingsList("VS_", ref inputID, ref line);
            line++;

            GUI.Label(SLineRect(line), "- " + Localizer.Format("#LOC_BDArmory_InputSettings_Tournament") + " -", centerLabel);//Tournament
            line++;
            InputSettingsList("TOURNAMENT_", ref inputID, ref line);
            GUI.EndScrollView();

            line = settingsHeight / settingsLineHeight;
            line += 2;
            settingsWidth = origSettingsWidth;
            settingsMargin = origSettingsMargin;
            if (!BDKeyBinder.current && GUI.Button(SLineRect(line), Localizer.Format("#LOC_BDArmory_InputSettings_BackBtn")))//"Back"
            {
                editKeys = false;
            }

            settingsHeight = origSettingsHeight;
            WindowRectSettings.height = origSettingsHeight;
            BDGUIUtils.UseMouseEventInRect(WindowRectSettings);
        }

        void InputSettingsList(string prefix, ref int id, ref float line)
        {
            if (inputFields != null)
            {
                for (int i = 0; i < inputFields.Length; i++)
                {
                    string fieldName = inputFields[i].Name;
                    if (fieldName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        InputSettingsLine(fieldName, id++, ref line);
                    }
                }
            }
        }

        void InputSettingsLine(string fieldName, int id, ref float line)
        {
            GUI.Box(SLineRect(line), GUIContent.none);
            string label = String.Empty;
            if (BDKeyBinder.IsRecordingID(id))
            {
                string recordedInput;
                if (BDKeyBinder.current.AcquireInputString(out recordedInput))
                {
                    BDInputInfo orig = (BDInputInfo)typeof(BDInputSettingsFields).GetField(fieldName).GetValue(null);
                    BDInputInfo recorded = new BDInputInfo(recordedInput, orig.description);
                    typeof(BDInputSettingsFields).GetField(fieldName).SetValue(null, recorded);
                }

                label = "      " + Localizer.Format("#LOC_BDArmory_InputSettings_recordedInput");//Press a key or button.
            }
            else
            {
                BDInputInfo inputInfo = new BDInputInfo();
                try
                {
                    inputInfo = (BDInputInfo)typeof(BDInputSettingsFields).GetField(fieldName).GetValue(null);
                }
                catch (NullReferenceException)
                {
                    Debug.Log("[BDArmory]: Reflection failed to find input info of field: " + fieldName);
                    editKeys = false;
                    return;
                }
                label = " " + inputInfo.description + " : " + inputInfo.inputString;

                if (GUI.Button(SSetKeyRect(line), Localizer.Format("#LOC_BDArmory_InputSettings_SetKey")))//"Set Key"
                {
                    BDKeyBinder.BindKey(id);
                }
                if (GUI.Button(SClearKeyRect(line), Localizer.Format("#LOC_BDArmory_InputSettings_Clear")))//"Clear"
                {
                    typeof(BDInputSettingsFields).GetField(fieldName)
                        .SetValue(null, new BDInputInfo(inputInfo.description));
                }
            }
            GUI.Label(SLeftRect(line), label);
            line++;
        }

        Rect SSetKeyRect(float line)
        {
            return new Rect(settingsMargin + (2 * (settingsWidth - 2 * settingsMargin) / 3), line * settingsLineHeight,
                (settingsWidth - (2 * settingsMargin)) / 6, settingsLineHeight);
        }

        Rect SClearKeyRect(float line)
        {
            return
                new Rect(
                    settingsMargin + (2 * (settingsWidth - 2 * settingsMargin) / 3) + (settingsWidth - 2 * settingsMargin) / 6,
                    line * settingsLineHeight, (settingsWidth - (2 * settingsMargin)) / 6, settingsLineHeight);
        }

        #endregion GUI

        void HideGameUI()
        {
            GAME_UI_ENABLED = false;
        }

        void ShowGameUI()
        {
            GAME_UI_ENABLED = true;
        }

        internal void OnDestroy()
        {
            if (maySavethisInstance)
            {
                BDAWindowSettingsField.Save();
                SaveConfig();
            }

            GameEvents.onHideUI.Remove(HideGameUI);
            GameEvents.onShowUI.Remove(ShowGameUI);
            GameEvents.onVesselGoOffRails.Remove(OnVesselGoOffRails);
            GameEvents.OnGameSettingsApplied.Remove(SaveVolumeSettings);
            GameEvents.onVesselChange.Remove(VesselChange);
        }

        void OnVesselGoOffRails(Vessel v)
        {
            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory]: Loaded vessel: " + v.vesselName + ", Velocity: " + v.Velocity() + ", packed: " + v.packed);
                //v.SetWorldVelocity(Vector3d.zero);
            }
        }

        public void SaveVolumeSettings()
        {
            SeismicChargeFX.originalShipVolume = GameSettings.SHIP_VOLUME;
            SeismicChargeFX.originalMusicVolume = GameSettings.MUSIC_VOLUME;
            SeismicChargeFX.originalAmbienceVolume = GameSettings.AMBIENCE_VOLUME;
        }
    }
}
