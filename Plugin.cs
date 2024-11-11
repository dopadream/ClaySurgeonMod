using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ClaySurgeonMod
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "dopadream.lethalcompany.ClaySurgeonMod", PLUGIN_NAME = "Clay Surgeon", PLUGIN_VERSION = "1.0.0";
        internal static new ManualLogSource Logger;
        internal static GameObject clayPrefab;
        internal static GameObject barberPrefab;
        protected const string anchorPath = "MeshContainer";
        protected const string animPath = "MeshContainer/AnimContainer";


        void Awake()
        {
            Logger = base.Logger;


            //Credits to ButteryStancakes for asset loading code!

            try
            {
                AssetBundle claysurgeonbundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "claysurgeonmod"));
                clayPrefab = claysurgeonbundle.LoadAsset("ClaySurgeonNew", typeof(GameObject)) as GameObject;
                claysurgeonbundle.Unload(false);
            }
            catch
            {
                Logger.LogError("Encountered some error loading asset bundle. Did you install the plugin correctly?");
                return;
            }


            new Harmony(PLUGIN_GUID).PatchAll();

            Logger.LogInfo($"{PLUGIN_NAME} v{PLUGIN_VERSION} loaded");
        }

        [HarmonyPatch]
        class BarberMaterialTweaksPatches
        {

            [HarmonyPatch(typeof(ClaySurgeonAI), "Awake")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostWake(ClaySurgeonAI __instance)
            {
                Destroy(__instance.creatureAnimator.gameObject);
                Transform clayClone = Instantiate(clayPrefab, __instance.transform.position, __instance.transform.rotation, __instance.transform.Find(anchorPath)).transform;
                __instance.creatureAnimator = clayClone.GetComponentInChildren<Animator>();
                __instance.skin = clayClone.GetComponentInChildren<SkinnedMeshRenderer>();
                __instance.gameObject.GetComponentInChildren<ScanNodeProperties>().headerText = "Clay Surgeon";
                clayClone.GetComponentInChildren<EnemyAnimationEvent>().mainScript = __instance.gameObject.GetComponentInChildren<EnemyAnimationEvent>().mainScript;
            }

            [HarmonyPatch(typeof(ClaySurgeonAI), "Start")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostStart(ClaySurgeonAI __instance)
            {
                Material[] barberMats = new Material[__instance.skin.sharedMaterials.Length];
                for (int i = 0; i < barberMats.Length; i++)
                    barberMats[i] = __instance.skin.materials[i];
                    __instance.skin.sharedMaterials = barberMats;
       
            }

       
            [HarmonyPatch(typeof(ClaySurgeonAI), "SetVisibility")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostSetVis(ClaySurgeonAI __instance)
            {
                float num = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, __instance.transform.position + Vector3.up * 0.7f);

                Material[] barberMats = __instance.skin.sharedMaterials;
                foreach (Material barberMat in barberMats)
                    barberMat.SetFloat("_AlphaCutoff", (num - __instance.minDistance) / (__instance.maxDistance - __instance.minDistance));
                    __instance.skin.sharedMaterials = barberMats;
            }
        }
    }
}