using System.Collections;
using BDArmory.Core;
using BDArmory.Misc;
using BDArmory.Modules;
using UnityEngine;
using KSP.Localization;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.FlightAndEditor, false)]
    public class BDTargetSelector : MonoBehaviour
    {
        public static BDTargetSelector Instance;

        const float width = 250;
        const float margin = 5;
        const float buttonHeight = 20;
        const float buttonGap = 2;

        private int guiCheckIndex;
        private bool ready = false;
        private bool open = false;
        private Rect window;
        private float height;

        private Vector2 windowLocation;
        private MissileFire targetWeaponManager;

        public void Open(MissileFire weaponManager, Vector2 position)
        {
            open = true;
            targetWeaponManager = weaponManager;
            windowLocation = position;
        }

        private void TargetingSelectorWindow(int id)
        {
            height = margin;
            height += buttonHeight;
            
            height += buttonGap;
            Rect CoMRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle CoMStyle = targetWeaponManager.targetCoM ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(CoMRect, "#LOC_BDArmory_TargetCOM", CoMStyle))
            {
                targetWeaponManager.targetWeapon = false;
                targetWeaponManager.targetEngine = false;
                targetWeaponManager.targetCommand = false;
                targetWeaponManager.targetMass = false;
                targetWeaponManager.targetCoM = true;
                targetWeaponManager.targetingString = "#LOC_BDArmory_COM";
                open = false;
            }
            height += buttonHeight;

            height += buttonGap;
            Rect MassRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle MassStyle = targetWeaponManager.targetCoM ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(MassRect, "#LOC_BDArmory_Settings_AT_Mass", MassStyle))
            {
                targetWeaponManager.targetWeapon = false;
                targetWeaponManager.targetEngine = false;
                targetWeaponManager.targetCommand = false;
                targetWeaponManager.targetMass = true;
                targetWeaponManager.targetCoM = false;
                targetWeaponManager.targetingString = "#LOC_BDArmory_Mass";
                open = false;
            }
            height += buttonHeight;

            height += buttonGap;
            Rect CommandRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle CommandStyle = targetWeaponManager.targetCommand ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(CommandRect, "#LOC_BDArmory_Settings_AT_Command", CommandStyle))
            {
                targetWeaponManager.targetWeapon = false;
                targetWeaponManager.targetEngine = false;
                targetWeaponManager.targetCommand = true;
                targetWeaponManager.targetMass = false;
                targetWeaponManager.targetCoM = false;
                targetWeaponManager.targetingString = "#LOC_BDArmory_Command";
                open = false;
            }
            height += buttonHeight;

            height += buttonGap;
            Rect EngineRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle EngineStyle = targetWeaponManager.targetEngine ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(EngineRect, "#LOC_BDArmory_Settings_AT_Engines", EngineStyle))
            {
                targetWeaponManager.targetWeapon = false;
                targetWeaponManager.targetEngine = true;
                targetWeaponManager.targetCommand = false;
                targetWeaponManager.targetMass = false;
                targetWeaponManager.targetCoM = false;
                targetWeaponManager.targetingString = "#LOC_BDArmory_Engines";
                open = false;
            }
            height += buttonHeight;

            height += buttonGap;
            Rect weaponRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle WepStyle = targetWeaponManager.targetWeapon ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(weaponRect, "#LOC_BDArmory_Settings_AT_Weapons", WepStyle))
            {
                targetWeaponManager.targetWeapon = true;
                targetWeaponManager.targetEngine = false;
                targetWeaponManager.targetCommand = false;
                targetWeaponManager.targetMass = false;
                targetWeaponManager.targetCoM = false;
                targetWeaponManager.targetingString = "#LOC_BDArmory_Weapons";
                open = false;
            }
            height += buttonHeight;

            height += margin;
            BDGUIUtils.RepositionWindow(ref window);
            BDGUIUtils.UseMouseEventInRect(window);
        }

        protected virtual void OnGUI()
        {
            if (ready)
            {
                if (open && BDArmorySetup.GAME_UI_ENABLED
                    && Event.current.type == EventType.MouseDown
                    && !window.Contains(Event.current.mousePosition))
                {
                    open = false;
                }

                if (open && BDArmorySetup.GAME_UI_ENABLED)
                {
                    var clientRect = new Rect(
                        Mathf.Min(windowLocation.x, Screen.width - width),
                        Mathf.Min(windowLocation.y, Screen.height - height),
                        width,
                        height);
                    window = GUI.Window(10591029, clientRect, TargetingSelectorWindow, "", BDArmorySetup.BDGuiSkin.window);
                    Misc.Misc.UpdateGUIRect(window, guiCheckIndex);
                }
                else
                {
                    Misc.Misc.UpdateGUIRect(new Rect(), guiCheckIndex);
                }
            }
        }

        private void Awake()
        {
            if (Instance)
                Destroy(Instance);
            Instance = this;
        }

        private void Start()
        {
            StartCoroutine(WaitForBdaSettings());
        }

        private void OnDestroy()
        {
            ready = false;
        }

        private IEnumerator WaitForBdaSettings()
        {
            while (BDArmorySetup.Instance == null)
                yield return null;

            ready = true;
            guiCheckIndex = Misc.Misc.RegisterGUIRect(new Rect());
        }
    }
}
