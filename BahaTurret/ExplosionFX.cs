using System;
using UnityEngine;

namespace BahaTurret
{
	public class ExplosionFX : MonoBehaviour
	{
		KSPParticleEmitter[] pEmitters;
		Light lightFX;
		float startTime;
		public AudioClip exSound;
		AudioSource audioSource;
		float maxTime = 0;
		
		
		
		
		void Start()
		{
			startTime = Time.time;
			pEmitters = gameObject.GetComponentsInChildren<KSPParticleEmitter>();
			foreach(KSPParticleEmitter pe in pEmitters)
			{
				pe.emit = true;	
				if(!pe.useWorldSpace) pe.force = (4.49f * FlightGlobals.getGeeForceAtPosition(transform.position));
				if(pe.maxEnergy > maxTime)
				{
					maxTime = pe.maxEnergy;	
				}
			}
			lightFX = gameObject.AddComponent<Light>();
			lightFX.color = Misc.ParseColor255("255,238,184,255");
			lightFX.intensity = 8;
			lightFX.range = 50;
			
			
			audioSource = gameObject.AddComponent<AudioSource>();
			audioSource.minDistance = 20;
			audioSource.maxDistance = 1000;
			audioSource.volume = Mathf.Sqrt(GameSettings.SHIP_VOLUME);
			
			audioSource.PlayOneShot(exSound);
		}
		
		
		void FixedUpdate()
		{
			lightFX.intensity -= 12 * Time.fixedDeltaTime;
			if(Time.time-startTime > 0.2f)
			{
				foreach(KSPParticleEmitter pe in pEmitters)
				{
					pe.emit = false;	
				}
				
				
			}
			if(Time.time-startTime > maxTime)
			{
				GameObject.Destroy(gameObject);	
			}
		}
		
		
		/* explosion sizes:
		 * 1: small, regular sound (like missiles and rockets)
		 * 2: large, regular sound (like mk82 bomb)
		 * 3: small, pop sound (like cluster submunition)
		 */
		public static void CreateExplosion(Vector3 position, int size, float radius, float power, Vessel sourceVessel)
		{
			GameObject go;
			AudioClip soundClip;
			if(size == 2)
			{
				go = GameDatabase.Instance.GetModel("BDArmory/Models/explosion/explosionLarge");
				soundClip = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/explode1");
			}
			else if(size == 3)
			{
				go = GameDatabase.Instance.GetModel("BDArmory/Models/explosion/explosion");
				soundClip = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/subExplode");
			}
			else
			{
				go = GameDatabase.Instance.GetModel("BDArmory/Models/explosion/explosion");
				soundClip = GameDatabase.Instance.GetAudioClip("BDArmory/Sounds/explode1");
			}
			Quaternion rotation = Quaternion.LookRotation(FlightGlobals.getUpAxis());
			GameObject newExplosion = (GameObject)	GameObject.Instantiate(go, position, rotation);
			newExplosion.SetActive(true);
			newExplosion.AddComponent<ExplosionFX>();
			newExplosion.GetComponent<ExplosionFX>().exSound = soundClip;
			foreach(KSPParticleEmitter pe in newExplosion.GetComponentsInChildren<KSPParticleEmitter>())
			{
				pe.emit = true;	
			}
			
			RaycastHit[] hits = Physics.SphereCastAll(position, radius, FlightGlobals.getUpAxis(), 1, 557057);
			foreach(RaycastHit hitExplosion in hits)
			{
				//hitting parts
				Part explodePart = null;
				try
				{
					explodePart = Part.FromGO(hitExplosion.rigidbody.gameObject);
				}catch(NullReferenceException){}
				if(explodePart!=null && !explodePart.partInfo.name.Contains("Strut"))
				{
					
					if(!MissileLauncher.CheckIfMissile(explodePart))
					{
						float random = UnityEngine.Random.Range(0f,100f);
						float chance = (radius-Vector3.Distance(explodePart.transform.position, position))/radius * 2 * 100;
						chance *= 0.75f;
						if(random < chance) explodePart.temperature = explodePart.maxTemp+500;
						else
						{
							explodePart.rigidbody.AddExplosionForce(power, position, radius, 0, ForceMode.Impulse);	
						}
					}
					else if(MissileLauncher.CheckIfMissile(explodePart) && (explodePart.GetComponent<MissileLauncher>().sourceVessel != sourceVessel || explodePart.GetComponent<MissileLauncher>().sourceVessel==null))
					{
						explodePart.GetComponent<MissileLauncher>().Detonate();
					}
					
					if(MissileLauncher.CheckIfMissile(explodePart))
					{
						Debug.Log ("Explosion hit missile. Missile source: "+explodePart.GetComponent<MissileLauncher>().sourceVessel+", explosionSource: "+sourceVessel);	
					}
				}
				else
				{
				
					//hitting buildings
					DestructibleBuilding hitBuilding = null;
					try{
						hitBuilding = hitExplosion.collider.gameObject.GetComponentUpwards<DestructibleBuilding>();
					}
					catch(NullReferenceException){}
					if(hitBuilding!=null && hitBuilding.IsIntact)
					{
						float damageToBuilding = (power*radius/Vector3.Distance(hitExplosion.point, position)) * 5f;
						if(damageToBuilding > hitBuilding.impactMomentumThreshold/10) hitBuilding.AddDamage(damageToBuilding);
						if(hitBuilding.Damage > hitBuilding.impactMomentumThreshold) hitBuilding.Demolish();
						if(BDArmorySettings.DRAW_DEBUG_LINES) Debug.Log("explosion hit destructible building! Damage: "+(damageToBuilding).ToString("0.00")+ ", total Damage: "+hitBuilding.Damage);
					}
				}
				
			}
			
		}
	}
}

