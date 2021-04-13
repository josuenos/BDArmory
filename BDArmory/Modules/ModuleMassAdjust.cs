using BDArmory.Control;
using BDArmory.UI;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace BDArmory.Modules
{
    public class ModuleMassAdjust : PartModule, IPartMassModifier
    {
        public float GetModuleMass(float baseMass, ModifierStagingSituation situation) => massMod;
        public ModifierChangeWhen GetModuleMassChangeWhen() => ModifierChangeWhen.CONSTANTLY;

        public float massMod = 0f; //mass to add to part, in tons
        public float duration = 15; //duration of effect, in seconds
        //change both^^ to BDArmorySettings?
        private float startMass = 0;
        private float oldmassMod;
        private bool hasSetup = false;

        private void EndEffect()
        {
            massMod = 0;
            part.RemoveModule(this);
            //Debug.Log("[BDArmory.ModuleMassAdjust]: ME field expired, " + this.part.name + "mass: " + this.part.mass);
        }

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            if (BDArmorySetup.GameIsPaused) return;

            duration -= 1 * TimeWarp.fixedDeltaTime;

            if (duration <= 0)
            {
                EndEffect();
            }
            if (!hasSetup)
            {
                SetupME();
            }
            if (massMod != oldmassMod)
            {
                if (massMod < 0) //for negative mass modifier - i.e. MassEffect sytyle antigrav/weight reduction
                {
                    massMod = Mathf.Clamp(massMod, (-startMass * 0.95f), Mathf.Infinity); //clamp mod mass to min of 5% of original value to prevent negative mass and whatever Kraken that summons
                }
				oldmassMod = massMod;
                //Debug.Log("[BDArmory.ModuleMassAdjust]: Applying additional ME field to " + this.part.name + ", orig mass: " + startMass + ", massMod = " + massMod);
            }
        }
        
        private void SetupME()
        {
            startMass = this.part.mass;
            hasSetup = true;
            Debug.Log("[BDArmory.ModuleMassAdjust]: Applying ME field to " + this.part.name + ", orig mass: " + startMass + ", massMod = " + massMod);
        }
    }
}
