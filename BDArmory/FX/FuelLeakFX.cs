using BDArmory.Misc;
using BDArmory.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace BDArmory.FX
{
    class FuelLeakFX : MonoBehaviour
    {
        Part parentPart;
        public static ObjectPool CreateLeakFXPool(string modelPath)
        {
            var template = GameDatabase.Instance.GetModel(modelPath);
            var decal = template.AddComponent<FuelLeakFX>();
            template.SetActive(false);
            return ObjectPool.CreateObjectPool(template, 10, true, true);
        }

        public float lifeTime = 20;
        private float startTime;
        private float disableTime = -1;
        private float _highestEnergy = 1;
        KSPParticleEmitter[] pEmitters;
        void OnEnable()
        {
            BDArmorySetup.numberOfParticleEmitters++;
            startTime = Time.time;
            pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();

            using (var pe = pEmitters.AsEnumerable().GetEnumerator())
                while (pe.MoveNext())
                {
                    if (pe.Current == null) continue;

                    pe.Current.emit = true;
                    _highestEnergy = pe.Current.maxEnergy;
                    EffectBehaviour.AddParticleEmitter(pe.Current);
                }
        }
        void onDisable()
        {
            BDArmorySetup.numberOfParticleEmitters--;
            foreach (var pe in pEmitters)
                if (pe != null)
                {
                    pe.emit = false;
                    EffectBehaviour.RemoveParticleEmitter(pe);
                }
        }
        void Update()
        {
            if (!gameObject.activeInHierarchy || !HighLogic.LoadedSceneIsFlight)
            {
                return;
            }
            transform.rotation = Quaternion.FromToRotation(Vector3.up, -FlightGlobals.getGeeForceAtPosition(transform.position));
            if (Time.time - startTime >= lifeTime && disableTime < 0)
            {
                disableTime = Time.time; //grab time when emission stops
                foreach (var pe in pEmitters)
                    if (pe != null)
                        pe.emit = false;
            }
            if (disableTime > 0 && Time.time - disableTime > _highestEnergy) //wait until last emitted particle has finished
            {
                gameObject.SetActive(false);
            }
        }
        public void AttachAt(Part hitPart, RaycastHit hit, Vector3 offset)
        {
            parentPart = hitPart;
            transform.SetParent(hitPart.transform);
            transform.position = hit.point + offset;
            transform.rotation = Quaternion.FromToRotation(Vector3.up, -FlightGlobals.getGeeForceAtPosition(transform.position));
            parentPart.OnJustAboutToDie += OnParentDestroy;
            parentPart.OnJustAboutToBeDestroyed += OnParentDestroy;
            gameObject.SetActive(true);
        }
        public void OnParentDestroy()
        {
            if (parentPart)
            {
                parentPart.OnJustAboutToDie -= OnParentDestroy;
                parentPart.OnJustAboutToBeDestroyed -= OnParentDestroy;
                parentPart = null;
                transform.parent = null;
                gameObject.SetActive(false);
            }
        }
        public void OnDestroy()
        {
            OnParentDestroy();
        }
    }
}
