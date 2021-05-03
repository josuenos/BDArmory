using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using BDArmory.Control;
using BDArmory.Core;
using BDArmory.Core.Extension;
using BDArmory.Guidances;
using BDArmory.Misc;
using BDArmory.Targeting;
using BDArmory.UI;
using UnityEngine;

namespace BDArmory.Modules
{
    public class BDModulePilotAI : BDGenericAIBase, IBDAIControl
    {
        public enum SteerModes
        { NormalFlight, Aiming }

        SteerModes steerMode = SteerModes.NormalFlight;

        bool extending;
        double startedExtendingAt = 0;
        public string extendingReason = "";

        bool requestedExtend;
        Vector3 requestedExtendTpos;

        public bool IsExtending
        {
            get { return extending || requestedExtend; }
        }

        public void StopExtending()
        {
            extending = false;
            extendingReason = "";
            startedExtendingAt = 0;
            if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDModulePilotAI]: " + vessel.vesselName + " stopped extending due to request");
        }

        public void RequestExtend(Vector3 tPosition, Vessel target = null)
        {
            requestedExtend = true;
            requestedExtendTpos = tPosition;
            extendingReason = "Request";
            if (target != null)
                extendTarget = target;
        }

        public Vessel extendTarget = null;

        public override bool CanEngage()
        {
            return !vessel.LandedOrSplashed;
        }

        GameObject vobj;

        Transform velocityTransform
        {
            get
            {
                if (!vobj)
                {
                    vobj = new GameObject("velObject");
                    vobj.transform.position = vessel.ReferenceTransform.position;
                    vobj.transform.parent = vessel.ReferenceTransform;
                }

                return vobj.transform;
            }
        }

        Vector3 upDirection = Vector3.up;

