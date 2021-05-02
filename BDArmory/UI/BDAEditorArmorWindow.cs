using System;
using System.Collections;
using System.Collections.Generic;
using BDArmory.Core;
using BDArmory.Core.Module;
using BDArmory.Misc;
using BDArmory.Modules;
using BDArmory.Radar;
using KSP.UI.Screens;
using UnityEngine;

namespace BDArmory.UI
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    internal class BDAEditorArmorWindow : MonoBehaviour
    {
        public static BDAEditorArmorWindow Instance = null;
        private ApplicationLauncherButton toolbarButton = null;

        private bool showArmorWindow = false;
        private string windowTitle = "BDArmory Craft Armor Tools";
        private Rect windowRect = new Rect(300, 150, 300, 350);

        private GUIContent[] armorGUI;
        private GUIContent armorBoxText;
        private BDGUIComboBox armorBox;
        private int previous_index = -1;

        private float totalArmorMass;
        private bool CalcArmor = false;
        private bool SetType = false;
        private bool SetThickness = false;
        private string selectedArmor = "None";
        private bool armorslist = false;
        private float armorThickness = 10;
        private float oldThickness = 10;
        private float maxThickness = 60;
        private bool Visualizer = false;
        private bool oldVisualizer = false;
        private bool refreshVisualizer = false;
        void Awake()
        {
        }

        void Start()
        {
            Instance = this;
            AddToolbarButton();
            GameEvents.onEditorShipModified.Add(OnEditorShipModifiedEvent);
        }

        private void FillArmorList()
        {
            armorGUI = new GUIContent[ArmorInfo.armors.Count];
            for (int i = 0; i < ArmorInfo.armors.Count; i++)
            {
                GUIContent gui = new GUIContent(ArmorInfo.armors[i].name);
                armorGUI[i] = gui;
            }

            armorBoxText = new GUIContent();
            armorBoxText.text = "Select Armor Material";
        }

        private void OnEditorShipModifiedEvent(ShipConstruct data)
        {
            CalcArmor = true;
            refreshVisualizer = true;
        }

        private void OnDestroy()
        {
            GameEvents.onEditorShipModified.Remove(OnEditorShipModifiedEvent);

            if (toolbarButton)
            {
                ApplicationLauncher.Instance.RemoveModApplication(toolbarButton);
                toolbarButton = null;
            }
        }

        IEnumerator ToolbarButtonRoutine()
        {
            if (toolbarButton || (!HighLogic.LoadedSceneIsEditor)) yield break;
            while (!ApplicationLauncher.Ready)
            {
                yield return null;
            }

            AddToolbarButton();
        }

        void AddToolbarButton()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                if (toolbarButton == null)
                {
                    Texture buttonTexture = GameDatabase.Instance.GetTexture(BDArmorySetup.textureDir + "icon_Armor", false);
                    toolbarButton = ApplicationLauncher.Instance.AddModApplication(ShowToolbarGUI, HideToolbarGUI, Dummy, Dummy, Dummy, Dummy, ApplicationLauncher.AppScenes.SPH | ApplicationLauncher.AppScenes.VAB, buttonTexture);
                }
            }
        }

        public void ShowToolbarGUI()
        {
            showArmorWindow = true;
            CalcArmor = true;
        }

        public void HideToolbarGUI()
        {
            showArmorWindow = false;
            CalcArmor = false;
        }

        void Dummy()
        { }

        void OnGUI()
        {
            if (showArmorWindow)
            {
                windowRect = GUI.Window(this.GetInstanceID(), windowRect, WindowArmor, windowTitle, BDArmorySetup.BDGuiSkin.window);

            }

            PreventClickThrough();
        }

        void WindowArmor(int windowID)
        {
            if (GUI.Button(new Rect(windowRect.width - 18, 2, 16, 16), "X"))
            {
                HideToolbarGUI();
            }

            if (CalcArmor)
            {
                SetType = false;
                CalculateArmorMass();
            }
            if (armorThickness != oldThickness)
            {
                oldThickness = armorThickness;
                SetThickness = true;
                CalculateArmorMass();
            }

            GUIStyle style = BDArmorySetup.BDGuiSkin.label;        

            style.fontStyle = FontStyle.Normal;
            GUI.Label(new Rect(10, 50, 300, 20), "Total Armor mass for Vessel: " + totalArmorMass, style);

            Visualizer = GUI.Toggle(new Rect(10, 30, 280, 20), Visualizer, "Toggle Armor Visualizer", Visualizer ? BDArmorySetup.BDGuiSkin.box : BDArmorySetup.BDGuiSkin.button);
            if (refreshVisualizer || Visualizer != oldVisualizer)
            {
                VisualizeArmor();
            }

            GUI.Label(new Rect(10, 80, 300, 20), "Armor Thickness: " + armorThickness + "mm", style);
            armorThickness = GUI.HorizontalSlider(new Rect(20, 100, 260, 20), armorThickness, 0, maxThickness);
            armorThickness /= 5;
            armorThickness = Mathf.Round(armorThickness);
            armorThickness *= 5;

            if (!armorslist)
            {
                FillArmorList();
                GUIStyle listStyle = new GUIStyle(BDArmorySetup.BDGuiSkin.button);
                listStyle.fixedHeight = 18; //make list contents slightly smaller
                armorBox = new BDGUIComboBox(new Rect(10, 130, 280, 20), new Rect(10, 130, 280, 20), armorBoxText, armorGUI, 120, listStyle);
                armorslist = true;
            }

            int selected_index = armorBox.Show();

            if (selected_index != previous_index)
            {
                if (selected_index != -1)
                {
                    selectedArmor = ArmorInfo.armors[selected_index].name;
                    SetType = true;
                    CalculateArmorMass();
                }
            }
            previous_index = selected_index;

            GUI.DragWindow();
            BDGUIUtils.RepositionWindow(ref windowRect);
        }

        void CalculateArmorMass()
        {
            if (EditorLogic.RootPart == null)
                return;

            // Encapsulate editor ShipConstruct into a vessel:
            Vessel v = new Vessel();
            v.parts = EditorLogic.fetch.ship.Parts;
            totalArmorMass = 0;
            using (List<Part>.Enumerator parts = EditorLogic.fetch.ship.Parts.GetEnumerator())
                while (parts.MoveNext())
                {
                    HitpointTracker armor = parts.Current.GetComponent<HitpointTracker>();
                    if (armor != null)
                    {
                        if (SetType || SetThickness)
                        {
                            if (SetType)
                            {
                                armor.ArmorTypeNum = (ArmorInfo.armors.FindIndex(t => t.name == selectedArmor) + 1);
                            }
                            if (SetThickness)
                            {
                                if (armor.name != "None")
                                {
                                    armor.Armor = Mathf.Clamp(armorThickness, 0, armor.maxSupportedArmor);
                                }
                            }
                            armor.ArmorSetup(null, null);
                        }
                        if (armor.maxSupportedArmor > maxThickness)
                        {
                            maxThickness = armor.maxSupportedArmor;
                        }
                        totalArmorMass += armor.armorMass;
                    }
                }
            CalcArmor = false;
            if (SetType || SetThickness)
            {
                refreshVisualizer = true;
            }
            SetType = false;
            SetThickness = false;
        }
        void VisualizeArmor()
        {
            if (EditorLogic.RootPart == null)
                return;
            // Encapsulate editor ShipConstruct into a vessel:
            Vessel v = new Vessel();
            v.parts = EditorLogic.fetch.ship.Parts;
            if (Visualizer)
            {
                using (List<Part>.Enumerator parts = EditorLogic.fetch.ship.Parts.GetEnumerator())
                    while (parts.MoveNext())
                    {
                        HitpointTracker a = parts.Current.GetComponent<HitpointTracker>();
                        if (a != null)
                        {
                            //float h = armor.ArmorTypeNum / (ArmorInfo.armors.Count + 1);
                            //Color ArmorColor = Color.HSVToRGB((armor.ArmorTypeNum / (ArmorInfo.armors.Count + 1)) 1f, 1f);
                            //ArmorColor.a = ((armor.Armor / armor.maxSupportedArmor) * 255);
                            parts.Current.SetHighlightColor(Color.HSVToRGB((a.ArmorTypeNum / (ArmorInfo.armors.Count + 1)), ((a.Armor / a.maxSupportedArmor)*5), 1f));
                            parts.Current.SetHighlight(true, false);
                            parts.Current.highlightType = Part.HighlightType.AlwaysOn;
                        }
                    }
            }
            else
            {
                using (List<Part>.Enumerator parts = EditorLogic.fetch.ship.Parts.GetEnumerator())
                    while (parts.MoveNext())
                    {
                        HitpointTracker armor = parts.Current.GetComponent<HitpointTracker>();
                        if (armor != null)
                        {
                            parts.Current.highlightType = Part.HighlightType.OnMouseOver;
                            parts.Current.SetHighlightColor(Part.defaultHighlightPart);
                            parts.Current.SetHighlight(false, false);
                        }
                    }
            }
            oldVisualizer = Visualizer;
            refreshVisualizer = false;
        }
/// <summary>
/// Lock the model if our own window is shown and has cursor focus to prevent click-through.
/// Code adapted from FAR Editor GUI
/// </summary>
private void PreventClickThrough()
        {
            bool cursorInGUI = false;
            EditorLogic EdLogInstance = EditorLogic.fetch;
            if (!EdLogInstance)
            {
                return;
            }
            if (showArmorWindow)
            {
                cursorInGUI = windowRect.Contains(GetMousePos());
            }
            if (cursorInGUI)
            {
                if (!CameraMouseLook.GetMouseLook())
                    EdLogInstance.Lock(false, false, false, "BDARCSLOCK");
                else
                    EdLogInstance.Unlock("BDARCSLOCK");
            }
            else if (!cursorInGUI)
            {
                EdLogInstance.Unlock("BDARCSLOCK");
            }
        }

        private Vector3 GetMousePos()
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            return mousePos;
        }
    } //EditorRCsWindow
}
