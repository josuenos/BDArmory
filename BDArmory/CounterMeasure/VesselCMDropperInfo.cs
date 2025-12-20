using BDArmory.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BDArmory.CounterMeasure
{
    public class VesselCMDropperInfo : MonoBehaviour
    {
        List<CMDropper> droppers = new List<CMDropper>();
        public Vessel vessel;
        public Dictionary<CMDropper.CountermeasureTypes, int> cmCounts = new Dictionary<CMDropper.CountermeasureTypes, int>();
        public Dictionary<CMDropper.CountermeasureTypes, int> cmMaxCounts = new Dictionary<CMDropper.CountermeasureTypes, int>();
        public Dictionary<CMDropper.CountermeasureTypes, bool> hasCMGauge = new Dictionary<CMDropper.CountermeasureTypes, bool>();        
        bool cleaningRequired = false;
		
        void Start()
        {
            if (!Setup())
            {
                Destroy(this);
                return;
            }
            vessel.OnJustAboutToBeDestroyed += AboutToBeDestroyed;
            GameEvents.onVesselPartCountChanged.Add(OnVesselPartCountChanged);
            GameEvents.onVesselChange.Add(OnVesselSwitched);
            if (vessel.isActiveVessel)
                 StartCoroutine(DelayedCleanListRoutine());
            else cleaningRequired = true;
        }


        bool Setup()
        {
            if (!HighLogic.LoadedSceneIsFlight) return false;
            if (!vessel) vessel = GetComponent<Vessel>();
            if (!vessel)
            {
                Debug.Log("[BDArmory.VesselCMDropperInfo]: VesselCMDropperInfo was added to an object with no vessel component");
                return false;
            }
            hasCMGauge = Enum.GetValues(typeof(CMDropper.CountermeasureTypes)).Cast<CMDropper.CountermeasureTypes>().ToDictionary(cm => cm, cm => false);
            cmCounts = Enum.GetValues(typeof(CMDropper.CountermeasureTypes)).Cast<CMDropper.CountermeasureTypes>().ToDictionary(cm => cm, cm => 0);
            cmMaxCounts = Enum.GetValues(typeof(CMDropper.CountermeasureTypes)).Cast<CMDropper.CountermeasureTypes>().ToDictionary(cm => cm, cm => 0);
            return true;
        }

        void OnDestroy()
        {
            if (vessel) vessel.OnJustAboutToBeDestroyed -= AboutToBeDestroyed;
            GameEvents.onVesselPartCountChanged.Remove(OnVesselPartCountChanged);
			GameEvents.onVesselChange.Remove(OnVesselSwitched);
        }

        void AboutToBeDestroyed()
        {
            Destroy(this);
        }

        void OnVesselPartCountChanged(Vessel v)
        {
            if (gameObject.activeInHierarchy && v == vessel && vessel.isActiveVessel)
                StartCoroutine(DelayedCleanListRoutine());
            else cleaningRequired = true;
        }
		void OnVesselSwitched(Vessel v)
		{
			if (gameObject.activeInHierarchy && v == vessel && cleaningRequired)
                CleanList();
		}
        public void AddCMDropper(CMDropper CM)
        {
            if (droppers is null && !Setup())
            {
                Destroy(this);
                return;
            }

            if (!droppers.Contains(CM))
            {
                droppers.Add(CM);
            }

            DelayedCleanList();
        }

        public void RemoveCMDropper(CMDropper CM)
        {
            if (droppers is null && !Setup())
            {
                Destroy(this);
                return;
            }

            droppers.Remove(CM);
            CleanList();
        }

        public void DelayedCleanList()
        {
            StartCoroutine(DelayedCleanListRoutine());
        }
        IEnumerator DelayedCleanListRoutine()
        {
            var wait = new WaitForFixedUpdate();
            yield return wait;
            yield return wait;
            CleanList();
        }

        void CleanList()
        {
            vessel = GetComponent<Vessel>();
            if (!vessel)
            {
                Destroy(this);
            }
            droppers.RemoveAll(j => j == null);
            droppers.RemoveAll(j => j.vessel != vessel); //cull destroyed CM boxes, if any, refresh Gauges on remainder
            cmCounts.Clear();
            cmMaxCounts.Clear();
            foreach (var cmType in Enum.GetValues(typeof(CMDropper.CountermeasureTypes)).Cast<CMDropper.CountermeasureTypes>())
            {
                cmCounts.Add(cmType, 0);
                cmMaxCounts.Add(cmType, 0);
            }
            foreach (CMDropper p in droppers)
            {
                if (p.vessel != this.vessel) continue;
                cmCounts[p.cmType] += p.cmCount;
                cmMaxCounts[p.cmType] += p.maxCMCount;

                if (hasCMGauge[p.cmType] || p.hasGauge)
                {
                    hasCMGauge[p.cmType] = true;
                    continue;
                }
                p.gauge = (BDStagingAreaGauge)p.part.AddModule("BDStagingAreaGauge");
                hasCMGauge[p.cmType] = true;
                p.hasGauge = true;
                p.gauge.AmmoName = p.cmType switch
                {
                    CMDropper.CountermeasureTypes.Flare => "Flares",
                    CMDropper.CountermeasureTypes.Chaff => "Chaff",
                    CMDropper.CountermeasureTypes.Smoke => "Smoke",
                    CMDropper.CountermeasureTypes.Decoy => "Decoys",

                    CMDropper.CountermeasureTypes.Bubbles => "Bubbles",
                    _ => "???"
                };
                /*
                bool addGauge = false;
                switch (p.cmType)
                {
                    case CMDropper.CountermeasureTypes.Flare:
                        {
                            if (!(hasCMGauge[p.cmType] || p.hasGauge))
                                addGauge = true;
                            hasCMGauge[p.cmType] = true;
                        }
                        break;
                    case CMDropper.CountermeasureTypes.Chaff:
                        {
                            if (!(hasCMGauge[p.cmType] || p.hasGauge))
                                addGauge = true;
                            hasCMGauge[p.cmType] = true;
                        }
                        break;
                    case CMDropper.CountermeasureTypes.Smoke:
                        {
                            if (!(hasCMGauge[p.cmType] || p.hasGauge))
                                addGauge = true;
                            hasCMGauge[p.cmType] = true;
                        }
                        break;
                    case CMDropper.CountermeasureTypes.Decoy:
                        {
                            if (!(hasCMGauge[p.cmType] || p.hasGauge))
                                addGauge = true;
                            hasCMGauge[p.cmType] = true;
                        }
                        break;
                    case CMDropper.CountermeasureTypes.Bubbles:
                        {
                            if (!(hasCMGauge[p.cmType] || p.hasGauge))
                                addGauge = true;
                            hasCMGauge[p.cmType] = true;
                        }
                        break;
                }
                if (addGauge)
                {
                    p.gauge = (BDStagingAreaGauge)p.part.AddModule("BDStagingAreaGauge");
                    p.hasGauge = true;
                    hasCMGauge[p.cmType] = true;
                    p.gauge.AmmoName = p.cmType switch
                    {
                        CMDropper.CountermeasureTypes.Flare => "Flares",
                        CMDropper.CountermeasureTypes.Chaff => "Chaff",
                        CMDropper.CountermeasureTypes.Smoke => "Smoke",
                        CMDropper.CountermeasureTypes.Decoy => "Decoys",
                        CMDropper.CountermeasureTypes.Bubbles => "Bubbles",
                        _ => "???"
                    };
                }
                */
            }
            foreach (var cmType in Enum.GetValues(typeof(CMDropper.CountermeasureTypes)).Cast<CMDropper.CountermeasureTypes>())
            {
                hasCMGauge[cmType] = false;
            }
            cleaningRequired = false;
        }
    }
}