        #region Pilot AI Settings GUI

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerFactor", //Steer Factor
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0.1f, maxValue = 20f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float steerMult = 6.6f;
        //make a combat steer mult and idle steer mult

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerKi", //Steer Ki
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float steerKiAdjust = 0.25f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true,
             guiName = "#LOC_BDArmory_Custom_SteerKi_Clamps", //Steer Ki
             groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
         UI_Toggle(scene = UI_Scene.All, enabledText = "#LOC_BDArmory_Enabled",
             disabledText = "#LOC_BDArmory_Disabled")]
        public bool customKiClamps = false;

        [KSPField(isPersistant = true,
             guiName = "#LOC_BDArmory_PitchKi_Clamp", //Steer Ki
             groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", advancedTweakable = true, groupStartCollapsed = true),
         UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float pitchKiClamp = 1f;

        [KSPField(isPersistant = true,
             guiName = "#LOC_BDArmory_YawKi_Clamp", //Steer Ki
             groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", advancedTweakable = true, groupStartCollapsed = true),
         UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float yawKiClamp = 0.55f;

        [KSPField(isPersistant = true,
             guiName = "#LOC_BDArmory_RollKi_Clamp", //Steer Ki
             groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", advancedTweakable = true, groupStartCollapsed = true),
         UI_FloatRange(minValue = 0.01f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float rollKiClamp = 0.45f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_SteerDamping", //Steer Damping
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float steerDamping = 2.5f;

        #region Dynamic Damping
        // Note: min/max is replaced by off-target/on-target in localisation, but the variable names are kept to avoid reconfiguring existing craft.
        // Dynamic Damping
        [KSPField(guiName = "#LOC_BDArmory_DynamicDamping", groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true), UI_Label(scene = UI_Scene.All)]
        private string DynamicDampingLabel = "";

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DynamicDampingMin", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingMin = 1.5f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DynamicDampingMax", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingMax = 4f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DynamicDampingFactor", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float dynamicSteerDampingFactor = 7f;

        // Dynamic Pitch
        [KSPField(guiName = "#LOC_BDArmory_DynamicDampingPitch", groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true), UI_Label(scene = UI_Scene.All)]
        private string PitchLabel = "";

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingPitch", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_Toggle(scene = UI_Scene.All, enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled")]
        public bool dynamicDampingPitch = true;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingPitchMin", advancedTweakable = true, //Dynamic steer damping Clamp min
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingPitchMin = 1.5f;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingPitchMax", advancedTweakable = true, //Dynamic steer damping Clamp max
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingPitchMax = 4f;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingPitchFactor", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float dynamicSteerDampingPitchFactor = 7f;

        // Dynamic Yaw
        [KSPField(guiName = "#LOC_BDArmory_DynamicDampingYaw", groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true), UI_Label(scene = UI_Scene.All)]
        private string YawLabel = "";

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingYaw", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_Toggle(scene = UI_Scene.All, enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled")]
        public bool dynamicDampingYaw = true;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingYawMin", advancedTweakable = true, //Dynamic steer damping Clamp min
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingYawMin = 1.5f;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingYawMax", advancedTweakable = true, //Dynamic steer damping Clamp max
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingYawMax = 4f;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingYawFactor", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float dynamicSteerDampingYawFactor = 7f;

        // Dynamic Roll
        [KSPField(guiName = "#LOC_BDArmory_DynamicDampingRoll", groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true), UI_Label(scene = UI_Scene.All)]
        private string RollLabel = "";

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingRoll", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_Toggle(scene = UI_Scene.All, enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled")]
        public bool dynamicDampingRoll = true;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingRollMin", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingRollMin = 1.5f;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingRollMax", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 8f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float DynamicDampingRollMax = 4f;

        [KSPField(isPersistant = true, guiName = "#LOC_BDArmory_DynamicDampingRollFactor", advancedTweakable = true, //Dynamic steer dampening Factor
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0.1f, maxValue = 10f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float dynamicSteerDampingRollFactor = 7f;

        //Toggle Dynamic Steer Damping
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DynamicSteerDamping", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_Toggle(scene = UI_Scene.All, disabledText = "#LOC_BDArmory_Disabled", enabledText = "#LOC_BDArmory_Enabled")]
        public bool dynamicSteerDamping = false;

        //Toggle 3-Axis Dynamic Steer Damping
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_3AxisDynamicSteerDamping", advancedTweakable = true,
            groupName = "pilotAI_PID", groupDisplayName = "#LOC_BDArmory_PilotAI_PID", groupStartCollapsed = true),
            UI_Toggle(enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled", scene = UI_Scene.All)]
        public bool CustomDynamicAxisFields = false;
        #endregion

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_DefaultAltitude", //Default Alt.
            groupName = "pilotAI_Altitudes", groupDisplayName = "#LOC_BDArmory_PilotAI_Altitudes", groupStartCollapsed = true),
            UI_FloatRange(minValue = 150f, maxValue = 15000f, stepIncrement = 25f, scene = UI_Scene.All)]
        public float defaultAltitude = 1500;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinAltitude", //Min Altitude
            groupName = "pilotAI_Altitudes", groupDisplayName = "#LOC_BDArmory_PilotAI_Altitudes", groupStartCollapsed = true),
            UI_FloatRange(minValue = 25f, maxValue = 6000, stepIncrement = 25f, scene = UI_Scene.All)]
        public float minAltitude = 500f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MaxSpeed", //Max Speed
            groupName = "pilotAI_Speeds", groupDisplayName = "#LOC_BDArmory_PilotAI_Speeds", groupStartCollapsed = true),
            UI_FloatRange(minValue = 20f, maxValue = 800f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float maxSpeed = 325;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_TakeOffSpeed", //TakeOff Speed
            groupName = "pilotAI_Speeds", groupDisplayName = "#LOC_BDArmory_PilotAI_Speeds", groupStartCollapsed = true),
            UI_FloatRange(minValue = 10f, maxValue = 200f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float takeOffSpeed = 70;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinSpeed", //MinCombatSpeed
            groupName = "pilotAI_Speeds", groupDisplayName = "#LOC_BDArmory_PilotAI_Speeds", groupStartCollapsed = true),
            UI_FloatRange(minValue = 10f, maxValue = 200, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float minSpeed = 60f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_StrafingSpeed", //Strafing Speed
            groupName = "pilotAI_Speeds", groupDisplayName = "#LOC_BDArmory_PilotAI_Speeds", groupStartCollapsed = true),
            UI_FloatRange(minValue = 10f, maxValue = 200, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float strafingSpeed = 120f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_IdleSpeed", //Idle Speed
            groupName = "pilotAI_Speeds", groupDisplayName = "#LOC_BDArmory_PilotAI_Speeds", groupStartCollapsed = true),
            UI_FloatRange(minValue = 10f, maxValue = 200f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float idleSpeed = 120f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_LowSpeedSteerLimiter", advancedTweakable = true, // Low-Speed Steer Limiter
            groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
            UI_FloatRange(minValue = .1f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.All)]
        public float maxSteer = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_LowSpeedLimiterSpeed", advancedTweakable = true, // Low-Speed Limiter Switch Speed 
            groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
            UI_FloatRange(minValue = 10f, maxValue = 500f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float lowSpeedSwitch = 100f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_HighSpeedSteerLimiter", advancedTweakable = true, // High-Speed Steer Limiter
            groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
            UI_FloatRange(minValue = .1f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.All)]
        public float maxSteerAtMaxSpeed = 1;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_HighSpeedLimiterSpeed", advancedTweakable = true, // High-Speed Limiter Switch Speed 
            groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
            UI_FloatRange(minValue = 10f, maxValue = 500f, stepIncrement = 1.0f, scene = UI_Scene.All)]
        public float cornerSpeed = 200f;

        //[KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AttitudeLimiter", advancedTweakable = true, //Attitude Limiter, not currently functional
        //    groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
        // UI_FloatRange(minValue = 10f, maxValue = 90f, stepIncrement = 5f, scene = UI_Scene.All)]
        //public float maxAttitude = 90f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_BankLimiter", advancedTweakable = true, //Bank Angle Limiter
            groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
            UI_FloatRange(minValue = 10f, maxValue = 180f, stepIncrement = 5f, scene = UI_Scene.All)]
        public float maxBank = 180f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_maxAllowedGForce", //Max G
            groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
            UI_FloatRange(minValue = 2f, maxValue = 45f, stepIncrement = 0.25f, scene = UI_Scene.All)]
        public float maxAllowedGForce = 10;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_maxAllowedAoA", //Max AoA
            groupName = "pilotAI_ControlLimits", groupDisplayName = "#LOC_BDArmory_PilotAI_ControlLimits", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 85f, stepIncrement = 2.5f, scene = UI_Scene.All)]
        public float maxAllowedAoA = 35;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_MinEvasionTime", advancedTweakable = true, // Minimum Evasion Time
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = .05f, scene = UI_Scene.All)]
        public float minEvasionTime = 0.2f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EvasionThreshold", advancedTweakable = true, //Evade Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 100f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float evasionThreshold = 50f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_EvasionTimeThreshold", advancedTweakable = true, // Time on Target Threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 1f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float evasionTimeThreshold = 0f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CollisionAvoidanceThreshold", advancedTweakable = true, //Vessel collision avoidance threshold
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 50f, stepIncrement = 1f, scene = UI_Scene.All)]
        float collisionAvoidanceThreshold = 30f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_CollisionAvoidancePeriod", advancedTweakable = true, //Vessel collision avoidance period
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 3f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        float vesselCollisionAvoidancePeriod = 1.5f; // Avoid for 1.5s.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ExtendMultiplier", advancedTweakable = true, //Extend Distance Multiplier
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 2f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float extendMult = 1f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ExtendTargetVel", advancedTweakable = true, //Extend Target Velocity Factor
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 2f, stepIncrement = .1f, scene = UI_Scene.All)]
        public float extendTargetVel = 0.8f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ExtendTargetAngle", advancedTweakable = true, //Extend Target Angle
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 180f, stepIncrement = 1f, scene = UI_Scene.All)]
        public float extendTargetAngle = 78f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ExtendTargetDist", advancedTweakable = true, //Extend Target Distance
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 5000f, stepIncrement = 25f, scene = UI_Scene.All)]
        public float extendTargetDist = 400f;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ExtendToggle", advancedTweakable = true,//Extend Toggle
            groupName = "pilotAI_EvadeExtend", groupDisplayName = "#LOC_BDArmory_PilotAI_EvadeExtend", groupStartCollapsed = true),
            UI_Toggle(enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled", scene = UI_Scene.All),]
        public bool canExtend = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, category = "DoubleSlider", guiName = "#LOC_BDArmory_TurnRadiusTwiddleFactorMin", advancedTweakable = true,//Turn radius twiddle factors (category seems to have no effect)
            groupName = "pilotAI_Terrain", groupDisplayName = "#LOC_BDArmory_PilotAI_Terrain", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float turnRadiusTwiddleFactorMin = 2.0f; // Minimum and maximum twiddle factors for the turn radius. Depends on roll rate and how the vessel behaves under fire.
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, category = "DoubleSlider", guiName = "#LOC_BDArmory_TurnRadiusTwiddleFactorMax", advancedTweakable = true,//Turn radius twiddle factors (category seems to have no effect)
            groupName = "pilotAI_Terrain", groupDisplayName = "#LOC_BDArmory_PilotAI_Terrain", groupStartCollapsed = true),
            UI_FloatRange(minValue = 1f, maxValue = 5f, stepIncrement = 0.1f, scene = UI_Scene.All)]
        public float turnRadiusTwiddleFactorMax = 4.0f; // Minimum and maximum twiddle factors for the turn radius. Depends on roll rate and how the vessel behaves under fire.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_AllowRamming", advancedTweakable = true, //Toggle Allow Ramming
            groupName = "pilotAI_Ramming", groupDisplayName = "#LOC_BDArmory_PilotAI_Ramming", groupStartCollapsed = true),
            UI_Toggle(enabledText = "#LOC_BDArmory_Enabled", disabledText = "#LOC_BDArmory_Disabled", scene = UI_Scene.All),]
        public bool allowRamming = true; // Allow switching to ramming mode.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_ControlSurfaceLag", advancedTweakable = true,//Control surface lag (for getting an accurate intercept for ramming).
            groupName = "pilotAI_Ramming", groupDisplayName = "#LOC_BDArmory_PilotAI_Ramming", groupStartCollapsed = true),
            UI_FloatRange(minValue = 0f, maxValue = 0.2f, stepIncrement = 0.01f, scene = UI_Scene.All)]
        public float controlSurfaceLag = 0.01f; // Lag time in response of control surfaces.

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_Orbit", advancedTweakable = true),//Orbit 
            UI_Toggle(enabledText = "#LOC_BDArmory_Orbit_enabledText", disabledText = "#LOC_BDArmory_Orbit_disabledText", scene = UI_Scene.All),]//Starboard (CW)--Port (CCW)
        public bool ClockwiseOrbit = true;

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_UnclampTuning", advancedTweakable = true),//Unclamp tuning 
            UI_Toggle(enabledText = "#LOC_BDArmory_UnclampTuning_enabledText", disabledText = "#LOC_BDArmory_UnclampTuning_disabledText", scene = UI_Scene.All),]//Unclamped--Clamped
        public bool UpToEleven = false;

        Dictionary<string, float> altMaxValues = new Dictionary<string, float>
        {
            { nameof(defaultAltitude), 100000f },
            { nameof(minAltitude), 60000f },
            { nameof(steerMult), 200f },
            { nameof(steerKiAdjust), 20f },
            { nameof(steerDamping), 100f },
            { nameof(maxSteer), 1f},
            { nameof(maxSpeed), 3000f },
            { nameof(takeOffSpeed), 2000f },
            { nameof(minSpeed), 2000f },
            { nameof(idleSpeed), 3000f },
            { nameof(maxAllowedGForce), 1000f },
            { nameof(maxAllowedAoA), 180f },
            { nameof(extendMult), 200f },
            { nameof(minEvasionTime), 10f },
            { nameof(evasionThreshold), 300f },
            { nameof(evasionTimeThreshold), 3f },
            { nameof(turnRadiusTwiddleFactorMin), 10f},
            { nameof(turnRadiusTwiddleFactorMax), 10f},
            { nameof(controlSurfaceLag), 1f},
            { nameof(DynamicDampingMin), 100f },
            { nameof(DynamicDampingMax), 100f },
            { nameof(dynamicSteerDampingFactor), 100f },
            { nameof(DynamicDampingPitchMin), 100f },
            { nameof(DynamicDampingPitchMax), 100f },
            { nameof(dynamicSteerDampingPitchFactor), 100f },
            { nameof(DynamicDampingYawMin), 100f },
            { nameof(DynamicDampingYawMax), 100f },
            { nameof(dynamicSteerDampingYawFactor), 100f },
            { nameof(DynamicDampingRollMin), 100f },
            { nameof(DynamicDampingRollMax), 100f },
            { nameof(dynamicSteerDampingRollFactor), 100f }

        };

        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_StandbyMode"),//Standby Mode
            UI_Toggle(enabledText = "#LOC_BDArmory_On", disabledText = "#LOC_BDArmory_Off")]//On--Off
        public bool standbyMode = false;

        private static Dictionary<string, List<System.Tuple<string, object>>> storedSettings; // Stored settings for each vessel.
        [KSPEvent(advancedTweakable = false, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_StoreSettings", active = true)]//Store Settings
        public void StoreSettings()
        {
            var vesselName = HighLogic.LoadedSceneIsFlight ? vessel.GetDisplayName() : EditorLogic.fetch.ship.shipName;
            if (storedSettings == null)
            {
                storedSettings = new Dictionary<string, List<System.Tuple<string, object>>>();
            }
            if (storedSettings.ContainsKey(vesselName))
            {
                if (storedSettings[vesselName] == null)
                {
                    storedSettings[vesselName] = new List<System.Tuple<string, object>>();
                }
                else
                {
                    storedSettings[vesselName].Clear();
                }
            }
            else
            {
                storedSettings.Add(vesselName, new List<System.Tuple<string, object>>());
            }
            var fields = typeof(BDModulePilotAI).GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                storedSettings[vesselName].Add(new System.Tuple<string, object>(field.Name, field.GetValue(this)));
            }
            Events["RestoreSettings"].active = true;
        }
        [KSPEvent(advancedTweakable = false, guiActive = true, guiActiveEditor = true, guiName = "#LOC_BDArmory_RestoreSettings", active = false)]//Restore Settings
        public void RestoreSettings()
        {
            var vesselName = HighLogic.LoadedSceneIsFlight ? vessel.GetDisplayName() : EditorLogic.fetch.ship.shipName;
            if (storedSettings == null || !storedSettings.ContainsKey(vesselName) || storedSettings[vesselName] == null || storedSettings[vesselName].Count == 0)
            {
                Debug.Log("[BDArmory.BDModulePilotAI]: No stored settings found for vessel " + vesselName + ".");
                return;
            }
            foreach (var setting in storedSettings[vesselName])
            {
                var field = typeof(BDModulePilotAI).GetField(setting.Item1, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(this, setting.Item2);
                }
            }
        }
        #endregion

        #region AI Internal Parameters
        bool toEleven = false;

        //manueuverability and g loading data
        // float maxDynPresGRecorded;
        float dynDynPresGRecorded = 1.0f; // Start at reasonable non-zero value.
        float dynMaxVelocityMagSqr = 1.0f; // Start at reasonable non-zero value.
        float dynDecayRate = 1.0f; // Decay rate for dynamic measurements. Set to a half-life of 60s in Start.

        float maxAllowedCosAoA;
        float lastAllowedAoA;

        float maxPosG;
        float cosAoAAtMaxPosG;

        float maxNegG;
        float cosAoAAtMaxNegG;

        float[] gLoadMovingAvgArray = new float[32];
        float[] cosAoAMovingAvgArray = new float[32];
        int movingAvgIndex;

        float gLoadMovingAvg;
        float cosAoAMovingAvg;

        float gaoASlopePerDynPres;        //used to limit control input at very high dynamic pressures to avoid structural failure
        float gOffsetPerDynPres;

        float posPitchDynPresLimitIntegrator = 1;
        float negPitchDynPresLimitIntegrator = -1;

        float lastCosAoA;
        float lastPitchInput;

        //Controller Integral
        float pitchIntegral;
        float yawIntegral;
        float rollIntegral;
        int lastPitchErrorSign;
        int lastYawErrorSign;
        int lastRollErrorSign;

        //instantaneous turn radius and possible acceleration from lift
        //properties can be used so that other AI modules can read this for future maneuverability comparisons between craft
        float turnRadius;
        float bodyGravity = (float)PhysicsGlobals.GravitationalAcceleration;

        public float TurnRadius
        {
            get { return turnRadius; }
            private set { turnRadius = value; }
        }

        float maxLiftAcceleration;

        public float MaxLiftAcceleration
        {
            get { return maxLiftAcceleration; }
            private set { maxLiftAcceleration = value; }
        }

        float turningTimer;
        float evasiveTimer;
        float threatRating;
        Vector3 lastTargetPosition;

        LineRenderer lr;
        Vector3 flyingToPosition;
        Vector3 rollTarget;
        Vector3 angVelRollTarget;

        //speed controller
        bool useAB = true;
        bool useBrakes = true;
        bool regainEnergy = false;

        //collision detection (for other vessels). Look ahead period is vesselCollisionAvoidancePeriod + vesselCollisionAvoidanceTickerFreq * Time.fixedDeltaTime
        int vesselCollisionAvoidanceTickerFreq = 10; // Number of fixedDeltaTime steps between vessel-vessel collision checks.
        int collisionDetectionTicker = 0;
        float collisionDetectionTimer = 0;
        Vector3 collisionAvoidDirection;

        // Terrain avoidance and below minimum altitude globals.
        int terrainAlertTicker = 0; // A ticker to reduce the frequency of terrain alert checks.
        bool belowMinAltitude; // True when below minAltitude or avoiding terrain.
        bool gainAltInhibited = false; // Inhibit gain altitude to minimum altitude when chasing or evading someone as long as we're pointing upwards.
        bool avoidingTerrain = false; // True when avoiding terrain.
        bool initialTakeOff = true; // False after the initial take-off.
        float terrainAlertDetectionRadius = 30.0f; // Sphere radius that the vessel occupies. Should cover most vessels. FIXME This could be based on the vessel's maximum width/height.
        float terrainAlertThreatRange; // The distance to the terrain to consider (based on turn radius).
        float terrainAlertThreshold; // The current threshold for triggering terrain avoidance based on various factors.
        float terrainAlertDistance; // Distance to the terrain (in the direction of the terrain normal).
        Vector3 terrainAlertNormal; // Approximate surface normal at the terrain intercept.
        Vector3 terrainAlertDirection; // Terrain slope in the direction of the velocity at the terrain intercept.
        Vector3 terrainAlertCorrectionDirection; // The direction to go to avoid the terrain.
        float terrainAlertCoolDown = 0; // Cool down period before allowing other special modes to take effect (currently just "orbitting").
        Vector3 relativeVelocityRightDirection; // Right relative to current velocity and upDirection.
        Vector3 relativeVelocityDownDirection; // Down relative to current velocity and upDirection.
        Vector3 terrainAlertDebugPos, terrainAlertDebugDir, terrainAlertDebugPos2, terrainAlertDebugDir2; // Debug vector3's for drawing lines.
        bool terrainAlertDebugDraw2 = false;

        // Ramming
        public bool ramming = false; // Whether or not we're currently trying to ram someone.

        //Dynamic Steer Damping
        private bool dynamicDamping = false;
        private bool CustomDynamicAxisField = false;
        public float dynSteerDampingValue;
        public float dynSteerDampingPitchValue;
        public float dynSteerDampingYawValue;
        public float dynSteerDampingRollValue;

        //custom ki clamp
        private bool customKiClampToggle;

        //wing command
        bool useRollHint;
        private Vector3d debugFollowPosition;

        double commandSpeed;
        Vector3d commandHeading;

        float finalMaxSteer = 1;

        string lastStatus = "Free";
        #endregion

        #region RMB info in editor

        // <color={XKCDColors.HexFormat.Lime}>Yes</color>
        public override string GetInfo()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<b>Available settings</b>:");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Default Alt.</color> - altitude to fly at when cruising/idle");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Min Altitude</color> - below this altitude AI will prioritize gaining altitude over combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Factor</color> - higher will make the AI apply more control input for the same desired rotation");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Ki</color> - higher will make the AI apply control trim faster");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Damping</color> - higher will make the AI apply more control input when it wants to stop rotation");
            if (GameSettings.ADVANCED_TWEAKABLES)
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Steer Limiter</color> - limit AI from applying full control input");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max Speed</color> - AI will not fly faster than this");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- TakeOff Speed</color> - speed at which to start pitching up when taking off");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- MinCombat Speed</color> - AI will prioritize regaining speed over combat below this");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Idle Speed</color> - Cruising speed when not in combat");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max G</color> - AI will try not to perform maneuvers at higher G than this");
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Max AoA</color> - AI will try not to exceed this angle of attack");
            if (GameSettings.ADVANCED_TWEAKABLES)
            {
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Extend Multiplier</color> - scale the time spent extending");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Evasion Multiplier</color> - scale the time spent evading");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Dynamic Steer Damping (min/max)</color> - Dynamically adjust the steer damping factor based on angle to target");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Dyn Steer Damping Factor</color> - Strength of dynamic steer damping adjustment");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Turn Radius Tuning (min/max)</color> - Compensating factor for not being able to perform the perfect turn when oriented correctly/incorrectly");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Control Surface Lag</color> - Lag time in response of control surfaces");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Orbit</color> - Which direction to orbit when idling over a location");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Extend Toggle</color> - Toggle extending multiplier behaviour");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Dynamic Steer Damping</color> - Toggle dynamic steer damping");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Allow Ramming</color> - Toggle ramming behaviour when out of guns/ammo");
                sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Unclamp tuning</color> - Increases variable limits, no direct effect on behaviour");
            }
            sb.AppendLine($"<color={XKCDColors.HexFormat.Cyan}>- Standby Mode</color> - AI will not take off until an enemy is detected");

            return sb.ToString();
        }

        #endregion RMB info in editor

        protected void SetSliderClamps(string fieldNameMin, string fieldNameMax)
        {
            // Enforce min <= max for pairs of sliders
            UI_FloatRange field = (UI_FloatRange)Fields[fieldNameMin].uiControlEditor;
            field.onFieldChanged = OnMinUpdated;
            field = (UI_FloatRange)Fields[fieldNameMin].uiControlFlight;
            field.onFieldChanged = OnMinUpdated;
            field = (UI_FloatRange)Fields[fieldNameMax].uiControlEditor;
            field.onFieldChanged = OnMaxUpdated;
            field = (UI_FloatRange)Fields[fieldNameMax].uiControlFlight;
            field.onFieldChanged = OnMaxUpdated;
        }
        public void OnMinUpdated(BaseField field, object obj)
        {
            if (turnRadiusTwiddleFactorMax < turnRadiusTwiddleFactorMin) { turnRadiusTwiddleFactorMax = turnRadiusTwiddleFactorMin; } // Enforce min < max for turn radius twiddle factor.
            // if (DynamicDampingMax < DynamicDampingMin) { DynamicDampingMax = DynamicDampingMin; } // Enforce min < max for dynamic steer damping.
            // if (DynamicDampingPitchMax < DynamicDampingPitchMin) { DynamicDampingPitchMax = DynamicDampingPitchMin; }
            // if (DynamicDampingYawMax < DynamicDampingYawMin) { DynamicDampingYawMax = DynamicDampingYawMin; }
            // if (DynamicDampingRollMax < DynamicDampingRollMin) { DynamicDampingRollMax = DynamicDampingRollMin; } // reversed roll dynamic damp behavior
        }

        public void OnMaxUpdated(BaseField field, object obj)
        {
            if (turnRadiusTwiddleFactorMin > turnRadiusTwiddleFactorMax) { turnRadiusTwiddleFactorMin = turnRadiusTwiddleFactorMax; } // Enforce min < max for turn radius twiddle factor.
            // if (DynamicDampingMin > DynamicDampingMax) { DynamicDampingMin = DynamicDampingMax; } // Enforce min < max for dynamic steer damping.
            // if (DynamicDampingPitchMin > DynamicDampingPitchMax) { DynamicDampingPitchMin = DynamicDampingPitchMax; }
            // if (DynamicDampingYawMin > DynamicDampingYawMax) { DynamicDampingYawMin = DynamicDampingYawMax; }
            // if (DynamicDampingRollMin > DynamicDampingRollMax) { DynamicDampingRollMin = DynamicDampingRollMax; } // reversed roll dynamic damp behavior
        }

        public void ToggleDynamicDampingFields()
        {
            // Dynamic damping
            var DynamicDampingLabel = Fields["DynamicDampingLabel"];
            var DampingMin = Fields["DynamicDampingMin"];
            var DampingMax = Fields["DynamicDampingMax"];
            var DampingFactor = Fields["dynamicSteerDampingFactor"];

            DynamicDampingLabel.guiActive = dynamicSteerDamping && !CustomDynamicAxisFields;
            DynamicDampingLabel.guiActiveEditor = dynamicSteerDamping && !CustomDynamicAxisFields;
            DampingMin.guiActive = dynamicSteerDamping && !CustomDynamicAxisFields;
            DampingMin.guiActiveEditor = dynamicSteerDamping && !CustomDynamicAxisFields;
            DampingMax.guiActive = dynamicSteerDamping && !CustomDynamicAxisFields;
            DampingMax.guiActiveEditor = dynamicSteerDamping && !CustomDynamicAxisFields;
            DampingFactor.guiActive = dynamicSteerDamping && !CustomDynamicAxisFields;
            DampingFactor.guiActiveEditor = dynamicSteerDamping && !CustomDynamicAxisFields;

            // 3-axis dynamic damping
            var DynamicPitchLabel = Fields["PitchLabel"];
            var DynamicDampingPitch = Fields["dynamicDampingPitch"];
            var DynamicDampingPitchMaxField = Fields["DynamicDampingPitchMax"];
            var DynamicDampingPitchMinField = Fields["DynamicDampingPitchMin"];
            var DynamicDampingPitchFactorField = Fields["dynamicSteerDampingPitchFactor"];

            var DynamicYawLabel = Fields["YawLabel"];
            var DynamicDampingYaw = Fields["dynamicDampingYaw"];
            var DynamicDampingYawMaxField = Fields["DynamicDampingYawMax"];
            var DynamicDampingYawMinField = Fields["DynamicDampingYawMin"];
            var DynamicDampingYawFactorField = Fields["dynamicSteerDampingYawFactor"];

            var DynamicRollLabel = Fields["RollLabel"];
            var DynamicDampingRoll = Fields["dynamicDampingRoll"];
            var DynamicDampingRollMaxField = Fields["DynamicDampingRollMax"];
            var DynamicDampingRollMinField = Fields["DynamicDampingRollMin"];
            var DynamicDampingRollFactorField = Fields["dynamicSteerDampingRollFactor"];

            DynamicPitchLabel.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicPitchLabel.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitch.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitch.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitchMinField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitchMinField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitchMaxField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitchMaxField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitchFactorField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingPitchFactorField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;

            DynamicYawLabel.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicYawLabel.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYaw.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYaw.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYawMinField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYawMinField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYawMaxField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYawMaxField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYawFactorField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingYawFactorField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;

            DynamicRollLabel.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicRollLabel.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRoll.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRoll.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRollMinField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRollMinField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRollMaxField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRollMaxField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRollFactorField.guiActive = CustomDynamicAxisFields && dynamicSteerDamping;
            DynamicDampingRollFactorField.guiActiveEditor = CustomDynamicAxisFields && dynamicSteerDamping;

            StartCoroutine(ToggleDynamicDampingButtons());
        }

        IEnumerator ToggleDynamicDampingButtons()
        {
            // Toggle the visibility of buttons, then re-enable them to avoid messing up the order in the GUI.
            var dynamicSteerDampingField = Fields["dynamicSteerDamping"];
            var customDynamicAxisField = Fields["CustomDynamicAxisFields"];
            dynamicSteerDampingField.guiActive = false;
            dynamicSteerDampingField.guiActiveEditor = false;
            customDynamicAxisField.guiActive = false;
            customDynamicAxisField.guiActiveEditor = false;
            yield return new WaitForFixedUpdate();
            dynamicSteerDampingField.guiActive = true;
            dynamicSteerDampingField.guiActiveEditor = true;
            customDynamicAxisField.guiActive = dynamicDamping;
            customDynamicAxisField.guiActiveEditor = dynamicDamping;
        }

        void ToggleKiClamps()
        {
            var pitch = Fields["pitchKiClamp"];
            var yaw = Fields["yawKiClamp"];
            var roll = Fields["rollKiClamp"];
            pitch.guiActive = customKiClamps;
            pitch.guiActiveEditor = customKiClamps;
            yaw.guiActive = customKiClamps;
            yaw.guiActiveEditor = customKiClamps;
            roll.guiActive = customKiClamps;
            roll.guiActiveEditor = customKiClamps;

            StartCoroutine(ToggleKiButtons());
        }

        IEnumerator ToggleKiButtons()
        {
            var toggle = Fields["customKiClamps"];
            toggle.guiActive = false;
            toggle.guiActiveEditor = false;
            yield return new WaitForFixedUpdate();
            toggle.guiActive = true;
            toggle.guiActiveEditor = true;
        }

        protected override void Start()
        {
            base.Start();

            if (HighLogic.LoadedSceneIsFlight)
            {
                maxAllowedCosAoA = (float)Math.Cos(maxAllowedAoA * Math.PI / 180.0);
                lastAllowedAoA = maxAllowedAoA;
                GameEvents.onVesselPartCountChanged.Add(UpdateTerrainAlertDetectionRadius);
                UpdateTerrainAlertDetectionRadius(vessel);
                dynDecayRate = Mathf.Exp(Mathf.Log(0.5f) * Time.fixedDeltaTime / 60f); // Decay rate for a half-life of 60s.
            }

            SetSliderClamps("turnRadiusTwiddleFactorMin", "turnRadiusTwiddleFactorMax");
            // SetSliderClamps("DynamicDampingMin", "DynamicDampingMax");
            // SetSliderClamps("DynamicDampingPitchMin", "DynamicDampingPitchMax");
            // SetSliderClamps("DynamicDampingYawMin", "DynamicDampingYawMax");
            // SetSliderClamps("DynamicDampingRollMin", "DynamicDampingRollMax");
            dynamicDamping = dynamicSteerDamping;
            CustomDynamicAxisField = CustomDynamicAxisFields;
            ToggleDynamicDampingFields();
            // InitSteerDamping();
            if ((HighLogic.LoadedSceneIsFlight || HighLogic.LoadedSceneIsEditor) && storedSettings != null && storedSettings.ContainsKey(HighLogic.LoadedSceneIsFlight ? vessel.GetDisplayName() : EditorLogic.fetch.ship.shipName))
            {
                Events["RestoreSettings"].active = true;
            }
        }

        protected override void OnDestroy()
        {
            GameEvents.onVesselPartCountChanged.Remove(UpdateTerrainAlertDetectionRadius);
            base.OnDestroy();
        }

        public override void ActivatePilot()
        {
            base.ActivatePilot();

            belowMinAltitude = vessel.LandedOrSplashed;
            prevTargetDir = vesselTransform.up;
            if (initialTakeOff && !vessel.LandedOrSplashed) // In case we activate pilot after taking off manually.
                initialTakeOff = false;

            bodyGravity = (float)PhysicsGlobals.GravitationalAcceleration * (float)vessel.orbit.referenceBody.GeeASL; // Set gravity for calculations;
        }

        void Update()
        {
            if (BDArmorySettings.DRAW_DEBUG_LINES && pilotEnabled)
            {
                if (lr)
                {
                    lr.enabled = true;
                    lr.SetPosition(0, vessel.ReferenceTransform.position);
                    lr.SetPosition(1, flyingToPosition);
                }
                else
                {
                    lr = gameObject.AddComponent<LineRenderer>();
                    lr.positionCount = 2;
                    lr.startWidth = 0.5f;
                    lr.endWidth = 0.5f;
                }

                minSpeed = Mathf.Clamp(minSpeed, 0, idleSpeed - 20);
                minSpeed = Mathf.Clamp(minSpeed, 0, maxSpeed - 20);
            }
            else
            {
                if (lr)
                {
                    lr.enabled = false;
                }
            }

            // switch up the alt values if up to eleven is toggled
            if (UpToEleven != toEleven)
            {
                using (var s = altMaxValues.Keys.ToList().GetEnumerator())
                    while (s.MoveNext())
                    {
                        UI_FloatRange euic = (UI_FloatRange)
                            (HighLogic.LoadedSceneIsFlight ? Fields[s.Current].uiControlFlight : Fields[s.Current].uiControlEditor);
                        float tempValue = euic.maxValue;
                        euic.maxValue = altMaxValues[s.Current];
                        altMaxValues[s.Current] = tempValue;
                        // change the value back to what it is now after fixed update, because changing the max value will clamp it down
                        // using reflection here, don't look at me like that, this does not run often
                        StartCoroutine(setVar(s.Current, (float)typeof(BDModulePilotAI).GetField(s.Current).GetValue(this)));
                    }
                toEleven = UpToEleven;
            }

            //hide dynamic steer damping fields if dynamic damping isn't toggled
            if (dynamicSteerDamping != dynamicDamping)
            {
                // InitSteerDamping();
                dynamicDamping = dynamicSteerDamping;
                ToggleDynamicDampingFields();
            }
            //hide custom dynamic axis fields when it isn't toggled
            if (CustomDynamicAxisFields != CustomDynamicAxisField)
            {
                CustomDynamicAxisField = CustomDynamicAxisFields;
                ToggleDynamicDampingFields();
            }

            //ki clamps
            if (customKiClampToggle != customKiClamps)
            {
                customKiClampToggle = customKiClamps;
                ToggleKiClamps();
            }
        }

        IEnumerator setVar(string name, float value)
        {
            yield return new WaitForFixedUpdate();
            typeof(BDModulePilotAI).GetField(name).SetValue(this, value);
        }

        void FixedUpdate()
        {
            //floating origin and velocity offloading corrections
            if (lastTargetPosition != null && (!FloatingOrigin.Offset.IsZero() || !Krakensbane.GetFrameVelocity().IsZero()))
            {
                lastTargetPosition -= FloatingOrigin.OffsetNonKrakensbane;
            }
        }

        // This is triggered every Time.fixedDeltaTime.
        protected override void AutoPilot(FlightCtrlState s)
        {
            finalMaxSteer = 1f; // Reset finalMaxSteer, is adjusted in subsequent methods

            if (terrainAlertCoolDown > 0)
                terrainAlertCoolDown -= Time.fixedDeltaTime;

            //default brakes off full throttle
            //s.mainThrottle = 1;

            //vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);
            AdjustThrottle(maxSpeed, true);
            useAB = true;
            useBrakes = true;
            vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);

            steerMode = SteerModes.NormalFlight;
            useVelRollTarget = false;

            // landed and still, chill out
            if (vessel.LandedOrSplashed && standbyMode && weaponManager && (BDATargetManager.GetClosestTarget(this.weaponManager) == null || BDArmorySettings.PEACE_MODE)) //TheDog: replaced querying of targetdatabase with actual check if a target can be detected
            {
                //s.mainThrottle = 0;
                //vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
                AdjustThrottle(0, true);
                return;
            }

            //upDirection = -FlightGlobals.getGeeForceAtPosition(transform.position).normalized;
            upDirection = VectorUtils.GetUpDirection(vessel.transform.position);

            CalculateAccelerationAndTurningCircle();

            if ((float)vessel.radarAltitude < minAltitude)
            { belowMinAltitude = true; }

            if (gainAltInhibited && (!belowMinAltitude || !(currentStatus == "Engaging" || currentStatus == "Evading" || currentStatus.StartsWith("Gain Alt"))))
            { // Allow switching between "Engaging", "Evading" and "Gain Alt." while below minimum altitude without disabling the gain altitude inhibitor.
                gainAltInhibited = false;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDModulePilotAI]: " + vessel.vesselName + " is no longer inhibiting gain alt");
            }

            if (!gainAltInhibited && belowMinAltitude && (currentStatus == "Engaging" || currentStatus == "Evading"))
            { // Vessel went below minimum altitude while "Engaging" or "Evading", enable the gain altitude inhibitor.
                gainAltInhibited = true;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDModulePilotAI]: " + vessel.vesselName + " was " + currentStatus + " and went below min altitude, inhibiting gain alt.");
            }

            if (vessel.srfSpeed < minSpeed)
            { regainEnergy = true; }
            else if (!belowMinAltitude && vessel.srfSpeed > Mathf.Min(minSpeed + 20f, idleSpeed))
            { regainEnergy = false; }


            UpdateVelocityRelativeDirections();
            CheckLandingGear();
            if (!vessel.LandedOrSplashed && (FlyAvoidTerrain(s) || (!ramming && FlyAvoidOthers(s))))
            { turningTimer = 0; }
            else if (belowMinAltitude && !(gainAltInhibited && Vector3.Dot(vessel.Velocity() / vessel.srfSpeed, vessel.upAxis) > 0)) // If we're below minimum altitude, gain altitude unless we're being inhibited and gaining altitude.
            {
                if (initialTakeOff || command != PilotCommands.Follow)
                {
                    TakeOff(s);
                    turningTimer = 0;
                }
            }
            else
            {
                if (command != PilotCommands.Free)
                { UpdateCommand(s); }
                else
                { UpdateAI(s); }
            }
            UpdateGAndAoALimits(s);
            AdjustPitchForGAndAoALimits(s);

            // Perform the check here since we're now allowing evading/engaging while below mininum altitude.
            if (belowMinAltitude && vessel.radarAltitude > minAltitude && Vector3.Dot(vessel.Velocity() / vessel.srfSpeed, vessel.upAxis) > 0) // We're good.
            {
                terrainAlertCoolDown = 1.0f; // 1s cool down after avoiding terrain or gaining altitude. (Only used for delaying "orbitting" for now.)
                belowMinAltitude = false;
            }

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                if (lastStatus != currentStatus && !(lastStatus.StartsWith("Gain Alt.") && currentStatus.StartsWith("Gain Alt.")) && !(lastStatus.StartsWith("Terrain") && currentStatus.StartsWith("Terrain")))
                {
                    Debug.Log("[BDArmory.BDModulePilotAI]: Status of " + vessel.vesselName + " changed from " + lastStatus + " to " + currentStatus);
                }
                lastStatus = currentStatus;
            }
        }

        void UpdateAI(FlightCtrlState s)
        {
            SetStatus("Free");

            if (requestedExtend)
            {
                requestedExtend = false;
                if (!extending) startedExtendingAt = Planetarium.GetUniversalTime();
                extending = true;
                lastTargetPosition = requestedExtendTpos;
            }

            // Calculate threat rating from any threats
            float minimumEvasionTime = minEvasionTime;
            threatRating = evasionThreshold + 1f; // Don't evade by default
            if (weaponManager && (weaponManager.ThreatClosingTime(weaponManager.incomingMissileVessel) <= weaponManager.cmThreshold))
            {
                threatRating = 0f; // Allow entering evasion code if we're under missile fire
                minimumEvasionTime = 0f; //  Trying to evade missile threats when they don't exist will result in NREs
            }
            else if (weaponManager.underFire && !ramming) // If we're ramming, ignore gunfire.
            {
                if (weaponManager.incomingMissTime >= evasionTimeThreshold) // If we haven't been under fire long enough, ignore gunfire
                    threatRating = weaponManager.incomingMissDistance;
            }

            debugString.AppendLine($"Threat Rating: {threatRating}");

            // If we're currently evading or a threat is significant and we're not ramming.
            if ((evasiveTimer < minimumEvasionTime && evasiveTimer != 0) || threatRating < evasionThreshold)
            {
                if (evasiveTimer < minimumEvasionTime)
                {
                    threatRelativePosition = vessel.Velocity().normalized + vesselTransform.right;

                    if (weaponManager)
                    {
                        if (weaponManager.rwr != null ? weaponManager.rwr.rwrEnabled : false) //use rwr to check missile threat direction
                        {
                            Vector3 missileThreat = Vector3.zero;
                            bool missileThreatDetected = false;
                            float closestMissileThreat = float.MaxValue;
                            for (int i = 0; i < weaponManager.rwr.pingsData.Length; i++)
                            {
                                TargetSignatureData threat = weaponManager.rwr.pingsData[i];
                                if (threat.exists && threat.signalStrength == 4)
                                {
                                    missileThreatDetected = true;
                                    float dist = (weaponManager.rwr.pingWorldPositions[i] - vesselTransform.position).sqrMagnitude;
                                    if (dist < closestMissileThreat)
                                    {
                                        closestMissileThreat = dist;
                                        missileThreat = weaponManager.rwr.pingWorldPositions[i];
                                    }
                                }
                            }
                            if (missileThreatDetected)
                            {
                                threatRelativePosition = missileThreat - vesselTransform.position;
                                if (extending)
                                    StopExtending(); // Don't keep trying to extend if under fire from missiles
                            }
                        }

                        if (weaponManager.underFire)
                        {
                            threatRelativePosition = weaponManager.incomingThreatPosition - vesselTransform.position;
                        }
                    }
                }
                Evasive(s);
                evasiveTimer += Time.fixedDeltaTime;
                turningTimer = 0;

                if (evasiveTimer >= minimumEvasionTime)
                {
                    evasiveTimer = 0;
                    collisionDetectionTicker = vesselCollisionAvoidanceTickerFreq + 1; //check for collision again after exiting evasion routine
                }
            }
            else if (!extending && weaponManager && targetVessel != null && targetVessel.transform != null)
            {
                evasiveTimer = 0;
                if (!targetVessel.LandedOrSplashed)
                {
                    Vector3 targetVesselRelPos = targetVessel.vesselTransform.position - vesselTransform.position;
                    if (canExtend && vessel.altitude < defaultAltitude && Vector3.Angle(targetVesselRelPos, -upDirection) < 35) // Target is at a steep angle below us and we're below default altitude, extend to get a better angle instead of attacking now.
                    {
                        //dangerous if low altitude and target is far below you - don't dive into ground!
                        if (!extending) startedExtendingAt = Planetarium.GetUniversalTime();
                        extending = true;
                        extendingReason = "Too steeply below";
                        lastTargetPosition = targetVessel.vesselTransform.position;
                        extendTarget = targetVessel;
                    }

                    if (Vector3.Angle(targetVessel.vesselTransform.position - vesselTransform.position, vesselTransform.up) > 35) // If target is outside of 35° cone ahead of us then keep flying straight.
                    {
                        turningTimer += Time.fixedDeltaTime;
                    }
                    else
                    {
                        turningTimer = 0;
                    }

                    debugString.AppendLine($"turningTimer: {turningTimer}");

                    float targetForwardDot = Vector3.Dot(targetVesselRelPos.normalized, vesselTransform.up); // Cosine of angle between us and target (1 if target is in front of us , -1 if target is behind us)
                    float targetVelFrac = (float)(targetVessel.srfSpeed / vessel.srfSpeed);      //this is the ratio of the target vessel's velocity to this vessel's srfSpeed in the forward direction; this allows smart decisions about when to break off the attack

                    float extendTargetDot = Mathf.Cos(extendTargetAngle * Mathf.Deg2Rad);
                    if (canExtend && targetVelFrac < extendTargetVel && targetForwardDot < extendTargetDot && targetVesselRelPos.magnitude < extendTargetDist) // Default values: Target is outside of ~78° cone ahead, closer than 400m and slower than us, so we won't be able to turn to attack it now.
                    {
                        if (!extending) startedExtendingAt = Planetarium.GetUniversalTime();
                        extending = true;
                        extendingReason = "Can't turn fast enough";
                        lastTargetPosition = targetVessel.vesselTransform.position - vessel.Velocity();       //we'll set our last target pos based on the enemy vessel and where we were 1 seconds ago
                        extendTarget = targetVessel;
                        weaponManager.ForceScan();
                    }
                    if (canExtend && turningTimer > 15)
                    {
                        //extend if turning circles for too long
                        RequestExtend(targetVessel.vesselTransform.position, targetVessel);
                        extendingReason = "Turning too long";
                        turningTimer = 0;
                        weaponManager.ForceScan();
                    }
                }
                else //extend if too close for an air-to-ground attack
                {
                    float extendDistance;
                    if (weaponManager.currentGun) // If using a gun, take the extend multiplier into account.
                        extendDistance = Mathf.Clamp(weaponManager.guardRange - 1800, 500, 4000) * extendMult; // General extending distance.
                    else
                        extendDistance = Mathf.Clamp(weaponManager.guardRange - 1800, 2500, 4000);
                    float srfDist = (GetSurfacePosition(targetVessel.transform.position) - GetSurfacePosition(vessel.transform.position)).sqrMagnitude;

                    if (srfDist < extendDistance * extendDistance && Vector3.Angle(vesselTransform.up, targetVessel.transform.position - vessel.transform.position) > 45)
                    {
                        if (!extending) startedExtendingAt = Planetarium.GetUniversalTime();
                        extending = true;
                        extendingReason = "Surface target";
                        lastTargetPosition = targetVessel.transform.position;
                        extendTarget = targetVessel;
                        weaponManager.ForceScan();
                    }
                }

                if (!extending)
                {
                    if (weaponManager.HasWeaponsAndAmmo() || !RamTarget(s, targetVessel)) // If we're out of ammo, see if we can ram someone, otherwise, behave as normal.
                    {
                        ramming = false;
                        SetStatus("Engaging");
                        debugString.AppendLine($"Flying to target " + targetVessel.vesselName);
                        FlyToTargetVessel(s, targetVessel);
                    }
                }
            }
            else
            {
                evasiveTimer = 0;
                if (!extending && !(terrainAlertCoolDown > 0))
                {
                    SetStatus("Orbiting");
                    FlyOrbit(s, assignedPositionGeo, 2000, idleSpeed, ClockwiseOrbit);
                }
            }

            if (extending)
            {
                evasiveTimer = 0;
                SetStatus("Extending");
                debugString.AppendLine($"Extending");
                FlyExtend(s, lastTargetPosition);
            }
        }

        bool PredictCollisionWithVessel(Vessel v, float maxTime, out Vector3 badDirection)
        {
            if (vessel == null || v == null || v == (weaponManager != null ? weaponManager.incomingMissileVessel : null)
                || v.rootPart.FindModuleImplementing<MissileBase>() != null) //evasive will handle avoiding missiles
            {
                badDirection = Vector3.zero;
                return false;
            }

            // Use the nearest time to closest point of approach to check separation instead of iteratively sampling. Should give faster, more accurate results.
            float timeToCPA = vessel.ClosestTimeToCPA(v, maxTime); // This uses the same kinematics as AIUtils.PredictPosition.
            if (timeToCPA > 0 && timeToCPA < maxTime)
            {
                Vector3 tPos = AIUtils.PredictPosition(v, timeToCPA);
                Vector3 myPos = AIUtils.PredictPosition(vessel, timeToCPA);
                if (Vector3.SqrMagnitude(tPos - myPos) < collisionAvoidanceThreshold * collisionAvoidanceThreshold) // Within collisionAvoidanceThreshold of each other. Danger Will Robinson!
                {
                    badDirection = tPos - vesselTransform.position;
                    return true;
                }
            }

            badDirection = Vector3.zero;
            return false;
        }

        bool RamTarget(FlightCtrlState s, Vessel v)
        {
            if (BDArmorySettings.DISABLE_RAMMING || !allowRamming) return false; // Override from BDArmory settings and local config.
            if (v == null) return false; // We don't have a target.
            if (Vector3.Dot(vessel.srf_vel_direction, v.srf_vel_direction) * (float)v.srfSpeed / (float)vessel.srfSpeed > 0.95f) return false; // We're not approaching them fast enough.
            Vector3 relVelocity = v.Velocity() - vessel.Velocity();
            Vector3 relPosition = v.transform.position - vessel.transform.position;
            Vector3 relAcceleration = v.acceleration - vessel.acceleration;
            float timeToCPA = vessel.ClosestTimeToCPA(v, 16f);

            // Let's try to ram someone!
            if (!ramming)
                ramming = true;
            SetStatus("Ramming speed!");

            // Ease in velocity from 16s to 8s, ease in acceleration from 8s to 2s using the logistic function to give smooth adjustments to target point.
            float easeAccel = Mathf.Clamp01(1.1f / (1f + Mathf.Exp((timeToCPA - 5f))) - 0.05f);
            float easeVel = Mathf.Clamp01(2f - timeToCPA / 8f);
            Vector3 predictedPosition = AIUtils.PredictPosition(v.transform.position, v.Velocity() * easeVel, v.acceleration * easeAccel, timeToCPA);

            // Set steer mode to aiming for less than 8s left
            if (timeToCPA < 8f)
                steerMode = SteerModes.Aiming;
            else
                steerMode = SteerModes.NormalFlight;

            if (controlSurfaceLag > 0)
                predictedPosition += -1 * controlSurfaceLag * controlSurfaceLag * (timeToCPA / controlSurfaceLag - 1f + Mathf.Exp(-timeToCPA / controlSurfaceLag)) * vessel.acceleration * easeAccel; // Compensation for control surface lag.
            FlyToPosition(s, predictedPosition);
            AdjustThrottle(maxSpeed, false, true); // Ramming speed!

            return true;
        }

        void FlyToTargetVessel(FlightCtrlState s, Vessel v)
        {
            Vector3 target = v.CoM;
            MissileBase missile = null;
            Vector3 vectorToTarget = v.transform.position - vesselTransform.position;
            float distanceToTarget = vectorToTarget.magnitude;
            float planarDistanceToTarget = Vector3.ProjectOnPlane(vectorToTarget, upDirection).magnitude;
            float angleToTarget = Vector3.Angle(target - vesselTransform.position, vesselTransform.up);
            float strafingDistance = -1f;
            float relativeVelocity = (float)(vessel.srf_velocity - v.srf_velocity).magnitude;
            if (weaponManager)
            {
                missile = weaponManager.CurrentMissile;
                if (missile != null)
                {
                    if (missile.GetWeaponClass() == WeaponClasses.Missile)
                    {
                        if (distanceToTarget > 5500f)
                        {
                            finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                        }

                        if (missile.TargetingMode == MissileBase.TargetingModes.Heat && !weaponManager.heatTarget.exists)
                        {
                            debugString.AppendLine($"Attempting heat lock");
                            target += v.srf_velocity.normalized * 10;
                        }
                        else
                        {
                            target = MissileGuidance.GetAirToAirFireSolution(missile, v);
                        }

                        if (angleToTarget < 20f)
                        {
                            steerMode = SteerModes.Aiming;
                        }
                    }
                    else //bombing
                    {
                        if (distanceToTarget > 4500f)
                        {
                            finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                        }

                        if (angleToTarget < 45f)
                        {
                            target = target + (Mathf.Max(defaultAltitude - 500f, minAltitude) * upDirection);
                            Vector3 tDir = (target - vesselTransform.position).normalized;
                            tDir = (1000 * tDir) - (vessel.Velocity().normalized * 600);
                            target = vesselTransform.position + tDir;
                        }
                        else
                        {
                            target = target + (Mathf.Max(defaultAltitude - 500f, minAltitude) * upDirection);
                        }
                    }
                }
                else if (weaponManager.currentGun)
                {
                    ModuleWeapon weapon = weaponManager.currentGun;
                    if (weapon != null)
                    {
                        Vector3 leadOffset = weapon.GetLeadOffset();

                        float targetAngVel = Vector3.Angle(v.transform.position - vessel.transform.position, v.transform.position + (vessel.Velocity()) - vessel.transform.position);
                        debugString.AppendLine($"targetAngVel: {targetAngVel}");
                        float magnifier = Mathf.Clamp(targetAngVel, 1f, 2f);
                        magnifier += ((magnifier - 1f) * Mathf.Sin(Time.time * 0.75f));
                        target -= magnifier * leadOffset;

                        angleToTarget = Vector3.Angle(vesselTransform.up, target - vesselTransform.position);
                        if (distanceToTarget < weaponManager.gunRange && angleToTarget < 20)
                        {
                            steerMode = SteerModes.Aiming; //steer to aim
                        }
                        else
                        {
                            if (distanceToTarget > 3500f || angleToTarget > 90f || vessel.srfSpeed < takeOffSpeed)
                            {
                                finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                            }
                            else
                            {
                                //figuring how much to lead the target's movement to get there after its movement assuming we can manage a constant speed turn
                                //this only runs if we're not aiming and not that far from the target and the target is in front of us
                                float curVesselMaxAccel = Math.Min(dynDynPresGRecorded * (float)vessel.dynamicPressurekPa, maxAllowedGForce * bodyGravity);
                                if (curVesselMaxAccel > 0)
                                {
                                    float timeToTurn = (float)vessel.srfSpeed * angleToTarget * Mathf.Deg2Rad / curVesselMaxAccel;
                                    target += v.Velocity() * timeToTurn;
                                    target += 0.5f * v.acceleration * timeToTurn * timeToTurn;
                                }
                            }
                        }

                        if (v.LandedOrSplashed)
                        {
                            if (distanceToTarget < weapon.maxTargetingRange + relativeVelocity) // Distance until starting to strafe plus 1s for changing speed.
                            {
                                strafingDistance = Mathf.Max(0f, distanceToTarget - weapon.maxTargetingRange);
                            }
                            if (distanceToTarget > defaultAltitude * 2.2f)
                            {
                                target = FlightPosition(target, defaultAltitude);
                            }
                            else
                            {
                                steerMode = SteerModes.Aiming;
                            }
                        }
                        else if (distanceToTarget > weaponManager.gunRange * 1.5f || Vector3.Dot(target - vesselTransform.position, vesselTransform.up) < 0)
                        {
                            target = v.CoM;
                        }
                    }
                }
                else if (planarDistanceToTarget > weaponManager.gunRange * 1.25f && (vessel.altitude < targetVessel.altitude || (float)vessel.radarAltitude < defaultAltitude)) //climb to target vessel's altitude if lower and still too far for guns
                {
                    finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                    target = vesselTransform.position + GetLimitedClimbDirectionForSpeed(vectorToTarget);
                }
                else
                {
                    finalMaxSteer = GetSteerLimiterForSpeedAndPower();
                }
            }

            float targetDot = Vector3.Dot(vesselTransform.up, v.transform.position - vessel.transform.position);

            //manage speed when close to enemy
            float finalMaxSpeed = maxSpeed;
            if (targetDot > 0f)
            {
                if (strafingDistance < 0f) // Beyond range of beginning strafing run.
                    finalMaxSpeed = Mathf.Max((distanceToTarget - 100f) / 8f, 0f) + (float)v.srfSpeed;
                else
                    finalMaxSpeed = strafingSpeed + (float)v.srfSpeed;
                finalMaxSpeed = Mathf.Max(finalMaxSpeed, minSpeed);
            }
            AdjustThrottle(finalMaxSpeed, true);

            if ((targetDot < 0 && vessel.srfSpeed > finalMaxSpeed)
                && distanceToTarget < 300 && vessel.srfSpeed < v.srfSpeed * 1.25f && Vector3.Dot(vessel.Velocity(), v.Velocity()) > 0) //distance is less than 800m
            {
                debugString.AppendLine($"Enemy on tail. Braking!");
                AdjustThrottle(minSpeed, true);
            }
            if (missile != null
                && targetDot > 0
                && distanceToTarget < MissileLaunchParams.GetDynamicLaunchParams(missile, v.Velocity(), v.transform.position).minLaunchRange
                && vessel.srfSpeed > idleSpeed)
            {
                RequestExtend(targetVessel.transform.position, targetVessel); // Get far enough away to use the missile.
                extendingReason = "Too close for missile";
            }

            if (regainEnergy && angleToTarget > 30f)
            {
                RegainEnergy(s, target - vesselTransform.position);
                return;
            }
            else
            {
                useVelRollTarget = true;
                FlyToPosition(s, target);
                return;
            }
        }

        void RegainEnergy(FlightCtrlState s, Vector3 direction, float throttleOverride = -1f)
        {
            debugString.AppendLine($"Regaining energy");

            steerMode = SteerModes.Aiming;
            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, upDirection);
            float angle = (Mathf.Clamp((float)vessel.radarAltitude - minAltitude, 0, 1500) / 1500) * 90;
            angle = Mathf.Clamp(angle, 0, 55) * Mathf.Deg2Rad;

            Vector3 targetDirection = Vector3.RotateTowards(planarDirection, -upDirection, angle, 0);
            targetDirection = Vector3.RotateTowards(vessel.Velocity(), targetDirection, 15f * Mathf.Deg2Rad, 0).normalized;

            if (throttleOverride >= 0)
                AdjustThrottle(maxSpeed, false, true, throttleOverride);
            else
                AdjustThrottle(maxSpeed, false, true);

            FlyToPosition(s, vesselTransform.position + (targetDirection * 100), true);
        }

        float GetSteerLimiterForSpeedAndPower()
        {
            float possibleAccel = speedController.GetPossibleAccel();
            float speed = (float)vessel.srfSpeed;

            debugString.AppendLine($"possibleAccel: {possibleAccel}");

            float limiter = ((speed - minSpeed) / 2 / minSpeed) + possibleAccel / 15f; // FIXME The calculation for possibleAccel needs further investigation.
            debugString.AppendLine($"unclamped limiter: { limiter}");

            return Mathf.Clamp01(limiter);
        }

        Vector3 prevTargetDir;
        Vector3 debugPos;
        bool useVelRollTarget;

        void FlyToPosition(FlightCtrlState s, Vector3 targetPosition, bool overrideThrottle = false)
        {
            if (!belowMinAltitude) // Includes avoidingTerrain
            {
                if (weaponManager && Time.time - weaponManager.timeBombReleased < 1.5f)
                {
                    targetPosition = vessel.transform.position + vessel.Velocity();
                }

                targetPosition = FlightPosition(targetPosition, minAltitude);
                targetPosition = vesselTransform.position + ((targetPosition - vesselTransform.position).normalized * 100);
            }

            Vector3d srfVel = vessel.Velocity();
            if (srfVel != Vector3d.zero)
            {
                velocityTransform.rotation = Quaternion.LookRotation(srfVel, -vesselTransform.forward);
            }
            velocityTransform.rotation = Quaternion.AngleAxis(90, velocityTransform.right) * velocityTransform.rotation;

            //ang vel
            Vector3 localAngVel = vessel.angularVelocity;
            //test
            Vector3 currTargetDir = (targetPosition - vesselTransform.position).normalized;
            if (steerMode == SteerModes.NormalFlight)
            {
                float gRotVel = ((10f * maxAllowedGForce) / ((float)vessel.srfSpeed));
                //currTargetDir = Vector3.RotateTowards(prevTargetDir, currTargetDir, gRotVel*Mathf.Deg2Rad, 0);
            }
            Vector3 targetAngVel = Vector3.Cross(prevTargetDir, currTargetDir) / Time.fixedDeltaTime;
            Vector3 localTargetAngVel = vesselTransform.InverseTransformVector(targetAngVel);
            prevTargetDir = currTargetDir;
            targetPosition = vessel.transform.position + (currTargetDir * 100);

            flyingToPosition = targetPosition;

            //test poststall
            float AoA = Vector3.Angle(vessel.ReferenceTransform.up, vessel.Velocity());
            if (AoA > 30f)
            {
                steerMode = SteerModes.Aiming;
            }

            //slow down for tighter turns
            float velAngleToTarget = Mathf.Clamp(Vector3.Angle(targetPosition - vesselTransform.position, vessel.Velocity()), 0, 90);
            float speedReductionFactor = 1.25f;
            float finalSpeed = Mathf.Min(speedController.targetSpeed, Mathf.Clamp(maxSpeed - (speedReductionFactor * velAngleToTarget), idleSpeed, maxSpeed));
            debugString.AppendLine($"Final Target Speed: {finalSpeed}");

            if (!overrideThrottle)
            {
                AdjustThrottle(finalSpeed, useBrakes, useAB);
            }

            if (steerMode == SteerModes.Aiming)
            {
                localAngVel -= localTargetAngVel;
            }

            Vector3 targetDirection;
            Vector3 targetDirectionYaw;
            float yawError;
            float pitchError;
            //float postYawFactor;
            //float postPitchFactor;
            if (steerMode == SteerModes.NormalFlight)
            {
                targetDirection = velocityTransform.InverseTransformDirection(targetPosition - velocityTransform.position).normalized;
                targetDirection = Vector3.RotateTowards(Vector3.up, targetDirection, 45 * Mathf.Deg2Rad, 0);

                targetDirectionYaw = vesselTransform.InverseTransformDirection(vessel.Velocity()).normalized;
                targetDirectionYaw = Vector3.RotateTowards(Vector3.up, targetDirectionYaw, 45 * Mathf.Deg2Rad, 0);
            }
            else//(steerMode == SteerModes.Aiming)
            {
                targetDirection = vesselTransform.InverseTransformDirection(targetPosition - vesselTransform.position).normalized;
                targetDirection = Vector3.RotateTowards(Vector3.up, targetDirection, 25 * Mathf.Deg2Rad, 0);
                targetDirectionYaw = targetDirection;
            }
            debugPos = vessel.transform.position + (targetPosition - vesselTransform.position) * 5000;

            //// Adjust targetDirection based on ATTITUDE limits
            //var horizonUp = Vector3.ProjectOnPlane(vesselTransform.up, upDirection).normalized;
            //var horizonRight = -Vector3.Cross(horizonUp, upDirection);
            //float attitude = Vector3.SignedAngle(horizonUp, vesselTransform.up, horizonRight);
            //if ((Mathf.Abs(attitude) > maxAttitude) && (maxAttitude != 90f))
            //{
            //    var projectPlane = Vector3.RotateTowards(upDirection, horizonUp, attitude * Mathf.PI / 180f, 0f);
            //    targetDirection = Vector3.ProjectOnPlane(targetDirection, projectPlane);
            //}
            //debugString.AppendLine($"Attitude: " + attitude);

            pitchError = VectorUtils.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(targetDirection, Vector3.right), Vector3.back);
            yawError = VectorUtils.SignedAngle(Vector3.up, Vector3.ProjectOnPlane(targetDirectionYaw, Vector3.forward), Vector3.right);

            // User-set steer limits
            if (maxSteer > maxSteerAtMaxSpeed)
                finalMaxSteer *= Mathf.Clamp((maxSteerAtMaxSpeed - maxSteer) / (cornerSpeed - lowSpeedSwitch + 0.001f) * ((float)vessel.srfSpeed - lowSpeedSwitch) + maxSteer, maxSteerAtMaxSpeed, maxSteer); // Linearly varies between two limits, clamped at limit values
            else
                finalMaxSteer *= Mathf.Clamp((maxSteerAtMaxSpeed - maxSteer) / (cornerSpeed - lowSpeedSwitch + 0.001f) * ((float)vessel.srfSpeed - lowSpeedSwitch) + maxSteer, maxSteer, maxSteerAtMaxSpeed); // Linearly varies between two limits, clamped at limit values
            finalMaxSteer = Mathf.Max(finalMaxSteer, 0.1f); // added just in case to ensure some input is retained no matter what happens
            debugString.AppendLine($"finalMaxSteer: {finalMaxSteer}");

            //roll
            Vector3 currentRoll = -vesselTransform.forward;
            float rollUp = (steerMode == SteerModes.Aiming ? 5f : 10f);
            if (steerMode == SteerModes.NormalFlight)
            {
                rollUp += (1 - finalMaxSteer) * 10f;
            }
            rollTarget = (targetPosition + (rollUp * upDirection)) - vesselTransform.position;

            //test
            if (steerMode == SteerModes.Aiming && !belowMinAltitude)
            {
                angVelRollTarget = -140 * vesselTransform.TransformVector(Quaternion.AngleAxis(90f, Vector3.up) * localTargetAngVel);
                rollTarget += angVelRollTarget;
            }

            if (command == PilotCommands.Follow && useRollHint)
            {
                rollTarget = -commandLeader.vessel.ReferenceTransform.forward;
            }

            //
            if (belowMinAltitude)
            {
                if (avoidingTerrain)
                    rollTarget = terrainAlertNormal * 100;
                else
                    rollTarget = vessel.upAxis * 100;
            }
            if (useVelRollTarget && !belowMinAltitude)
            {
                rollTarget = Vector3.ProjectOnPlane(rollTarget, vessel.Velocity());
                currentRoll = Vector3.ProjectOnPlane(currentRoll, vessel.Velocity());
            }
            else
            {
                rollTarget = Vector3.ProjectOnPlane(rollTarget, vesselTransform.up);
            }

            //ramming
            if (ramming)
                rollTarget = Vector3.ProjectOnPlane(targetPosition - vesselTransform.position + rollUp * Mathf.Clamp((targetPosition - vesselTransform.position).magnitude / 500f, 0f, 1f) * upDirection, vesselTransform.up);

            // Limit Bank Angle, this should probably be re-worked using quaternions or something like that, SignedAngle doesn't work well for angles > 90
            Vector3 horizonNormal = Vector3.ProjectOnPlane(vessel.transform.position - vessel.mainBody.transform.position, vesselTransform.up);
            float bankAngle = Vector3.SignedAngle(horizonNormal, rollTarget, vesselTransform.up);

            // FlightGlobals.ActiveVessel.mainBody.transform.position - this.vessel.transform.position;
            if ((Mathf.Abs(bankAngle) > maxBank) && maxBank != 180)
                rollTarget = Vector3.RotateTowards(horizonNormal, rollTarget, maxBank / 180 * Mathf.PI, 0.0f);

            bankAngle = Vector3.SignedAngle(horizonNormal, rollTarget, vesselTransform.up);
            // debugString.AppendLine($"Bank Angle: " + bankAngle);

            //v/q
            float dynamicAdjustment = Mathf.Clamp(16 * (float)(vessel.srfSpeed / vessel.dynamicPressurekPa), 0, 1.2f);

            float rollError = Misc.Misc.SignedAngle(currentRoll, rollTarget, vesselTransform.right);
            float steerRoll = (steerMult * 0.0015f * rollError);
            float rollDamping = (.10f * SteerDamping(Mathf.Abs(rollError), Vector3.Angle(targetPosition - vesselTransform.position, vesselTransform.up), 3) * -localAngVel.y);
            steerRoll -= rollDamping;
            steerRoll *= dynamicAdjustment;

            if (steerMode == SteerModes.NormalFlight)
            {
                //premature dive fix
                pitchError *= Mathf.Clamp01((21 - Mathf.Exp(Mathf.Abs(rollError) / 30)) / 20);
            }

            float steerPitch = (0.015f * steerMult * pitchError) - (SteerDamping(Mathf.Abs(Vector3.Angle(targetPosition - vesselTransform.position, vesselTransform.up)), Vector3.Angle(targetPosition - vesselTransform.position, vesselTransform.up), 1) * -localAngVel.x);
            float steerYaw = (0.005f * steerMult * yawError) - (SteerDamping(Mathf.Abs(yawError * (steerMode == SteerModes.Aiming ? (180f / 25f) : 4f)), Vector3.Angle(targetPosition - vesselTransform.position, vesselTransform.up), 2) * 0.2f * -localAngVel.z);

            pitchIntegral += pitchError * Time.deltaTime / 90f;
            yawIntegral += yawError * Time.deltaTime / 90f;
            rollIntegral += rollError * Time.deltaTime / 90f;

            pitchIntegral = lastPitchErrorSign != Math.Sign(pitchError) ? 0 : pitchIntegral;
            yawIntegral = lastYawErrorSign != Math.Sign(yawError) ? 0 : yawIntegral;
            rollIntegral = lastRollErrorSign != Math.Sign(rollError) ? 0 : rollIntegral;

            lastPitchErrorSign = Math.Sign(pitchError);
            lastYawErrorSign = Math.Sign(yawError);
            lastRollErrorSign = Math.Sign(rollError);

            steerPitch *= dynamicAdjustment;
            steerYaw *= dynamicAdjustment;

            float pitchKi = steerKiAdjust; //This is what should be allowed to be tweaked by the player, just like the steerMult, it is very low right now
            pitchIntegral = Mathf.Clamp(pitchIntegral, -pitchKiClamp, pitchKiClamp); //0.2f is the limit of the integral variable, making it bigger increases overshoot
            steerPitch += pitchIntegral * pitchKi; //Adds the integral component to the mix

            float yawKi = steerKiAdjust;
            yawIntegral = Mathf.Clamp(yawIntegral, -yawKiClamp, yawKiClamp);
            steerYaw += yawIntegral * yawKi;

            float rollKi = steerKiAdjust;
            rollIntegral = Mathf.Clamp(rollIntegral, -rollKiClamp, rollKiClamp);
            steerRoll += rollIntegral * rollKi;

            s.roll = Mathf.Clamp(steerRoll, -maxSteer, maxSteer);
            s.yaw = Mathf.Clamp(steerYaw, -finalMaxSteer, finalMaxSteer);
            s.pitch = Mathf.Clamp(steerPitch, Mathf.Min(-finalMaxSteer, -0.2f), finalMaxSteer);

            if (BDArmorySettings.DRAW_DEBUG_LABELS)
            {
                Debug.Log("[BDArmory.BDModulePilotAI]: PitchIntegral: " + pitchIntegral);
                Debug.Log("[BDArmory.BDModulePilotAI]: YawIntegral: " + yawIntegral);
                Debug.Log("[BDArmory.BDModulePilotAI]: RollIntegral: " + rollIntegral);
                Debug.Log("[BDArmory.BDModulePilotAI]: SteerPitch: " + steerPitch);
                Debug.Log("[BDArmory.BDModulePilotAI]: SteerYaw: " + steerYaw);
                Debug.Log("[BDArmory.BDModulePilotAI]: SteerRoll: " + steerRoll);
            }
        }

        void FlyExtend(FlightCtrlState s, Vector3 tPosition)
        {
            if (weaponManager)
            {
                if (weaponManager.TargetOverride)
                {
                    extending = false;
                    extendingReason = "";
                    startedExtendingAt = 0;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDModulePilotAI]: " + vessel.vesselName + " stopped extending due to target override");
                }

                float extendDistance = Mathf.Clamp(weaponManager.guardRange - 1800, 500, 4000) * extendMult; // General extending distance.
                float desiredMinAltitude = (float)vessel.radarAltitude + (defaultAltitude - (float)vessel.radarAltitude) * extendMult; // Desired minimum altitude after extending.

                if (weaponManager.CurrentMissile && weaponManager.CurrentMissile.GetWeaponClass() == WeaponClasses.Bomb) // Run away from the bomb!
                {
                    extendDistance = 4500;
                    desiredMinAltitude = defaultAltitude;
                }

                if (targetVessel != null && !targetVessel.LandedOrSplashed) // We have a flying target, only extend a short distance and don't climb.
                {
                    extendDistance = 300 * extendMult; // The effect of this is generally to extend for only 1 frame.
                    desiredMinAltitude = minAltitude;
                }
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDModulePilotAI]: " + vessel.vesselName + " is extending for " + (Planetarium.GetUniversalTime() - startedExtendingAt) + "s due to \"" + extendingReason + "\" for distance " + extendDistance + ", expected time " + (extendDistance / vessel.srfSpeed) + "s");

                Vector3 srfVector = Vector3.ProjectOnPlane(vessel.transform.position - tPosition, upDirection);
                float srfDist = srfVector.magnitude;
                if (srfDist < extendDistance) // Extend from position is closer (horizontally) than the extend distance.
                {
                    Vector3 targetDirection = srfVector.normalized * extendDistance;
                    Vector3 target = vessel.transform.position + targetDirection; // Target extend position horizontally.
                    target = GetTerrainSurfacePosition(target) + (vessel.upAxis * Mathf.Min(defaultAltitude, MissileGuidance.GetRaycastRadarAltitude(vesselTransform.position))); // Adjust for terrain changes at target extend position.
                    target = FlightPosition(target, desiredMinAltitude); // Further adjustments for speed, situation, etc. and desired minimum altitude after extending.
                    if (regainEnergy)
                    {
                        RegainEnergy(s, target - vesselTransform.position);
                        return;
                    }
                    else
                    {
                        FlyToPosition(s, target);
                    }
                }
                else // We're far enough away, stop extending.
                {
                    extending = false;
                    extendingReason = "";
                    startedExtendingAt = 0;
                    if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDModulePilotAI]: "+vessel.vesselName+" stopped extending due to gone far enough (" + srfDist + " of " + extendDistance + ")");
                }
            }
            else // No weapon manager.
            {
                extending = false;
                extendingReason = "";
                startedExtendingAt = 0;
                if (BDArmorySettings.DRAW_DEBUG_LABELS) Debug.Log("[BDArmory.BDModulePilotAI]: "+vessel.vesselName+" stopped extending due to no weapon manager");
            }
        }

        void FlyOrbit(FlightCtrlState s, Vector3d centerGPS, float radius, float speed, bool clockwise)
        {
            if (regainEnergy)
            {
                RegainEnergy(s, vessel.Velocity());
                return;
            }

            finalMaxSteer = GetSteerLimiterForSpeedAndPower();

            debugString.AppendLine($"Flying orbit");
            Vector3 flightCenter = GetTerrainSurfacePosition(VectorUtils.GetWorldSurfacePostion(centerGPS, vessel.mainBody)) + (defaultAltitude * upDirection);

            Vector3 myVectorFromCenter = Vector3.ProjectOnPlane(vessel.transform.position - flightCenter, upDirection);
            Vector3 myVectorOnOrbit = myVectorFromCenter.normalized * radius;

            Vector3 targetVectorFromCenter = Quaternion.AngleAxis(clockwise ? 15f : -15f, upDirection) * myVectorOnOrbit;

            Vector3 verticalVelVector = Vector3.Project(vessel.Velocity(), upDirection); //for vv damping

            Vector3 targetPosition = flightCenter + targetVectorFromCenter - (verticalVelVector * 0.25f);

            Vector3 vectorToTarget = targetPosition - vesselTransform.position;
            //Vector3 planarVel = Vector3.ProjectOnPlane(vessel.Velocity(), upDirection);
            //vectorToTarget = Vector3.RotateTowards(planarVel, vectorToTarget, 25f * Mathf.Deg2Rad, 0);
            vectorToTarget = GetLimitedClimbDirectionForSpeed(vectorToTarget);
            targetPosition = vesselTransform.position + vectorToTarget;

            if (command != PilotCommands.Free && (vessel.transform.position - flightCenter).sqrMagnitude < radius * radius * 1.5f)
            {
                Debug.Log("[BDArmory.BDModulePilotAI]: AI Pilot reached command destination.");
                command = PilotCommands.Free;
            }

            useVelRollTarget = true;

            AdjustThrottle(speed, false);
            FlyToPosition(s, targetPosition);
        }

        //sends target speed to speedController
        void AdjustThrottle(float targetSpeed, bool useBrakes, bool allowAfterburner = true, float throttleOverride = -1f)
        {
            speedController.targetSpeed = targetSpeed;
            speedController.useBrakes = useBrakes;
            speedController.allowAfterburner = allowAfterburner;
            speedController.throttleOverride = throttleOverride;
        }

        Vector3 threatRelativePosition;

        void Evasive(FlightCtrlState s)
        {
            if (s == null) return;
            if (vessel == null) return;
            if (weaponManager == null) return;

            SetStatus("Evading");
            debugString.AppendLine($"Evasive");
            debugString.AppendLine($"Threat Distance: {weaponManager.incomingMissileDistance}");

            bool hasABEngines = (speedController.multiModeEngines.Count > 0);

            collisionDetectionTicker += 2;

            if (weaponManager)
            {
                if (weaponManager.isFlaring)
                {
                    useAB = vessel.srfSpeed < minSpeed;
                    useBrakes = false;
                    float targetSpeed = minSpeed;
                    if (weaponManager.isChaffing)
                        targetSpeed = maxSpeed;
                    AdjustThrottle(targetSpeed, false, useAB);
                }

                if ((weaponManager.isChaffing || weaponManager.isFlaring) && weaponManager.incomingMissileVessel != null) // Missile evasion
                {
                    if ((weaponManager.ThreatClosingTime(weaponManager.incomingMissileVessel) <= 1.5f) && (!weaponManager.isChaffing)) // Missile is about to impact, pull a hard turn
                    {
                        debugString.AppendLine($"Missile about to impact! pull away!");

                        AdjustThrottle(maxSpeed, false, !weaponManager.isFlaring);

                        Vector3 cross = Vector3.Cross(weaponManager.incomingMissileVessel.transform.position - vesselTransform.position, vessel.Velocity()).normalized;
                        if (Vector3.Dot(cross, -vesselTransform.forward) < 0)
                        {
                            cross = -cross;
                        }
                        FlyToPosition(s, vesselTransform.position + (50 * vessel.Velocity() / vessel.srfSpeed) + (100 * cross));
                        return;
                    }
                    else // Fly at 90 deg to missile to put max distance between ourselves and dispensed flares/chaff
                    {
                        debugString.AppendLine($"Breaking from missile threat!");

                        // Break off at 90 deg to missile
                        Vector3 threatDirection = weaponManager.incomingMissileVessel.transform.position - vesselTransform.position;
                        threatDirection = Vector3.ProjectOnPlane(threatDirection, upDirection);
                        float sign = Vector3.SignedAngle(threatDirection, Vector3.ProjectOnPlane(vessel.Velocity(), upDirection), upDirection);
                        Vector3 breakDirection = Vector3.ProjectOnPlane(Vector3.Cross(Mathf.Sign(sign) * upDirection, threatDirection), upDirection);

                        // Dive to gain energy and hopefully lead missile into ground
                        float angle = (Mathf.Clamp((float)vessel.radarAltitude - minAltitude, 0, 1500) / 1500) * 90;
                        angle = Mathf.Clamp(angle, 0, 75) * Mathf.Deg2Rad;
                        Vector3 targetDirection = Vector3.RotateTowards(breakDirection, -upDirection, angle, 0);
                        targetDirection = Vector3.RotateTowards(vessel.Velocity(), targetDirection, 15f * Mathf.Deg2Rad, 0).normalized;

                        steerMode = SteerModes.Aiming;

                        if (weaponManager.isFlaring)
                            if (!hasABEngines)
                                AdjustThrottle(maxSpeed, false, useAB, 0.66f);
                            else
                                AdjustThrottle(maxSpeed, false, useAB);
                        else
                        {
                            useAB = true;
                            AdjustThrottle(maxSpeed, false, useAB);
                        }

                        FlyToPosition(s, vesselTransform.position + (targetDirection * 100), true);
                        return;
                    }
                }
                else if (weaponManager.underFire)
                {
                    debugString.Append($"Dodging gunfire");
                    float threatDirectionFactor = Vector3.Dot(vesselTransform.up, threatRelativePosition.normalized);
                    //Vector3 axis = -Vector3.Cross(vesselTransform.up, threatRelativePosition);

                    Vector3 breakTarget = threatRelativePosition * 2f;       //for the most part, we want to turn _towards_ the threat in order to increase the rel ang vel and get under its guns

                    if (threatDirectionFactor > 0.9f)     //within 28 degrees in front
                    { // This adds +-500/(threat distance) to the left or right relative to the breakTarget vector, regardless of the size of breakTarget
                        breakTarget += 500f / threatRelativePosition.magnitude * Vector3.Cross(threatRelativePosition.normalized, Mathf.Sign(Mathf.Sin((float)vessel.missionTime / 2)) * vessel.upAxis);
                        debugString.AppendLine($" from directly ahead!");
                    }
                    else if (threatDirectionFactor < -0.9) //within ~28 degrees behind
                    {
                        float threatDistanceSqr = threatRelativePosition.sqrMagnitude;
                        if (threatDistanceSqr > 400 * 400)
                        { // This sets breakTarget 1500m ahead and 500m down, then adds a 1000m offset at 90° to ahead based on missionTime. If the target is kinda close, brakes are also applied.
                            breakTarget = vesselTransform.position + vesselTransform.up * 1500 - 500 * vessel.upAxis;
                            breakTarget += Mathf.Sin((float)vessel.missionTime / 2) * vesselTransform.right * 1000 - Mathf.Cos((float)vessel.missionTime / 2) * vesselTransform.forward * 1000;
                            if (threatDistanceSqr > 800 * 800)
                                debugString.AppendLine($" from behind afar; engaging barrel roll");
                            else
                            {
                                debugString.AppendLine($" from behind moderate distance; engaging aggressvie barrel roll and braking");
                                steerMode = SteerModes.Aiming;
                                AdjustThrottle(minSpeed, true, false);
                            }
                        }
                        else
                        { // This sets breakTarget to the attackers position, then applies an up to 500m offset to the right or left (relative to the vessel) for the first half of the default evading period, then sets the breakTarget to be 150m right or left of the attacker.
                            breakTarget = threatRelativePosition;
                            if (evasiveTimer < 1.5f)
                                breakTarget += Mathf.Sin((float)vessel.missionTime * 2) * vesselTransform.right * 500;
                            else
                                breakTarget += -Math.Sign(Mathf.Sin((float)vessel.missionTime * 2)) * vesselTransform.right * 150;

                            debugString.AppendLine($" from directly behind and close; breaking hard");
                            steerMode = SteerModes.Aiming;
                            AdjustThrottle(minSpeed, true, false); // Brake to slow down and turn faster while breaking target
                        }
                    }
                    else
                    {
                        float threatDistanceSqr = threatRelativePosition.sqrMagnitude;
                        if (threatDistanceSqr < 400 * 400) // Within 400m to the side.
                        { // This sets breakTarget to be behind the attacker (relative to the evader) with a small offset to the left or right.
                            breakTarget += Mathf.Sin((float)vessel.missionTime * 2) * vesselTransform.right * 100;

                            steerMode = SteerModes.Aiming;
                            debugString.AppendLine($" from near side; turning towards attacker");
                        }
                        else // More than 400m to the side.
                        { // This sets breakTarget to be 1500m ahead, then adds a 1000m offset at 90° to ahead.
                            breakTarget = vesselTransform.position + vesselTransform.up * 1500;
                            breakTarget += Mathf.Sin((float)vessel.missionTime / 2) * vesselTransform.right * 1000 - Mathf.Cos((float)vessel.missionTime / 2) * vesselTransform.forward * 1000;
                            debugString.AppendLine($" from far side; engaging barrel roll");
                        }
                    }

                    float threatAltitudeDiff = Vector3.Dot(threatRelativePosition, vessel.upAxis);
                    if (threatAltitudeDiff > 500)
                        breakTarget += threatAltitudeDiff * vessel.upAxis;      //if it's trying to spike us from below, don't go crazy trying to dive below it
                    else
                        breakTarget += -150 * vessel.upAxis;   //dive a bit to escape

                    float breakTargetVerticalComponent = Vector3.Dot(breakTarget - vessel.transform.position, upDirection);
                    if (belowMinAltitude && breakTargetVerticalComponent < 0) // If we're below minimum altitude, enforce the evade direction to gain altitude.
                    {
                        breakTarget += -2f * breakTargetVerticalComponent * upDirection;
                    }

                    FlyToPosition(s, breakTarget);
                    return;
                }
            }

            Vector3 target = (vessel.srfSpeed < 200) ? FlightPosition(vessel.transform.position, minAltitude) : vesselTransform.position;
            float angleOff = Mathf.Sin(Time.time * 0.75f) * 180;
            angleOff = Mathf.Clamp(angleOff, -45, 45);
            target +=
                (Quaternion.AngleAxis(angleOff, upDirection) * Vector3.ProjectOnPlane(vesselTransform.up * 500, upDirection));
            //+ (Mathf.Sin (Time.time/3) * upDirection * minAltitude/3);
            debugString.AppendLine($"Evading unknown attacker");
            FlyToPosition(s, target);
        }

        void UpdateVelocityRelativeDirections() // Vectors that are used in TakeOff and FlyAvoidTerrain.
        {
            relativeVelocityRightDirection = Vector3.Cross(upDirection, vessel.srf_vel_direction).normalized;
            relativeVelocityDownDirection = Vector3.Cross(relativeVelocityRightDirection, vessel.srf_vel_direction).normalized;
        }

        void CheckLandingGear()
        {
            if (!vessel.LandedOrSplashed)
            {
                if (vessel.radarAltitude > 50.0f)
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false);
                else
                    vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, true);
            }
        }

        void TakeOff(FlightCtrlState s)
        {
            debugString.AppendLine($"Taking off/Gaining altitude");

            if (vessel.LandedOrSplashed && vessel.srfSpeed < takeOffSpeed)
            {
                SetStatus(initialTakeOff ? "Taking off" : vessel.Splashed ? "Splashed" : "Landed");
                if (vessel.Splashed)
                { vessel.ActionGroups.SetGroup(KSPActionGroup.Gear, false); }
                assignedPositionWorld = vessel.transform.position;
                return;
            }
            SetStatus("Gain Alt. (" + (int)minAltitude + "m)");

            steerMode = SteerModes.Aiming;

            float radarAlt = (float)vessel.radarAltitude;

            if (initialTakeOff && radarAlt > terrainAlertDetectionRadius)
                initialTakeOff = false;

            // Get surface normal relative to our velocity direction below the vessel and where the vessel is heading.
            RaycastHit rayHit;
            Vector3 forwardDirection = (vessel.horizontalSrfSpeed < 10 ? vesselTransform.up : (Vector3)vessel.srf_vel_direction) * 100; // Forward direction not adjusted for terrain.
            Vector3 forwardPoint = vessel.transform.position + forwardDirection * 100; // Forward point not adjusted for terrain.
            Ray ray = new Ray(forwardPoint, relativeVelocityDownDirection); // Check ahead and below.
            Vector3 terrainBelowAheadNormal = (Physics.Raycast(ray, out rayHit, minAltitude + 1.0f, 1 << 15)) ? rayHit.normal : upDirection; // Terrain normal below point ahead.
            ray = new Ray(vessel.transform.position, relativeVelocityDownDirection); // Check here below.
            Vector3 terrainBelowNormal = (Physics.Raycast(ray, out rayHit, minAltitude + 1.0f, 1 << 15)) ? rayHit.normal : upDirection; // Terrain normal below here.
            Vector3 normalToUse = Vector3.Dot(vessel.srf_vel_direction, terrainBelowNormal) < Vector3.Dot(vessel.srf_vel_direction, terrainBelowAheadNormal) ? terrainBelowNormal : terrainBelowAheadNormal; // Use the normal that has the steepest slope relative to our velocity.
            forwardPoint = vessel.transform.position + Vector3.ProjectOnPlane(forwardDirection, normalToUse).normalized * 100; // Forward point adjusted for terrain.
            float rise = Mathf.Clamp((float)vessel.srfSpeed * 0.215f, 5, 100); // Up to 45° rise angle above terrain changes at 465m/s.
            FlyToPosition(s, forwardPoint + upDirection * rise);
        }

        void UpdateTerrainAlertDetectionRadius(Vessel v)
        {
            if (v == vessel)
            {
                terrainAlertDetectionRadius = 2f * vessel.GetRadius();
            }
        }

        bool FlyAvoidTerrain(FlightCtrlState s) // Check for terrain ahead.
        {
            if (initialTakeOff) return false; // Don't do anything during the initial take-off.
            bool initialCorrection = !avoidingTerrain;
            float controlLagTime = 1.5f; // Time to fully adjust control surfaces. (Typical values seem to be 0.286s -- 1s for neutral to deployed according to wing lift comparison.) FIXME maybe this could also be a slider.

            ++terrainAlertTicker;
            int terrainAlertTickerThreshold = BDArmorySettings.TERRAIN_ALERT_FREQUENCY * (int)(1 + Mathf.Pow((float)vessel.radarAltitude / 500.0f, 2.0f) / Mathf.Max(1.0f, (float)vessel.srfSpeed / 150.0f)); // Scale with altitude^2 / speed.
            if (terrainAlertTicker >= terrainAlertTickerThreshold)
            {
                terrainAlertTicker = 0;

                // Reset/initialise some variables.
                avoidingTerrain = false; // Reset the alert.
                if (vessel.radarAltitude > minAltitude)
                    belowMinAltitude = false; // Also, reset the belowMinAltitude alert if it's active because of avoiding terrain.
                terrainAlertDistance = -1.0f; // Reset the terrain alert distance.
                float turnRadiusTwiddleFactor = turnRadiusTwiddleFactorMax; // A twiddle factor based on the orientation of the vessel, since it often takes considerable time to re-orient before avoiding the terrain. Start with the worst value.
                terrainAlertThreatRange = turnRadiusTwiddleFactor * turnRadius + (float)vessel.srfSpeed * controlLagTime; // The distance to the terrain to consider.

                // First, look 45° down, up, left and right from our velocity direction for immediate danger. (This should cover most immediate dangers.)
                Ray rayForwardUp = new Ray(vessel.transform.position, (vessel.srf_vel_direction - relativeVelocityDownDirection).normalized);
                Ray rayForwardDown = new Ray(vessel.transform.position, (vessel.srf_vel_direction + relativeVelocityDownDirection).normalized);
                Ray rayForwardLeft = new Ray(vessel.transform.position, (vessel.srf_vel_direction - relativeVelocityRightDirection).normalized);
                Ray rayForwardRight = new Ray(vessel.transform.position, (vessel.srf_vel_direction + relativeVelocityRightDirection).normalized);
                RaycastHit rayHit;
                if (Physics.Raycast(rayForwardDown, out rayHit, 1.5f * terrainAlertDetectionRadius, 1 << 15)) // sqrt(2) should be sufficient, so 1.5 will cover it.
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (Physics.Raycast(rayForwardUp, out rayHit, 1.5f * terrainAlertDetectionRadius, 1 << 15) && (terrainAlertDistance < 0.0f || rayHit.distance < terrainAlertDistance))
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (Physics.Raycast(rayForwardLeft, out rayHit, 1.5f * terrainAlertDetectionRadius, 1 << 15) && (terrainAlertDistance < 0.0f || rayHit.distance < terrainAlertDistance))
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (Physics.Raycast(rayForwardRight, out rayHit, 1.5f * terrainAlertDetectionRadius, 1 << 15) && (terrainAlertDistance < 0.0f || rayHit.distance < terrainAlertDistance))
                {
                    terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction);
                    terrainAlertNormal = rayHit.normal;
                }
                if (terrainAlertDistance > 0)
                {
                    terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, terrainAlertNormal).normalized;
                    avoidingTerrain = true;
                }
                else
                {
                    // Next, cast a sphere forwards to check for upcoming dangers.
                    Ray ray = new Ray(vessel.transform.position, vessel.srf_vel_direction);
                    if (Physics.SphereCast(ray, terrainAlertDetectionRadius, out rayHit, terrainAlertThreatRange, 1 << 15)) // Found something. 
                    {
                        // Check if there's anything directly ahead.
                        ray = new Ray(vessel.transform.position, vessel.srf_vel_direction);
                        terrainAlertDistance = rayHit.distance * -Vector3.Dot(rayHit.normal, vessel.srf_vel_direction); // Distance to terrain along direction of terrain normal.
                        terrainAlertNormal = rayHit.normal;
                        if (BDArmorySettings.DRAW_DEBUG_LINES)
                        {
                            terrainAlertDebugPos = rayHit.point;
                            terrainAlertDebugDir = rayHit.normal;
                        }
                        if (!Physics.Raycast(ray, out rayHit, terrainAlertThreatRange, 1 << 15)) // Nothing directly ahead, so we're just barely avoiding terrain.
                        {
                            // Change the terrain normal and direction as we want to just fly over it instead of banking away from it.
                            terrainAlertNormal = upDirection;
                            terrainAlertDirection = vessel.srf_vel_direction;
                        }
                        else
                        { terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, terrainAlertNormal).normalized; }
                        float sinTheta = Math.Min(0.0f, Vector3.Dot(vessel.srf_vel_direction, terrainAlertNormal)); // sin(theta) (measured relative to the plane of the surface).
                        float oneMinusCosTheta = 1.0f - Mathf.Sqrt(Math.Max(0.0f, 1.0f - sinTheta * sinTheta));
                        turnRadiusTwiddleFactor = (turnRadiusTwiddleFactorMin + turnRadiusTwiddleFactorMax) / 2.0f - (turnRadiusTwiddleFactorMax - turnRadiusTwiddleFactorMin) / 2.0f * Vector3.Dot(terrainAlertNormal, -vessel.transform.forward); // This would depend on roll rate (i.e., how quickly the vessel can reorient itself to perform the terrain avoidance maneuver) and probably other things.
                        float controlLagCompensation = Mathf.Max(0f, -Vector3.Dot(AIUtils.PredictPosition(vessel, controlLagTime * turnRadiusTwiddleFactor) - vessel.transform.position, terrainAlertNormal)); // Include twiddle factor as more re-orienting requires more control surface movement.
                        terrainAlertThreshold = turnRadiusTwiddleFactor * turnRadius * oneMinusCosTheta + controlLagCompensation;
                        if (terrainAlertDistance < terrainAlertThreshold) // Only do something about it if the estimated turn amount is a problem.
                        {
                            avoidingTerrain = true;

                            // Shoot new ray in direction theta/2 (i.e., the point where we should be parallel to the surface) above velocity direction to check if the terrain slope is increasing.
                            float phi = -Mathf.Asin(sinTheta) / 2f;
                            Vector3 upcoming = Vector3.RotateTowards(vessel.srf_vel_direction, terrainAlertNormal, phi, 0f);
                            ray = new Ray(vessel.transform.position, upcoming);
                            if (BDArmorySettings.DRAW_DEBUG_LINES)
                                terrainAlertDebugDraw2 = false;
                            if (Physics.Raycast(ray, out rayHit, terrainAlertThreatRange, 1 << 15))
                            {
                                if (rayHit.distance < terrainAlertDistance / Mathf.Sin(phi)) // Hit terrain closer than expected => terrain slope is increasing relative to our velocity direction.
                                {
                                    if (BDArmorySettings.DRAW_DEBUG_LINES)
                                    {
                                        terrainAlertDebugDraw2 = true;
                                        terrainAlertDebugPos2 = rayHit.point;
                                        terrainAlertDebugDir2 = rayHit.normal;
                                    }
                                    terrainAlertNormal = rayHit.normal; // Use the normal of the steeper terrain (relative to our velocity).
                                    terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, terrainAlertNormal).normalized;
                                }
                            }
                        }
                    }
                }
                // Finally, check the distance to sea-level as water doesn't act like a collider, so it's getting ignored.
                if (vessel.mainBody.ocean)
                {
                    float sinTheta = Vector3.Dot(vessel.srf_vel_direction, upDirection); // sin(theta) (measured relative to the ocean surface).
                    if (sinTheta < 0f) // Heading downwards
                    {
                        float oneMinusCosTheta = 1.0f - Mathf.Sqrt(Math.Max(0.0f, 1.0f - sinTheta * sinTheta));
                        turnRadiusTwiddleFactor = (turnRadiusTwiddleFactorMin + turnRadiusTwiddleFactorMax) / 2.0f - (turnRadiusTwiddleFactorMax - turnRadiusTwiddleFactorMin) / 2.0f * Vector3.Dot(upDirection, -vessel.transform.forward); // This would depend on roll rate (i.e., how quickly the vessel can reorient itself to perform the terrain avoidance maneuver) and probably other things.
                        float controlLagCompensation = Mathf.Max(0f, -Vector3.Dot(AIUtils.PredictPosition(vessel, controlLagTime * turnRadiusTwiddleFactor) - vessel.transform.position, upDirection)); // Include twiddle factor as more re-orienting requires more control surface movement.
                        terrainAlertThreshold = turnRadiusTwiddleFactor * turnRadius * oneMinusCosTheta + controlLagCompensation;

                        if ((float)vessel.altitude < terrainAlertThreshold && (terrainAlertDistance < 0 || (float)vessel.altitude < terrainAlertDistance)) // If the ocean surface is closer than the terrain (if any), then override the terrain alert values.
                        {
                            terrainAlertDistance = (float)vessel.altitude;
                            terrainAlertNormal = upDirection;
                            terrainAlertDirection = Vector3.ProjectOnPlane(vessel.srf_vel_direction, upDirection).normalized;
                            avoidingTerrain = true;

                            if (BDArmorySettings.DRAW_DEBUG_LINES)
                            {
                                terrainAlertDebugPos = vessel.transform.position + vessel.srf_vel_direction * (float)vessel.altitude / -sinTheta;
                                terrainAlertDebugDir = upDirection;
                            }
                        }
                    }
                }
            }

            if (avoidingTerrain)
            {
                belowMinAltitude = true; // Inform other parts of the code to behave as if we're below minimum altitude.
                float maxAngle = 70.0f * Mathf.Deg2Rad; // Maximum angle (towards surface normal) to aim.
                float adjustmentFactor = 1f; // Mathf.Clamp(1.0f - Mathf.Pow(terrainAlertDistance / terrainAlertThreatRange, 2.0f), 0.0f, 1.0f); // Don't yank too hard as it kills our speed too much. (This doesn't seem necessary.)
                // First, aim up to maxAngle towards the surface normal.
                Vector3 correctionDirection = Vector3.RotateTowards(terrainAlertDirection, terrainAlertNormal, maxAngle * adjustmentFactor, 0.0f);
                // Then, adjust the vertical pitch for our speed (to try to avoid stalling).
                Vector3 horizontalCorrectionDirection = Vector3.ProjectOnPlane(correctionDirection, upDirection).normalized;
                correctionDirection = Vector3.RotateTowards(correctionDirection, horizontalCorrectionDirection, Mathf.Max(0.0f, (1.0f - (float)vessel.srfSpeed / 120.0f) / 2.0f * maxAngle * Mathf.Deg2Rad) * adjustmentFactor, 0.0f); // Rotate up to maxAngle/2 back towards horizontal depending on speed < 120m/s.
                float alpha = Time.fixedDeltaTime * 2f; // 0.04 seems OK.
                float beta = Mathf.Pow(1.0f - alpha, terrainAlertTickerThreshold);
                terrainAlertCorrectionDirection = initialCorrection ? terrainAlertCorrectionDirection : (beta * terrainAlertCorrectionDirection + (1.0f - beta) * correctionDirection).normalized; // Update our target direction over several frames (if it's not the initial correction). (Expansion of N iterations of A = A*(1-a) + B*a. Not exact due to normalisation in the loop, but good enough.)
                FlyToPosition(s, vessel.transform.position + terrainAlertCorrectionDirection * 100);

                // Update status and book keeping.
                SetStatus("Terrain (" + (int)terrainAlertDistance + "m)");
                terrainAlertCoolDown = 0.5f; // 0.5s cool down after avoiding terrain or gaining altitude. (Only used for delaying "orbitting" for now.)
                return true;
            }

            // Hurray, we've avoided the terrain!
            avoidingTerrain = false;
            return false;
        }

        bool FlyAvoidOthers(FlightCtrlState s) // Check for collisions with other vessels and try to avoid them.
        { // Mostly a re-hash of FlyAvoidCollision, but with terrain detection removed.
            if (vesselCollisionAvoidancePeriod < Time.fixedDeltaTime) return false;
            if (collisionDetectionTimer > vesselCollisionAvoidancePeriod)
            {
                collisionDetectionTimer = 0;
                collisionDetectionTicker = vesselCollisionAvoidanceTickerFreq + 1;
            }
            if (collisionDetectionTimer > 0)
            {
                //fly avoid
                SetStatus("AvoidCollision");
                debugString.AppendLine($"Avoiding Collision");
                collisionDetectionTimer += Time.fixedDeltaTime;

                Vector3 target = vesselTransform.position + collisionAvoidDirection;
                FlyToPosition(s, target);
                return true;
            }
            else if (collisionDetectionTicker > vesselCollisionAvoidanceTickerFreq) // Only check every vesselCollisionAvoidanceTickerFreq frames.
            {
                collisionDetectionTicker = 0;

                // Check for collisions with other vessels.
                bool vesselCollision = false;
                collisionAvoidDirection = vessel.srf_vel_direction;
                using (var vs = BDATargetManager.LoadedVessels.GetEnumerator())
                    while (vs.MoveNext())
                    {
                        if (vs.Current == null) continue;
                        if (vs.Current == vessel || vs.Current.Landed || !(Vector3.Dot(vs.Current.transform.position - vesselTransform.position, vesselTransform.up) > 0)) continue;
                        if (!PredictCollisionWithVessel(vs.Current, vesselCollisionAvoidancePeriod + vesselCollisionAvoidanceTickerFreq * Time.fixedDeltaTime, out collisionAvoidDirection)) continue;
                        var ibdaiControl = vs.Current.FindPartModuleImplementing<IBDAIControl>();
                        if (ibdaiControl != null && ibdaiControl.commandLeader != null && ibdaiControl.commandLeader.vessel == vessel) continue;
                        vesselCollision = true;
                        break; // Early exit on first detected vessel collision. Chances of multiple vessel collisions are low.
                    }
                if (vesselCollision)
                {
                    Vector3 axis = -Vector3.Cross(vesselTransform.up, collisionAvoidDirection);
                    collisionAvoidDirection = Quaternion.AngleAxis(25, axis) * collisionAvoidDirection;        //don't need to change the angle that much to avoid, and it should prevent stupid suicidal manuevers as well
                    collisionDetectionTimer += Time.fixedDeltaTime;
                    return FlyAvoidOthers(s); // Call ourself again to trigger the actual avoidance.
                }
            }
            else
            { ++collisionDetectionTicker; }
            return false;
        }

        Vector3 GetLimitedClimbDirectionForSpeed(Vector3 direction)
        {
            if (Vector3.Dot(direction, upDirection) < 0)
            {
                debugString.AppendLine($"climb limit angle: unlimited");
                return direction; //only use this if climbing
            }

            Vector3 planarDirection = Vector3.ProjectOnPlane(direction, upDirection).normalized * 100;

            float angle = Mathf.Clamp((float)vessel.srfSpeed * 0.13f, 5, 90);

            debugString.AppendLine($"climb limit angle: {angle}");
            return Vector3.RotateTowards(planarDirection, direction, angle * Mathf.Deg2Rad, 0);
        }

        void UpdateGAndAoALimits(FlightCtrlState s)
        {
            if (vessel.dynamicPressurekPa <= 0 || vessel.srfSpeed < takeOffSpeed || belowMinAltitude && -Vector3.Dot(vessel.ReferenceTransform.forward, vessel.upAxis) < 0.8f)
            {
                return;
            }

            if (lastAllowedAoA != maxAllowedAoA)
            {
                lastAllowedAoA = maxAllowedAoA;
                maxAllowedCosAoA = (float)Math.Cos(lastAllowedAoA * Math.PI / 180.0);
            }
            float pitchG = -Vector3.Dot(vessel.acceleration, vessel.ReferenceTransform.forward);       //should provide g force in vessel up / down direction, assuming a standard plane
            float pitchGPerDynPres = pitchG / (float)vessel.dynamicPressurekPa;

            float curCosAoA = Vector3.Dot(vessel.Velocity().normalized, vessel.ReferenceTransform.forward);

            //adjust moving averages
            //adjust gLoad average
            gLoadMovingAvg *= 32f;
            gLoadMovingAvg -= gLoadMovingAvgArray[movingAvgIndex];
            gLoadMovingAvgArray[movingAvgIndex] = pitchGPerDynPres;
            gLoadMovingAvg += pitchGPerDynPres;
            gLoadMovingAvg /= 32f;

            //adjusting cosAoAAvg
            cosAoAMovingAvg *= 32f;
            cosAoAMovingAvg -= cosAoAMovingAvgArray[movingAvgIndex];
            cosAoAMovingAvgArray[movingAvgIndex] = curCosAoA;
            cosAoAMovingAvg += curCosAoA;
            cosAoAMovingAvg /= 32f;

            ++movingAvgIndex;
            if (movingAvgIndex == gLoadMovingAvgArray.Length)
                movingAvgIndex = 0;

            if (gLoadMovingAvg < maxNegG || Math.Abs(cosAoAMovingAvg - cosAoAAtMaxNegG) < 0.005f)
            {
                maxNegG = gLoadMovingAvg;
                cosAoAAtMaxNegG = cosAoAMovingAvg;
            }
            if (gLoadMovingAvg > maxPosG || Math.Abs(cosAoAMovingAvg - cosAoAAtMaxPosG) < 0.005f)
            {
                maxPosG = gLoadMovingAvg;
                cosAoAAtMaxPosG = cosAoAMovingAvg;
            }

            if (cosAoAAtMaxNegG >= cosAoAAtMaxPosG)
            {
                cosAoAAtMaxNegG = cosAoAAtMaxPosG = maxNegG = maxPosG = 0;
                gOffsetPerDynPres = gaoASlopePerDynPres = 0;
                return;
            }

            // if (maxPosG > maxDynPresGRecorded)
            //     maxDynPresGRecorded = maxPosG;

            dynDynPresGRecorded *= dynDecayRate; // Decay the highest observed G-force from dynamic pressure (we want a fairly recent value in case the planes dynamics have changed).
            if (!vessel.LandedOrSplashed && Math.Abs(gLoadMovingAvg) > dynDynPresGRecorded)
                dynDynPresGRecorded = Math.Abs(gLoadMovingAvg);

            dynMaxVelocityMagSqr *= dynDecayRate; // Decay the max recorded squared velocity at the same rate as the dynamic pressure G-force decays to keep the turnRadius constant if they otherwise haven't changed.
            if (!vessel.LandedOrSplashed && (float)vessel.Velocity().sqrMagnitude > dynMaxVelocityMagSqr)
                dynMaxVelocityMagSqr = (float)vessel.Velocity().sqrMagnitude;

            float aoADiff = cosAoAAtMaxPosG - cosAoAAtMaxNegG;

            //if (Math.Abs(pitchControlDiff) < 0.005f)
            //    return;                 //if the pitch control values are too similar, don't bother to avoid numerical errors

            gaoASlopePerDynPres = (maxPosG - maxNegG) / aoADiff;
            gOffsetPerDynPres = maxPosG - gaoASlopePerDynPres * cosAoAAtMaxPosG;     //g force offset
        }

        void AdjustPitchForGAndAoALimits(FlightCtrlState s)
        {
            float minCosAoA, maxCosAoA;
            //debugString += "\nMax Pos G: " + maxPosG + " @ " + cosAoAAtMaxPosG;
            //debugString += "\nMax Neg G: " + maxNegG + " @ " + cosAoAAtMaxNegG;

            if (vessel.LandedOrSplashed || vessel.srfSpeed < Math.Min(minSpeed, takeOffSpeed))         //if we're going too slow, don't use this
            {
                float speed = Math.Max(takeOffSpeed, minSpeed);
                negPitchDynPresLimitIntegrator = -1f * 0.001f * 0.5f * 1.225f * speed * speed;
                posPitchDynPresLimitIntegrator = 1f * 0.001f * 0.5f * 1.225f * speed * speed;
                return;
            }

            float invVesselDynPreskPa = 1f / (float)vessel.dynamicPressurekPa;

            maxCosAoA = maxAllowedGForce * bodyGravity * invVesselDynPreskPa;
            minCosAoA = -maxCosAoA;

            maxCosAoA -= gOffsetPerDynPres;
            minCosAoA -= gOffsetPerDynPres;

            maxCosAoA /= gaoASlopePerDynPres;
            minCosAoA /= gaoASlopePerDynPres;

            if (maxCosAoA > maxAllowedCosAoA)
                maxCosAoA = maxAllowedCosAoA;

            if (minCosAoA < -maxAllowedCosAoA)
                minCosAoA = -maxAllowedCosAoA;

            float curCosAoA = Vector3.Dot(vessel.Velocity() / vessel.srfSpeed, vessel.ReferenceTransform.forward);

            float centerCosAoA = (minCosAoA + maxCosAoA) * 0.5f;
            float curCosAoACentered = curCosAoA - centerCosAoA;
            float cosAoADiff = 0.5f * Math.Abs(maxCosAoA - minCosAoA);
            float curCosAoANorm = curCosAoACentered / cosAoADiff;      //scaled so that from centerAoA to maxAoA is 1

            float negPitchScalar, posPitchScalar;
            negPitchScalar = negPitchDynPresLimitIntegrator * invVesselDynPreskPa - lastPitchInput;
            posPitchScalar = lastPitchInput - posPitchDynPresLimitIntegrator * invVesselDynPreskPa;

            //update pitch control limits as needed
            float negPitchDynPresLimit, posPitchDynPresLimit;
            negPitchDynPresLimit = posPitchDynPresLimit = 0;
            if (curCosAoANorm < -0.15f)// || Math.Abs(negPitchScalar) < 0.01f)
            {
                float cosAoAOffset = curCosAoANorm + 1;     //set max neg aoa to be 0
                float aoALimScalar = Math.Abs(curCosAoANorm);
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                if (aoALimScalar > 1)
                    aoALimScalar = 1;

                float pitchInputScalar = negPitchScalar;
                pitchInputScalar = 1 - Mathf.Clamp01(Math.Abs(pitchInputScalar));
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                if (pitchInputScalar < 0)
                    pitchInputScalar = 0;

                float deltaCosAoANorm = curCosAoA - lastCosAoA;
                deltaCosAoANorm /= cosAoADiff;

                debugString.AppendLine($"Updating Neg Gs");
                negPitchDynPresLimitIntegrator -= 0.01f * Mathf.Clamp01(aoALimScalar + pitchInputScalar) * cosAoAOffset * (float)vessel.dynamicPressurekPa;
                negPitchDynPresLimitIntegrator -= 0.005f * deltaCosAoANorm * (float)vessel.dynamicPressurekPa;
                if (cosAoAOffset < 0)
                    negPitchDynPresLimit = -0.3f * cosAoAOffset;
            }
            if (curCosAoANorm > 0.15f)// || Math.Abs(posPitchScalar) < 0.01f)
            {
                float cosAoAOffset = curCosAoANorm - 1;     //set max pos aoa to be 0
                float aoALimScalar = Math.Abs(curCosAoANorm);
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                aoALimScalar *= aoALimScalar;
                if (aoALimScalar > 1)
                    aoALimScalar = 1;

                float pitchInputScalar = posPitchScalar;
                pitchInputScalar = 1 - Mathf.Clamp01(Math.Abs(pitchInputScalar));
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                pitchInputScalar *= pitchInputScalar;
                if (pitchInputScalar < 0)
                    pitchInputScalar = 0;

                float deltaCosAoANorm = curCosAoA - lastCosAoA;
                deltaCosAoANorm /= cosAoADiff;

                debugString.AppendLine($"Updating Pos Gs");
                posPitchDynPresLimitIntegrator -= 0.01f * Mathf.Clamp01(aoALimScalar + pitchInputScalar) * cosAoAOffset * (float)vessel.dynamicPressurekPa;
                posPitchDynPresLimitIntegrator -= 0.005f * deltaCosAoANorm * (float)vessel.dynamicPressurekPa;
                if (cosAoAOffset > 0)
                    posPitchDynPresLimit = -0.3f * cosAoAOffset;
            }

            float currentG = -Vector3.Dot(vessel.acceleration, vessel.ReferenceTransform.forward);
            float negLim, posLim;
            negLim = negPitchDynPresLimitIntegrator * invVesselDynPreskPa + negPitchDynPresLimit;
            if (negLim > s.pitch)
            {
                if (currentG > -(maxAllowedGForce * 0.97f * bodyGravity))
                {
                    negPitchDynPresLimitIntegrator -= (float)(0.15 * vessel.dynamicPressurekPa);        //jsut an override in case things break

                    maxNegG = currentG * invVesselDynPreskPa;
                    cosAoAAtMaxNegG = curCosAoA;

                    negPitchDynPresLimit = 0;

                    //maxPosG = 0;
                    //cosAoAAtMaxPosG = 0;
                }

                s.pitch = negLim;
                debugString.AppendLine($"Limiting Neg Gs");
            }
            posLim = posPitchDynPresLimitIntegrator * invVesselDynPreskPa + posPitchDynPresLimit;
            if (posLim < s.pitch)
            {
                if (currentG < (maxAllowedGForce * 0.97f * bodyGravity))
                {
                    posPitchDynPresLimitIntegrator += (float)(0.15 * vessel.dynamicPressurekPa);        //jsut an override in case things break

                    maxPosG = currentG * invVesselDynPreskPa;
                    cosAoAAtMaxPosG = curCosAoA;

                    posPitchDynPresLimit = 0;

                    //maxNegG = 0;
                    //cosAoAAtMaxNegG = 0;
                }

                s.pitch = posLim;
                debugString.AppendLine($"Limiting Pos Gs");
            }

            lastPitchInput = s.pitch;
            lastCosAoA = curCosAoA;

            debugString.AppendLine($"Neg Pitch Lim: {negLim}");
            debugString.AppendLine($"Pos Pitch Lim: {posLim}");
        }

        void CalculateAccelerationAndTurningCircle()
        {
            maxLiftAcceleration = dynDynPresGRecorded * (float)vessel.dynamicPressurekPa; //maximum acceleration from lift that the vehicle can provide

            maxLiftAcceleration = Mathf.Clamp(maxLiftAcceleration, bodyGravity, maxAllowedGForce * bodyGravity); //limit it to whichever is smaller, what we can provide or what we can handle. Assume minimum of 1G to avoid extremely high turn radiuses.

            turnRadius = dynMaxVelocityMagSqr / maxLiftAcceleration; //radius that we can turn in assuming constant velocity, assuming simple circular motion (this is a terrible assumption, the AI usually turns on afterboosters!)
        }

        Vector3 DefaultAltPosition()
        {
            return (vessel.transform.position + (-(float)vessel.altitude * upDirection) + (defaultAltitude * upDirection));
        }

        Vector3 GetSurfacePosition(Vector3 position)
        {
            return position - ((float)FlightGlobals.getAltitudeAtPos(position) * upDirection);
        }

        Vector3 GetTerrainSurfacePosition(Vector3 position)
        {
            return position - (MissileGuidance.GetRaycastRadarAltitude(position) * upDirection);
        }

        Vector3 FlightPosition(Vector3 targetPosition, float minAlt)
        {
            Vector3 forwardDirection = vesselTransform.up;
            Vector3 targetDirection = (targetPosition - vesselTransform.position).normalized;

            float vertFactor = 0;
            vertFactor += (((float)vessel.srfSpeed / minSpeed) - 2f) * 0.3f;          //speeds greater than 2x minSpeed encourage going upwards; below encourages downwards
            vertFactor += (((targetPosition - vesselTransform.position).magnitude / 1000f) - 1f) * 0.3f;    //distances greater than 1000m encourage going upwards; closer encourages going downwards
            vertFactor -= Mathf.Clamp01(Vector3.Dot(vesselTransform.position - targetPosition, upDirection) / 1600f - 1f) * 0.5f;       //being higher than 1600m above a target encourages going downwards
            if (targetVessel)
                vertFactor += Vector3.Dot(targetVessel.Velocity() / targetVessel.srfSpeed, (targetVessel.ReferenceTransform.position - vesselTransform.position).normalized) * 0.3f;   //the target moving away from us encourages upward motion, moving towards us encourages downward motion
            else
                vertFactor += 0.4f;
            vertFactor -= weaponManager.underFire ? 0.5f : 0;   //being under fire encourages going downwards as well, to gain energy

            float alt = (float)vessel.radarAltitude;

            if (vertFactor > 2)
                vertFactor = 2;
            if (vertFactor < -2)
                vertFactor = -2;

            vertFactor += 0.15f * Mathf.Sin((float)vessel.missionTime * 0.25f);     //some randomness in there

            Vector3 projectedDirection = Vector3.ProjectOnPlane(forwardDirection, upDirection);
            Vector3 projectedTargetDirection = Vector3.ProjectOnPlane(targetDirection, upDirection);
            if (Vector3.Dot(targetDirection, forwardDirection) < 0)
            {
                if (Vector3.Angle(targetDirection, forwardDirection) > 165f)
                {
                    targetPosition = vesselTransform.position + (Quaternion.AngleAxis(Mathf.Sign(Mathf.Sin((float)vessel.missionTime / 4)) * 45, upDirection) * (projectedDirection.normalized * 200));
                    targetDirection = (targetPosition - vesselTransform.position).normalized;
                }

                targetPosition = vesselTransform.position + Vector3.Cross(Vector3.Cross(forwardDirection, targetDirection), forwardDirection).normalized * 200;
            }
            else if (steerMode != SteerModes.Aiming)
            {
                float distance = (targetPosition - vesselTransform.position).magnitude;
                if (vertFactor < 0)
                    distance = Math.Min(distance, Math.Abs((alt - minAlt) / vertFactor));

                targetPosition += upDirection * Math.Min(distance, 1000) * vertFactor * Mathf.Clamp01(0.7f - Math.Abs(Vector3.Dot(projectedTargetDirection, projectedDirection)));
            }

            if ((float)vessel.radarAltitude > minAlt * 1.1f)
            {
                return targetPosition;
            }

            float pointRadarAlt = MissileGuidance.GetRaycastRadarAltitude(targetPosition);
            if (pointRadarAlt < minAlt)
            {
                float adjustment = (minAlt - pointRadarAlt);
                debugString.AppendLine($"Target position is below minAlt. Adjusting by {adjustment}");
                return targetPosition + (adjustment * upDirection);
            }
            else
            {
                return targetPosition;
            }
        }

        private float SteerDamping(float angleToTarget, float defaultTargetPosition, int axis)
        { //adjusts steer damping relative to a vessel's angle to its target position
            if (!dynamicSteerDamping) // Check if enabled.
            {
                DynamicDampingLabel = "Dyn Damping Not Toggled";
                PitchLabel = "Dyn Damping Not Toggled";
                YawLabel = "Dyn Damping Not Toggled";
                RollLabel = "Dyn Damping Not Toggled";
                return steerDamping;
            }
            else if (angleToTarget >= 180 || angleToTarget < 0) // Check for valid angle to target.
            {
                if (!CustomDynamicAxisFields)
                    DynamicDampingLabel = "N/A";
                switch (axis)
                {
                    case 1:
                        PitchLabel = "N/A";
                        break;
                    case 2:
                        YawLabel = "N/A";
                        break;
                    case 3:
                        RollLabel = "N/A";
                        break;
                }
                return steerDamping;
            }

            if (CustomDynamicAxisFields)
            {
                switch (axis)
                {
                    case 1:
                        if (dynamicDampingPitch)
                        {
                            dynSteerDampingPitchValue = GetDampeningFactor(angleToTarget, dynamicSteerDampingPitchFactor, DynamicDampingPitchMin, DynamicDampingPitchMax);
                            PitchLabel = dynSteerDampingPitchValue.ToString();
                            return dynSteerDampingPitchValue;
                        }
                        break;
                    case 2:
                        if (dynamicDampingYaw)
                        {
                            dynSteerDampingYawValue = GetDampeningFactor(angleToTarget, dynamicSteerDampingYawFactor, DynamicDampingYawMin, DynamicDampingYawMax);
                            YawLabel = dynSteerDampingYawValue.ToString();
                            return dynSteerDampingYawValue;
                        }
                        break;
                    case 3:
                        if (dynamicDampingRoll)
                        {
                            dynSteerDampingRollValue = GetDampeningFactor(angleToTarget, dynamicSteerDampingRollFactor, DynamicDampingRollMin, DynamicDampingRollMax);
                            RollLabel = dynSteerDampingRollValue.ToString();
                            return dynSteerDampingRollValue;
                        }
                        break;
                }
                // The specific axis wasn't enabled, use the global value
                dynSteerDampingValue = steerDamping;
                switch (axis)
                {
                    case 1:
                        PitchLabel = dynSteerDampingValue.ToString();
                        break;
                    case 2:
                        YawLabel = dynSteerDampingValue.ToString();
                        break;
                    case 3:
                        RollLabel = dynSteerDampingValue.ToString();
                        break;
                }
                return dynSteerDampingValue;
            }
            else //if custom axis groups is disabled
            {
                dynSteerDampingValue = GetDampeningFactor(defaultTargetPosition, dynamicSteerDampingFactor, DynamicDampingMin, DynamicDampingMax);
                DynamicDampingLabel = dynSteerDampingValue.ToString();
                return dynSteerDampingValue;
            }
        }

        private float GetDampeningFactor(float angleToTarget, float dynamicSteerDampingFactorAxis, float DynamicDampingMinAxis, float DynamicDampingMaxAxis)
        {
            return Mathf.Clamp((float)(Math.Pow((180 - angleToTarget) / 180, dynamicSteerDampingFactorAxis) * (DynamicDampingMaxAxis - DynamicDampingMinAxis) + DynamicDampingMinAxis), Mathf.Min(DynamicDampingMinAxis, DynamicDampingMaxAxis), Mathf.Max(DynamicDampingMinAxis, DynamicDampingMaxAxis));
        }

        public override bool IsValidFixedWeaponTarget(Vessel target)
        {
            if (!vessel) return false;
            // aircraft can aim at anything
            return true;
        }

        bool DetectCollision(Vector3 direction, out Vector3 badDirection)
        {
            badDirection = Vector3.zero;
            if ((float)vessel.radarAltitude < 20) return false;

            direction = direction.normalized;
            int layerMask = 1 << 15;
            Ray ray = new Ray(vesselTransform.position + (50 * vesselTransform.up), direction);
            float distance = Mathf.Clamp((float)vessel.srfSpeed * 4f, 125f, 2500);
            RaycastHit hit;
            if (!Physics.SphereCast(ray, 10, out hit, distance, layerMask)) return false;
            Rigidbody otherRb = hit.collider.attachedRigidbody;
            if (otherRb)
            {
                if (!(Vector3.Dot(otherRb.velocity, vessel.Velocity()) < 0)) return false;
                badDirection = hit.point - ray.origin;
                return true;
            }
            badDirection = hit.point - ray.origin;
            return true;
        }

        void UpdateCommand(FlightCtrlState s)
        {
            if (command == PilotCommands.Follow && !commandLeader)
            {
                ReleaseCommand();
                return;
            }

            if (command == PilotCommands.Follow)
            {
                SetStatus("Follow");
                UpdateFollowCommand(s);
            }
            else if (command == PilotCommands.FlyTo)
            {
                SetStatus("Fly To");
                FlyOrbit(s, assignedPositionGeo, 2500, idleSpeed, ClockwiseOrbit);
            }
            else if (command == PilotCommands.Attack)
            {
                if ((BDArmorySettings.RUNWAY_PROJECT) && (targetVessel != null) && ((targetVessel.vesselTransform.position - vessel.vesselTransform.position).sqrMagnitude <= (weaponManager.guardRange * weaponManager.guardRange))) // If the vessel has a target within range, let it fight!
                {
                    ReleaseCommand();
                    return;
                }
                else if (weaponManager.underAttack || weaponManager.underFire)
                {
                    ReleaseCommand();
                    return;
                }
                else
                {
                    SetStatus("Attack");
                    FlyOrbit(s, assignedPositionGeo, 4500, maxSpeed, ClockwiseOrbit);
                }
            }
        }

        void UpdateFollowCommand(FlightCtrlState s)
        {
            steerMode = SteerModes.NormalFlight;
            vessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);

            commandSpeed = commandLeader.vessel.srfSpeed;
            commandHeading = commandLeader.vessel.Velocity().normalized;

            //formation position
            Vector3d commandPosition = GetFormationPosition();
            debugFollowPosition = commandPosition;

            float distanceToPos = Vector3.Distance(vesselTransform.position, commandPosition);

            float dotToPos = Vector3.Dot(vesselTransform.up, commandPosition - vesselTransform.position);
            Vector3 flyPos;
            useRollHint = false;

            float ctrlModeThresh = 1000;

            if (distanceToPos < ctrlModeThresh)
            {
                flyPos = commandPosition + (ctrlModeThresh * commandHeading);

                Vector3 vectorToFlyPos = flyPos - vessel.ReferenceTransform.position;
                Vector3 projectedPosOffset = Vector3.ProjectOnPlane(commandPosition - vessel.ReferenceTransform.position, commandHeading);
                float posOffsetMag = projectedPosOffset.magnitude;
                float adjustAngle = (Mathf.Clamp(posOffsetMag * 0.27f, 0, 25));
                Vector3 projVel = Vector3.Project(vessel.Velocity() - commandLeader.vessel.Velocity(), projectedPosOffset);
                adjustAngle -= Mathf.Clamp(Mathf.Sign(Vector3.Dot(projVel, projectedPosOffset)) * projVel.magnitude * 0.12f, -10, 10);

                adjustAngle *= Mathf.Deg2Rad;

                vectorToFlyPos = Vector3.RotateTowards(vectorToFlyPos, projectedPosOffset, adjustAngle, 0);

                flyPos = vessel.ReferenceTransform.position + vectorToFlyPos;

                if (distanceToPos < 400)
                {
                    steerMode = SteerModes.Aiming;
                }
                else
                {
                    steerMode = SteerModes.NormalFlight;
                }

                if (distanceToPos < 10)
                {
                    useRollHint = true;
                }
            }
            else
            {
                steerMode = SteerModes.NormalFlight;
                flyPos = commandPosition;
            }

            double finalMaxSpeed = commandSpeed;
            if (dotToPos > 0)
            {
                finalMaxSpeed += (distanceToPos / 8);
            }
            else
            {
                finalMaxSpeed -= (distanceToPos / 2);
            }

            AdjustThrottle((float)finalMaxSpeed, true);

            FlyToPosition(s, flyPos);
        }

        Vector3d GetFormationPosition()
        {
            Quaternion origVRot = velocityTransform.rotation;
            Vector3 origVLPos = velocityTransform.localPosition;

            velocityTransform.position = commandLeader.vessel.ReferenceTransform.position;
            if (commandLeader.vessel.Velocity() != Vector3d.zero)
            {
                velocityTransform.rotation = Quaternion.LookRotation(commandLeader.vessel.Velocity(), upDirection);
                velocityTransform.rotation = Quaternion.AngleAxis(90, velocityTransform.right) * velocityTransform.rotation;
            }
            else
            {
                velocityTransform.rotation = commandLeader.vessel.ReferenceTransform.rotation;
            }

            Vector3d pos = velocityTransform.TransformPoint(this.GetLocalFormationPosition(commandFollowIndex));// - lateralVelVector - verticalVelVector;

            velocityTransform.localPosition = origVLPos;
            velocityTransform.rotation = origVRot;

            return pos;
        }

        public override void CommandTakeOff()
        {
            base.CommandTakeOff();
            standbyMode = false;
        }

        protected override void OnGUI()
        {
            base.OnGUI();

            if (!pilotEnabled || !vessel.isActiveVessel) return;

            if (!BDArmorySettings.DRAW_DEBUG_LINES) return;
            if (command == PilotCommands.Follow)
            {
                BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, debugFollowPosition, 2, Color.red);
            }

            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, debugPos, 5, Color.red);
            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + vesselTransform.up * 1000, 3, Color.white);
            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + -vesselTransform.forward * 100, 3, Color.yellow);

            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position, vesselTransform.position + rollTarget, 2, Color.blue);
            BDGUIUtils.DrawLineBetweenWorldPositions(vesselTransform.position + (0.05f * vesselTransform.right), vesselTransform.position + (0.05f * vesselTransform.right) + angVelRollTarget, 2, Color.green);
            if (avoidingTerrain)
            {
                BDGUIUtils.DrawLineBetweenWorldPositions(vessel.transform.position, terrainAlertDebugPos, 2, Color.cyan);
                BDGUIUtils.DrawLineBetweenWorldPositions(terrainAlertDebugPos, terrainAlertDebugPos + (terrainAlertThreshold - terrainAlertDistance) * terrainAlertDebugDir, 2, Color.cyan);
                if (terrainAlertDebugDraw2)
                {
                    BDGUIUtils.DrawLineBetweenWorldPositions(vessel.transform.position, terrainAlertDebugPos2, 2, Color.yellow);
                    BDGUIUtils.DrawLineBetweenWorldPositions(terrainAlertDebugPos2, terrainAlertDebugPos2 + (terrainAlertThreshold - terrainAlertDistance) * terrainAlertDebugDir2, 2, Color.yellow);
                }
                BDGUIUtils.DrawLineBetweenWorldPositions(vessel.transform.position, vessel.transform.position + 1.5f * terrainAlertDetectionRadius * (vessel.srf_vel_direction - relativeVelocityDownDirection).normalized, 1, Color.grey);
                BDGUIUtils.DrawLineBetweenWorldPositions(vessel.transform.position, vessel.transform.position + 1.5f * terrainAlertDetectionRadius * (vessel.srf_vel_direction + relativeVelocityDownDirection).normalized, 1, Color.grey);
                BDGUIUtils.DrawLineBetweenWorldPositions(vessel.transform.position, vessel.transform.position + 1.5f * terrainAlertDetectionRadius * (vessel.srf_vel_direction - relativeVelocityRightDirection).normalized, 1, Color.grey);
                BDGUIUtils.DrawLineBetweenWorldPositions(vessel.transform.position, vessel.transform.position + 1.5f * terrainAlertDetectionRadius * (vessel.srf_vel_direction + relativeVelocityRightDirection).normalized, 1, Color.grey);
            }
        }
    }
}