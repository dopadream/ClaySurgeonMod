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
using static UnityEngine.UIElements.UIR.Implementation.UIRStylePainter;
using BarberFixes;

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
        internal static EnemyType dummyType;
        internal static EnemyType curveDummyType;
        internal static Dictionary<string, EnemyType> allEnemiesList = [];
        internal static ConfigEntry<bool> configSpawnOverride;
        internal static ConfigEntry<bool> configInfestations;
        internal static ConfigEntry<bool> configCurve;
        internal static ConfigEntry<float> configAmbience;
        internal static ConfigEntry<float> configIridescence;
        protected const string anchorPath = "MeshContainer";
        protected const string animPath = "MeshContainer/AnimContainer";


        void Awake()
        {
            Logger = base.Logger;



            configSpawnOverride = Config.Bind("General", "Override Spawn Settings", true,
                new ConfigDescription("Overrides spawning logic of Clay Surgeons (Barbers). With this enabled, they will spawn in pairs and be more common. Disable if you want to customize their spawning yourself through plugins such as LethalQuantities."));

            configInfestations = Config.Bind("General", "Clay Infestations", true,
                new ConfigDescription("Adds a chance for Hoarding bug/Nutcracker infestations to be Clay Surgeon Infestations instead."));

            configCurve = Config.Bind("General", "Curved Acceleration", true,
                new ConfigDescription("Makes the Clay Surgeon accelerate at a curve rather than a flat rate per hour."));

            configAmbience = Config.Bind("Aesthetics", "Proximity Ambience Volume", 0.35f,
                new ConfigDescription(
                    "Controls the volume of the Clay Surgeon's proximity ambience.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            configIridescence = Config.Bind("Aesthetics", "Iridescence", 0.125f,
                new ConfigDescription(
                    "Controls the iridescence of the Clay Surgeon's clay material.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));


            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(configSpawnOverride, false));
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(configInfestations, false));
            LethalConfigManager.AddConfigItem(new BoolCheckBoxConfigItem(configCurve, false));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(configAmbience, false));
            LethalConfigManager.AddConfigItem(new FloatSliderConfigItem(configIridescence, false));


            LethalConfigManager.SkipAutoGen();

            //Credits to ButteryStancakes for asset loading code!

            try
            {
                AssetBundle claysurgeonbundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "claysurgeonmod"));
                clayPrefab = claysurgeonbundle.LoadAsset("ClaySurgeonNew", typeof(GameObject)) as GameObject;
                clayNode = claysurgeonbundle.LoadAsset("ClaySurgeonFile", typeof(TerminalNode)) as TerminalNode;
                dummyType = claysurgeonbundle.LoadAsset("DummyEnemyType", typeof(EnemyType)) as EnemyType;
                curveDummyType = claysurgeonbundle.LoadAsset("CurveDummyType", typeof(EnemyType)) as EnemyType;

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
        class ClaySurgeonPatches
        {

            [HarmonyPatch(typeof(RoundManager), "RefreshEnemiesList")]
            [HarmonyPostfix]
            static void RoundManagerPostRefresh(RoundManager __instance, ref int ___enemyRushIndex)
            {
                DateTime dateTime = new DateTime(DateTime.Now.Year, 10, 31);
                bool num = DateTime.Today == dateTime;
                System.Random random2 = new System.Random(StartOfRound.Instance.randomMapSeed + 5781);
                if ((num && random2.Next(0, 210) < 3) || random2.Next(0, 1000) < 15)
                {
                    if (___enemyRushIndex == -1 && configInfestations.Value)
                    {
                        for (int j = 0; j < __instance.currentLevel.Enemies.Count; j++)
                        {
                            if (__instance.currentLevel.Enemies[j].enemyType.enemyName == "Clay Surgeon")
                            {
                                if (__instance.currentLevel.Enemies[j].enemyType.MaxCount > 0)
                                {
                                    Logger.LogDebug("Clay infestation started!");
                                    ((Component)(object)__instance.indoorFog).gameObject.SetActive(random2.Next(0, 100) < 20);
                                    ___enemyRushIndex = j;
                                    __instance.currentMaxInsidePower = 30f;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

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
                        foreach (SpawnableEnemyWithRarity spawnableEnemyWithRarity in enemies)
                        {
                            if (allEnemiesList.ContainsKey(spawnableEnemyWithRarity.enemyType.name))
                            {
                                if (allEnemiesList[spawnableEnemyWithRarity.enemyType.name] == spawnableEnemyWithRarity.enemyType)
                                    Plugin.Logger.LogWarning($"allEnemiesList: Tried to cache reference to \"{spawnableEnemyWithRarity.enemyType.name}\" more than once");
                                else
                                    Plugin.Logger.LogWarning($"allEnemiesList: Tried to cache two different enemies by same name ({spawnableEnemyWithRarity.enemyType.name})");
                            }
                            else if (spawnableEnemyWithRarity.enemyType.enemyName == "Clay Surgeon")
                            {
                                spawnableEnemyWithRarity.enemyType.spawnInGroupsOf = 2;
                                spawnableEnemyWithRarity.enemyType.MaxCount = 8;
                                spawnableEnemyWithRarity.enemyType.probabilityCurve = dummyType.probabilityCurve;
                                spawnableEnemyWithRarity.enemyType.numberSpawnedFalloff = dummyType.numberSpawnedFalloff;
                                spawnableEnemyWithRarity.enemyType.useNumberSpawnedFalloff = true;
                                spawnableEnemyWithRarity.enemyType.numberSpawned = 0;
                                Logger.LogDebug("Barber spawn settings overriden!");
                                return;
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
                Logger.LogDebug("Speed: " + __instance.currentInterval);
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


            [HarmonyPatch(typeof(DanceClock), nameof(DanceClock.Tick))]
            [HarmonyPrefix]
            static bool DanceClockPreTick()
            {
                if (configCurve.Value)
                {
                    float currentInterval = Mathf.Clamp(curveDummyType.probabilityCurve.Evaluate((float)TimeOfDay.Instance.hour / TimeOfDay.Instance.numberOfHours), 1.25f, 2.75f);
                    foreach (ClaySurgeonAI barber in UnityEngine.Object.FindObjectsOfType<ClaySurgeonAI>())
                        barber.currentInterval = currentInterval;

                    // skip the original function
                    return false;
                }

                // just do the original function
                return true;
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
                        foreach (TerminalKeyword keyword in keywords)
                        {
                            if (keyword.word == "barber")
                            {
                                keyword.word = "clay surgeon";
                                return;
                            }
                        }
                        Logger.LogDebug("Barber bestiary entry replaced!");
                        return;
                    }
                }
            }
        }
    }
}