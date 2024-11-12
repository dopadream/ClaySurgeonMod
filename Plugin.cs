using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Video;
using LethalConfig;
using BepInEx.Configuration;
using LethalConfig.ConfigItems.Options;
using LethalConfig.ConfigItems;

namespace ClaySurgeonMod
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency("ainavt.lc.lethalconfig")]
    [BepInDependency("butterystancakes.lethalcompany.ventspawnfix")]
    [BepInDependency("butterystancakes.lethalcompany.barberfixes")]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "dopadream.lethalcompany.ClaySurgeonMod", PLUGIN_NAME = "Clay Surgeon", PLUGIN_VERSION = "1.0.0";
        internal static new ManualLogSource Logger;
        internal static GameObject clayPrefab;
        internal static GameObject barberPrefab;
        internal static TerminalNode clayNode;
        internal static Dictionary<string, EnemyType> allEnemiesList = [];
        internal static ConfigEntry<bool> configSpawnOverride;
        internal static ConfigEntry<float> configAmbience;
        internal static ConfigEntry<float> configIridescence;
        protected const string anchorPath = "MeshContainer";
        protected const string animPath = "MeshContainer/AnimContainer";


        void Awake()
        {
            Logger = base.Logger;



            configSpawnOverride = Config.Bind("General", "Override spawn settings", true,
                new ConfigDescription("Override (force enable) the spawn in pair functionality from BarberFixes."));

            configAmbience = Config.Bind("Aesthetics", "Proximity Ambience Volume", 0.5f,
                new ConfigDescription(
                    "Controls the volume of the Clay Surgeon's proximity ambience.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            configIridescence = Config.Bind("Aesthetics", "Iridescence", 0.125f,
                new ConfigDescription(
                    "Controls the iridescence of the Clay Surgeon's clay material.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));


            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(configSpawnOverride, false));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(configAmbience, false));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(configIridescence, false));


            LethalConfigManager.SkipAutoGen();

            //Credits to ButteryStancakes for asset loading code!

            try
            {
                AssetBundle claysurgeonbundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "claysurgeonmod"));
                clayPrefab = claysurgeonbundle.LoadAsset("ClaySurgeonNew", typeof(GameObject)) as GameObject;
                clayNode = claysurgeonbundle.LoadAsset("ClaySurgeonFile", typeof(TerminalNode)) as TerminalNode;
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

            [HarmonyPatch(typeof(QuickMenuManager), "Start")]
            [HarmonyPostfix]

            static void QuickMenuManagerPostStart(QuickMenuManager __instance)
            {
                allEnemiesList.Clear();
                List<SpawnableEnemyWithRarity>[] allEnemyLists =
                [
                    __instance.testAllEnemiesLevel.Enemies
                ];


                if (configSpawnOverride.Value)
                {
                    foreach (List<SpawnableEnemyWithRarity> enemies in allEnemyLists)
                    {
                        foreach (SpawnableEnemyWithRarity spawnableEnemyWithRarity in enemies)
                        {
                            if (allEnemiesList.ContainsKey(spawnableEnemyWithRarity.enemyType.name) && spawnableEnemyWithRarity.enemyType.name == "Clay Surgeon")
                            {
                                spawnableEnemyWithRarity.enemyType.spawnInGroupsOf = 2;
                                spawnableEnemyWithRarity.enemyType.MaxCount = 8;
                            }
                        }
                    }
                }
            }


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

            [HarmonyPatch(typeof(ClaySurgeonAI), "Update")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostUpdate(ClaySurgeonAI __instance)
            {
               AudioSource[] sources = __instance.gameObject.GetComponentsInChildren<AudioSource>();
               foreach (AudioSource source in sources)
                {
                    if (source.clip.name == "ClaySurgeonAmbience")
                    {
                        source.volume = configAmbience.Value;
                        return;
                    }
                }

            }

            [HarmonyPatch(typeof(ClaySurgeonAI), "SetVisibility")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostSetVis(ClaySurgeonAI __instance)
            {
                float num = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, __instance.transform.position + Vector3.up * 0.7f);

                Material[] barberMats = __instance.skin.sharedMaterials;
                foreach (Material barberMat in barberMats)
                    barberMat.SetFloat("_AlphaCutoff", (num - __instance.minDistance) / (__instance.maxDistance - __instance.minDistance));
                __instance.skin.material.SetFloat("_IridescenceMask", configIridescence.Value);
                __instance.skin.sharedMaterials = barberMats;
            }


            [HarmonyPatch(typeof(Terminal), "Awake")]
            [HarmonyPostfix]

            static void TerminalPostWake(Terminal __instance)
            {
                TerminalKeyword[] keywords = __instance.terminalNodes.allKeywords;
                List<TerminalNode> nodes = __instance.enemyFiles;

                foreach (TerminalNode node in nodes)
                {
                    if (node.creatureFileID == 24)
                    {
                        node.displayVideo = clayNode.displayVideo;
                        node.displayText = clayNode.displayText;
                        node.creatureName = clayNode.creatureName;
                        Logger.LogDebug(node.displayVideo);
                        foreach (TerminalKeyword keyword in keywords)
                        {
                            if (keyword.word == "barber")
                            {
                                keyword.word = "clay surgeon";
                                return;
                            }
                        }
                        return;
                    }
                }
            }
        }
    }
}