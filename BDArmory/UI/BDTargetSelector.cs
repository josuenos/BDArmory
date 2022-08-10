using System.Collections;
using UnityEngine;
using KSP.Localization;

using BDArmory.Control;
using BDArmory.Utils;

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
            GUIStyle labelStyle = BDArmorySetup.BDGuiSkin.label;
            GUI.Label(new Rect(margin, height, width - 2 * margin, buttonHeight), Localizer.Format("#LOC_BDArmory_Selecttargeting"), labelStyle);
            if (GUI.Button(new Rect(width - 18, 2, 16, 16), "X"))
            {
                open = false;
            }
            height += buttonHeight;

            height += buttonGap;
            Rect CoMRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle CoMStyle = targetWeaponManager.targetCoM ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;
            //FIXME - switch these over to toggles instead of buttons; identified issue with weapon/engine targeting no sawing?
            if (GUI.Button(CoMRect, Localizer.Format("#LOC_BDArmory_TargetCOM"), CoMStyle))
            {
                targetWeaponManager.targetCoM = !targetWeaponManager.targetCoM;
                if (targetWeaponManager.targetCoM)
                {
                    targetWeaponManager.targetWeapon = false;
                    targetWeaponManager.targetEngine = false;
                    targetWeaponManager.targetCommand = false;
                    targetWeaponManager.targetMass = false;
                }
                if (!targetWeaponManager.targetCoM && (!targetWeaponManager.targetWeapon && !targetWeaponManager.targetEngine && !targetWeaponManager.targetCommand && !targetWeaponManager.targetMass))
                {
                    targetWeaponManager.targetMass = true;
                }
            }
            height += buttonHeight;

            height += buttonGap;
            Rect MassRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle MassStyle = targetWeaponManager.targetMass ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(MassRect, Localizer.Format("#LOC_BDArmory_Mass"), MassStyle))
            {
                targetWeaponManager.targetMass = !targetWeaponManager.targetMass;
                if (targetWeaponManager.targetMass)
                {
                    targetWeaponManager.targetCoM = false;
                }
                if (!targetWeaponManager.targetCoM && (!targetWeaponManager.targetWeapon && !targetWeaponManager.targetEngine && !targetWeaponManager.targetCommand && !targetWeaponManager.targetMass))
                {
                    targetWeaponManager.targetCoM = true;
                }
            }
            height += buttonHeight;

            height += buttonGap;
            Rect CommandRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle CommandStyle = targetWeaponManager.targetCommand ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(CommandRect, Localizer.Format("#LOC_BDArmory_Command"), CommandStyle))
            {
                targetWeaponManager.targetCommand = !targetWeaponManager.targetCommand;
                if (targetWeaponManager.targetCommand)
                {
                    targetWeaponManager.targetCoM = false;
                }
                if (!targetWeaponManager.targetCoM && (!targetWeaponManager.targetWeapon && !targetWeaponManager.targetEngine && !targetWeaponManager.targetCommand && !targetWeaponManager.targetMass))
                {
                    targetWeaponManager.targetCoM = true;
                }
            }
            height += buttonHeight;

            height += buttonGap;
            Rect EngineRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle EngineStyle = targetWeaponManager.targetEngine ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(EngineRect, Localizer.Format("#LOC_BDArmory_Engines"), EngineStyle))
            {
                targetWeaponManager.targetEngine = !targetWeaponManager.targetEngine;
                if (targetWeaponManager.targetEngine)
                {
                    targetWeaponManager.targetCoM = false;
                }
                if (!targetWeaponManager.targetCoM && (!targetWeaponManager.targetWeapon && !targetWeaponManager.targetEngine && !targetWeaponManager.targetCommand && !targetWeaponManager.targetMass))
                {
                    targetWeaponManager.targetCoM = true;
                }
            }
            height += buttonHeight;

            height += buttonGap;
            Rect weaponRect = new Rect(margin, height, width - 2 * margin, buttonHeight);
            GUIStyle WepStyle = targetWeaponManager.targetWeapon ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button;

            if (GUI.Button(weaponRect, Localizer.Format("#LOC_BDArmory_Weapons"), WepStyle))
            {
                targetWeaponManager.targetWeapon = !targetWeaponManager.targetWeapon;
                if (targetWeaponManager.targetWeapon)
                {
                    targetWeaponManager.targetCoM = false;
                }
                if (!targetWeaponManager.targetCoM && (!targetWeaponManager.targetWeapon && !targetWeaponManager.targetEngine && !targetWeaponManager.targetCommand && !targetWeaponManager.targetMass))
                {
                    targetWeaponManager.targetCoM = true;
                }
            }
            height += buttonHeight;

            height += margin;
            targetWeaponManager.targetingString = (targetWeaponManager.targetCoM ? Localizer.Format("#LOC_BDArmory_TargetCOM") + "; " : "")
                + (targetWeaponManager.targetMass ? Localizer.Format("#LOC_BDArmory_Mass") + "; " : "")
                + (targetWeaponManager.targetCommand ? Localizer.Format("#LOC_BDArmory_Command") + "; " : "")
                + (targetWeaponManager.targetEngine ? Localizer.Format("#LOC_BDArmory_Engines") + "; " : "")
                + (targetWeaponManager.targetWeapon ? Localizer.Format("#LOC_BDArmory_Weapons") + "; " : "");
            GUIUtils.RepositionWindow(ref window);
            GUIUtils.UseMouseEventInRect(window);
        }

        protected virtual void OnGUI()
        {
            if (!BDArmorySetup.GAME_UI_ENABLED) return;
            if (ready)
            {
                if (!open) return;

                var clientRect = new Rect(
                    Mathf.Min(windowLocation.x, Screen.width - width),
                    Mathf.Min(windowLocation.y, Screen.height - height),
                    width,
                    height);
                BDArmorySetup.SetGUIOpacity();
                window = GUI.Window(10591029, clientRect, TargetingSelectorWindow, "", BDArmorySetup.BDGuiSkin.window);
                BDArmorySetup.SetGUIOpacity(false);
                GUIUtils.UpdateGUIRect(window, guiCheckIndex);
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
            guiCheckIndex = GUIUtils.RegisterGUIRect(new Rect());
        }
    }
}
