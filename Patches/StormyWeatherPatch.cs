
using HarmonyLib;
using System;
using System.Reflection;
using System.Runtime;
using UnityEngine;
using UnityEngine.AI;
using BepInEx.Logging;
using GameNetcodeStuff;
using System.Linq;

namespace BetterLightning.Patches
{
    static class Util
    {
        public static bool StartParticleEffect(ref StormyWeather _instance, ref GameObject warningObject, ref Vector3 currentRandomStrikePos, ref float randomThunderTime, ref ParticleSystem particles)
        {
            if (warningObject == null)
            {
                warningObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                warningObject.name = "LightningSpot";
            }

            warningObject.transform.localPosition = currentRandomStrikePos;
            warningObject?.SetActive(true);

            float particleTime = MathF.Abs(TimeOfDay.Instance.globalTime - randomThunderTime);

            var newshape = particles.shape;
            newshape.meshRenderer = warningObject?.GetComponent<MeshRenderer>();

            particles.transform.localPosition = currentRandomStrikePos;
            particles.transform.localPosition += new Vector3(0, 0.01f, 0);

            float AudioLength = _instance.staticElectricityAudio.length;

            if (particleTime <= AudioLength)
            {
                StartAudioAndParticles(AudioLength - particleTime, ref particles, ref _instance);
                return true;
            }

            return false;
        }

        public static void StartAudioAndParticles(float offset, ref ParticleSystem particles, ref StormyWeather _instance)
        {
            particles.time = offset + 2.0f;
            particles.Play();
            particles.time = offset + 2.0f;

            particles.gameObject.GetComponent<AudioSource>().clip = _instance.staticElectricityAudio;
            particles.gameObject.GetComponent<AudioSource>().loop = false;
            particles.gameObject.GetComponent<AudioSource>().time = offset +2.0f;
            particles.gameObject.GetComponent<AudioSource>().Play();
        }

    }

    [HarmonyPatch(typeof(StormyWeather))]
    internal class StormyWeatherPatch
    {
        public static Vector3 lastRandomStrikePos = Vector3.zero;
        public static Vector3 currentRandomStrikePos = Vector3.zero;
        public static float randomThunderTime = 0.0f;
        public static bool generatedWarning = false;
        public static GameObject warningObject = null;
        public static float AudioTimer = -1.0f;

        public static ParticleSystem RandomLightningParticle = null;
        public static GameObject ParticleObject = null;

        //Override the base random function
        [HarmonyPatch("OnEnable")]
        [HarmonyPostfix]
        static void OnEnable(ref StormyWeather __instance)
        {
            generatedWarning = false;
            if (warningObject != null)
                GameObject.Destroy(warningObject);

            if(ParticleObject == null)
            {
                ParticleObject = GameObject.Instantiate<GameObject>(__instance.staticElectricityParticle.gameObject);
                ParticleObject.name = "RandomLightningParticles";

                RandomLightningParticle = ParticleObject.GetComponent<ParticleSystem>();
            }

            randomThunderTime = 0;
        }

        [HarmonyPatch("OnDisable")]
        [HarmonyPostfix]
        static void OnDisable()
        {
            if(warningObject != null)
                GameObject.Destroy(warningObject);

            if (ParticleObject != null)
            {
                GameObject.Destroy(ParticleObject);
                RandomLightningParticle = null;
            }
        }

        [HarmonyPatch("LightningStrikeRandom")]
        [HarmonyPrefix]
        static bool patch_Pre_LightningStrikeRandom(ref StormyWeather __instance, ref float ___timeAtLastStrike, ref GameObject[] ___outsideNodes, ref NavMeshHit ___navHit)
        {
            Vector3 vector;

            FieldInfo temp = AccessTools.Field(typeof(StormyWeather), "seed");
            System.Random seed = (System.Random)temp.GetValue(__instance);

            if (seed.Next(0, 100) < 60 && (randomThunderTime - ___timeAtLastStrike) * (float)TimeOfDay.Instance.currentWeatherVariable < 3f)
            {
                vector = currentRandomStrikePos;
            }
            else
            {
                int num = seed.Next(0, ___outsideNodes.Length);
                if (___outsideNodes == null || ___outsideNodes[num] == null)
                {
                    ___outsideNodes = (from x in GameObject.FindGameObjectsWithTag("OutsideAINode")
                                       orderby x.transform.position.x + x.transform.position.z
                                       select x).ToArray<GameObject>();
                }
                vector = ___outsideNodes[num].transform.position;
                vector = RoundManager.Instance.GetRandomNavMeshPositionInBoxPredictable(vector, 15f, ___navHit, seed, -1);
            }

            currentRandomStrikePos = vector;
            generatedWarning = true;

            return false;
        }

        [HarmonyPatch("DetermineNextStrikeInterval")]
        [HarmonyPrefix]
        static void patch_Pre_DetermineNextStrikeInterval(ref float ___randomThunderTime)
        {
            ___randomThunderTime = randomThunderTime;
        }

        [HarmonyPatch("DetermineNextStrikeInterval")]
        [HarmonyPostfix]
        static void patch_Post_DetermineNextStrikeInterval(ref float ___randomThunderTime)
        {
            randomThunderTime = ___randomThunderTime;
            ___randomThunderTime = TimeOfDay.Instance.globalTime + 1f;
        }

        static bool StartedWarning = false;
        //Override the function 
        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static void patch_Pre_Update(ref StormyWeather __instance, ref float ___randomThunderTime)
        {

            if (!__instance.gameObject.activeInHierarchy)
                return;

            //Keeps the function from calling lightning random strike on its own
            ___randomThunderTime = TimeOfDay.Instance.globalTime + 1f;

            //Determine the next lightning spot and time
            if (!generatedWarning)
            {
                AccessTools.Method(typeof(StormyWeather), "DetermineNextStrikeInterval").Invoke(__instance, null);
                AccessTools.Method(typeof(StormyWeather), "LightningStrikeRandom").Invoke(__instance, null);
                StartedWarning = Util.StartParticleEffect(ref __instance, ref warningObject, ref currentRandomStrikePos, ref randomThunderTime, ref RandomLightningParticle);
                AudioTimer = __instance.staticElectricityAudio.length;
            }

            if (!StartedWarning)
            {
                float particleTime = MathF.Abs(TimeOfDay.Instance.globalTime - randomThunderTime);
                if (particleTime <= AudioTimer)
                {
                    Util.StartAudioAndParticles(AudioTimer - particleTime, ref RandomLightningParticle, ref __instance);
                    StartedWarning = true;
                }
            }

            //When we hit the time, yeet that shit
            if (TimeOfDay.Instance.globalTime > randomThunderTime)
            {
                __instance.LightningStrike(currentRandomStrikePos, false);
                generatedWarning = false;

                //Cleanup Here
                RandomLightningParticle.Stop();
                RandomLightningParticle.gameObject.GetComponent<AudioSource>().Stop();
                warningObject?.SetActive(false);
            }
        }

    }

}
