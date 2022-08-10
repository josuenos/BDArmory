using System;
using System.Reflection;
using UnityEngine;

using BDArmory.Settings;

namespace BDArmory.Utils
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class FerramAerospace : MonoBehaviour
    {
        public static FerramAerospace Instance;
        public static bool hasFAR = false;
        private static bool hasCheckedForFAR = false;
        public static bool hasFARWing = false;
        public static bool hasFARControllableSurface = false;
        private static bool hasCheckedForFARWing = false;
        private static bool hasCheckedForFARControllableSurface = false;

        public static Assembly FARAssembly;
        public static Type FARWingModule;
        public static Type FARControllableSurfaceModule;


        void Awake()
        {
            if (Instance != null) return; // Don't replace existing instance.
            Instance = new FerramAerospace();
        }

        void Start()
        {
            CheckForFAR();
            if (hasFAR)
            {
                CheckForFARWing();
                CheckForFARControllableSurface();
            }
        }

        public static bool CheckForFAR()
        {
            if (hasCheckedForFAR) return hasFAR;
            hasCheckedForFAR = true;
            foreach (var assy in AssemblyLoader.loadedAssemblies)
            {
                if (assy.assembly.FullName.StartsWith("FerramAerospaceResearch"))
                {
                    FARAssembly = assy.assembly;
                    hasFAR = true;
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FARUtils]: Found FAR Assembly: {FARAssembly.FullName}");
                }
            }
            return hasFAR;
        }

        public static bool CheckForFARWing()
        {
            if (!hasFAR) return false;
            if (hasCheckedForFARWing) return hasFARWing;
            hasCheckedForFARWing = true;
            foreach (var type in FARAssembly.GetTypes())
            {
                if (type.Name == "FARWingAerodynamicModel")
                {
                    FARWingModule = type;
                    hasFARWing = true;
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FARUtils]: Found FAR wing module type.");
                }
            }
            return hasFARWing;
        }

        public static bool CheckForFARControllableSurface()
        {
            if (!hasFAR) return false;
            if (hasCheckedForFARControllableSurface) return hasFARControllableSurface;
            hasCheckedForFARControllableSurface = true;
            foreach (var type in FARAssembly.GetTypes())
            {
                if (type.Name == "FARControllableSurface")
                {
                    FARControllableSurfaceModule = type;
                    hasFARControllableSurface = true;
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FARUtils]: Found FAR controllable surface module type.");
                }
            }
            return hasFARControllableSurface;
        }

        public static float GetFARMassMult(Part part)
        {
            if (!hasFARWing) return 1;

            foreach (var module in part.Modules)
            {
                if (module.GetType() == FARWingModule)
                {
                    var massMultiplier = (float)FARWingModule.GetField("massMultiplier", BindingFlags.Public | BindingFlags.Instance).GetValue(module);
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FARUtils]: Found wing Mass multiplier of {massMultiplier} for {part.name}.");
                    return massMultiplier;
                }
                if (module.GetType() == FARControllableSurfaceModule)
                {
                    var massMultiplier = (float)FARControllableSurfaceModule.GetField("massMultiplier", BindingFlags.Public | BindingFlags.Instance).GetValue(module);
                    if (BDArmorySettings.DEBUG_OTHER) Debug.Log($"[BDArmory.FARUtils]: Found ctrl. srf. Mass multiplier of {massMultiplier} for {part.name}.");
                    return massMultiplier;
                }
            }
            return 1;
        }
    }
}
