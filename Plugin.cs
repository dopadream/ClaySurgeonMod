﻿using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using BepInEx.Configuration;
using BarberFixes;
using BepInEx.Bootstrap;
using System.Linq;

namespace ClaySurgeonMod
{
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInDependency("butterystancakes.lethalcompany.ventspawnfix")]
    [BepInDependency("butterystancakes.lethalcompany.barberfixes")]
    [BepInDependency(LETHAL_CONFIG, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string PLUGIN_GUID = "dopadream.lethalcompany.ClaySurgeonMod", PLUGIN_NAME = "Clay Surgeon", PLUGIN_VERSION = "1.3.2", LETHAL_CONFIG = "ainavt.lc.lethalconfig";
        internal static new ManualLogSource Logger;
        internal static GameObject clayPrefab;
        internal static TerminalNode clayNode;
        internal static EnemyType dummyType, curveDummyType;
        internal static Dictionary<string, EnemyType> allEnemiesList = [];
        internal static ConfigEntry<bool> configSpawnOverride, configInfestations, configCurve, configKlayWorld;
        internal static ConfigEntry<int> configMaxCount, configPowerLevel, configSpawnInGroupsOf;
        internal static ConfigEntry<float> configAmbience, configIridescence, configMinVisibility, configMaxVisibility;
        internal static ConfigEntry<float> configSkin0, configSkin1, configSkin2, configSkin3, configSkin4, configSkin5, configSkin6, configSkin7, configSkin8;
        internal static System.Random clayRandom;
        static Dictionary<Texture, IntWithRarity[]> clayWeightList = [];
        internal static Texture claySkinPurple, claySkinRed, claySkinGreen, claySkinYellow, claySkinOrange, claySkinWhite, claySkinBlack, claySkinPink, claySkinTeal;

        protected const string anchorPath = "MeshContainer";
        protected const string animPath = "MeshContainer/AnimContainer";

        internal void initLethalConfig()
        {
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.BoolCheckBoxConfigItem(configSpawnOverride, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configMaxCount, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configSpawnInGroupsOf, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.IntInputFieldConfigItem(configPowerLevel, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configMinVisibility, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configMaxVisibility, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.BoolCheckBoxConfigItem(configInfestations, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.BoolCheckBoxConfigItem(configCurve, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configAmbience, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configIridescence, false));

            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin0, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin1, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin2, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin3, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin4, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin5, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin6, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin7, false));
            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.FloatSliderConfigItem(configSkin8, false));


            LethalConfig.LethalConfigManager.AddConfigItem(new LethalConfig.ConfigItems.BoolCheckBoxConfigItem(configKlayWorld, false));

            LethalConfig.LethalConfigManager.SkipAutoGen();
        }

        void Awake()
        {
            Logger = base.Logger;



            configSpawnOverride = Config.Bind("General", "Override Spawn Settings", true,
                new ConfigDescription("Overrides spawning logic of Clay Surgeons (Barbers). With this enabled, they will spawn in pairs and be more common. Disable if you want to customize their spawning yourself through plugins such as LethalQuantities."));

            configMaxCount = Config.Bind("General", "Max Spawn Count", 6,
                new ConfigDescription("Defines the max spawn count of Clay Surgeons. Override Spawn Settings must be turned on."));

            configSpawnInGroupsOf = Config.Bind("General", "Spawn Group Count", 2,
                new ConfigDescription("Defines how many Clay Surgeons spawn in one vent. Override Spawn Settings must be turned on."));

            configPowerLevel = Config.Bind("General", "Power Level", 2,
                new ConfigDescription("Defines the power level of Clay Surgeons. Override Spawn Settings must be turned on."));

            configMinVisibility = Config.Bind("General", "Minimum Visibility Distance", 5f,
                new ConfigDescription(
                "Controls the distance at which the Clay Surgeon is fully visible.",
                new AcceptableValueRange<float>(5.0f, 15.0f)));

            configMaxVisibility = Config.Bind("General", "Maximum Visibility Distance", 15f,
                new ConfigDescription(
                "Controls the distance at which the Clay Surgeon is fully invisible.",
                new AcceptableValueRange<float>(7.0f, 15.0f)));

            configInfestations = Config.Bind("General", "Clay Infestations", true,
                new ConfigDescription("Adds a chance for Hoarding bug/Nutcracker infestations to be Clay Surgeon Infestations instead."));

            configCurve = Config.Bind("General", "Curved Acceleration", true,
                new ConfigDescription("Makes the Clay Surgeon accelerate at a curve rather than a flat rate per hour."));

            configAmbience = Config.Bind("Aesthetics", "Proximity Ambience Volume", 0.35f,
                new ConfigDescription(
                    "Controls the volume of the Clay Surgeon's proximity ambience.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            configIridescence = Config.Bind("Aesthetics", "Iridescence", 0.25f,
                new ConfigDescription(
                    "Controls the iridescence of the Clay Surgeon's clay material.",
                    new AcceptableValueRange<float>(0.0f, 1.0f)));

            configSkin0 = Config.Bind("Skins", "Default", 300.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin1 = Config.Bind("Skins", "Red Delicious", 300.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin2 = Config.Bind("Skins", "Eraser Pink", 0.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin3 = Config.Bind("Skins", "Snipsy Blue", 0.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin4 = Config.Bind("Skins", "Slimy Green", 300.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin5 = Config.Bind("Skins", "Taffy Yellow", 0.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin6 = Config.Bind("Skins", "Tan Orange", 0.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin7 = Config.Bind("Skins", "Isolated White", 0.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configSkin8 = Config.Bind("Skins", "Ink Black", 0.0f,
                new ConfigDescription(
                    "Controls the rarity of this skin.",
                    new AcceptableValueRange<float>(0.0f, 300.0f)));

            configKlayWorld = Config.Bind("Fun", "Klay World", false,
                new ConfigDescription("Guarantees clay infestations when possible. Clay infestations must be turned on!"));



            if (Chainloader.PluginInfos.ContainsKey(LETHAL_CONFIG))
            {
                initLethalConfig();
            }

            //Credits to ButteryStancakes for asset loading code!

            try
            {
                AssetBundle claysurgeonbundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "claysurgeonmod"));
                clayPrefab = claysurgeonbundle.LoadAsset("ClaySurgeonNew", typeof(GameObject)) as GameObject;
                clayNode = claysurgeonbundle.LoadAsset("ClaySurgeonFile", typeof(TerminalNode)) as TerminalNode;
                dummyType = claysurgeonbundle.LoadAsset("DummyEnemyType", typeof(EnemyType)) as EnemyType;
                curveDummyType = claysurgeonbundle.LoadAsset("CurveDummyType", typeof(EnemyType)) as EnemyType;

                claySkinPurple = claysurgeonbundle.LoadAsset("cs_default", typeof(Texture)) as Texture;
                claySkinRed = claysurgeonbundle.LoadAsset("cs_red_delicious", typeof(Texture)) as Texture;
                claySkinPink = claysurgeonbundle.LoadAsset("cs_eraser_pink", typeof(Texture)) as Texture;
                claySkinTeal = claysurgeonbundle.LoadAsset("cs_snipsy_blue", typeof(Texture)) as Texture;
                claySkinGreen = claysurgeonbundle.LoadAsset("cs_slimy_green", typeof(Texture)) as Texture;
                claySkinYellow = claysurgeonbundle.LoadAsset("cs_taffy_yellow", typeof(Texture)) as Texture;
                claySkinOrange = claysurgeonbundle.LoadAsset("cs_tan_orange", typeof(Texture)) as Texture;
                claySkinWhite = claysurgeonbundle.LoadAsset("cs_isolated_white", typeof(Texture)) as Texture;
                claySkinBlack = claysurgeonbundle.LoadAsset("cs_shadow_black", typeof(Texture)) as Texture;

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
                if ((num && random2.Next(0, 210) < 3) || random2.Next(0, 1000) < 15 || Plugin.configKlayWorld.Value)
                {
                    if (___enemyRushIndex == -1 && Plugin.configInfestations.Value)
                    {
                        for (int j = 0; j < __instance.currentLevel.Enemies.Count; j++)
                        {
                            if (__instance.currentLevel.Enemies[j].enemyType.enemyName == "Clay Surgeon")
                            {
                                if (__instance.currentLevel.Enemies[j].enemyType.MaxCount > 1)
                                {
                                    Logger.LogDebug("Clay infestation started!");
                                    ((Component)(object)__instance.indoorFog).gameObject.SetActive(random2.Next(0, 100) < 20);
                                    ___enemyRushIndex = j;
                                    __instance.currentMaxInsidePower = __instance.currentLevel.Enemies[j].enemyType.PowerLevel * __instance.currentLevel.Enemies[j].enemyType.MaxCount;
                                    break;
                                }
                            }
                        }
                    }
                }
                clayRandom = new System.Random(StartOfRound.Instance.randomMapSeed);
            }


            static Texture getRandomSkin(System.Random random)
            {

                clayWeightList.Clear();




                int percent = random.Next(0, 300);


                var configValues = new[]
                {
                    (int)configSkin0.Value,
                    (int)configSkin1.Value,
                    (int)configSkin2.Value,
                    (int)configSkin3.Value,
                    (int)configSkin4.Value,
                    (int)configSkin5.Value,
                    (int)configSkin6.Value,
                    (int)configSkin7.Value,
                    (int)configSkin8.Value
                };

                var claySkins = new[]
                {
                    claySkinPurple,
                    claySkinRed,
                    claySkinPink,
                    claySkinTeal,
                    claySkinGreen,
                    claySkinYellow,
                    claySkinOrange,
                    claySkinWhite,
                    claySkinBlack
                };

                List<int> matchingIndices = new List<int>();

                for (int i = 0; i < configValues.Length; i++)
                {
                    if (configValues[i] != 0 && percent <= configValues[i])
                    {
                        matchingIndices.Add(i);
                    }
                }

                if (matchingIndices.Count > 0)
                {
                    return claySkins[RoundManager.Instance.GetRandomWeightedIndex(configValues.ToArray(), random)];
                }

                // Default return value if no condition is met
                return claySkinPurple;
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


                if (Plugin.configSpawnOverride.Value)
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
                                spawnableEnemyWithRarity.enemyType.spawnInGroupsOf = configSpawnInGroupsOf.Value;
                                spawnableEnemyWithRarity.enemyType.MaxCount = configMaxCount.Value;
                                spawnableEnemyWithRarity.enemyType.PowerLevel = configPowerLevel.Value;
                                spawnableEnemyWithRarity.enemyType.probabilityCurve = dummyType.probabilityCurve;
                                spawnableEnemyWithRarity.enemyType.numberSpawnedFalloff = dummyType.numberSpawnedFalloff;
                                spawnableEnemyWithRarity.enemyType.useNumberSpawnedFalloff = true;
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
                __instance.skinnedMeshRenderers = clayClone.gameObject.GetComponentsInChildren<SkinnedMeshRenderer>();
                __instance.meshRenderers = clayClone.gameObject.GetComponentsInChildren<MeshRenderer>();
                __instance.skin.sharedMaterials[0].mainTexture = getRandomSkin(clayRandom);

                clayClone.GetComponentInChildren<EnemyAnimationEvent>().mainScript = __instance.gameObject.GetComponentInChildren<EnemyAnimationEvent>().mainScript;


            }

            [HarmonyPatch(typeof(ClaySurgeonAI), "Start")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostStart(ClaySurgeonAI __instance)
            {
                Material[] barberMats = new Material[__instance.skin.sharedMaterials.Length];
                for (int i = 0; i < barberMats.Length; i++)
                    barberMats[i] = Instantiate(__instance.skin.materials[i]);
                __instance.skin.sharedMaterials = barberMats;

            }

            [HarmonyPatch(typeof(ClaySurgeonAI), "Update")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostUpdate(ClaySurgeonAI __instance)
            {
                //Logger.LogDebug("Speed: " + __instance.currentInterval);
                AudioSource[] sources = __instance.gameObject.GetComponentsInChildren<AudioSource>();
                foreach (AudioSource source in sources)
                {
                    if (source.clip.name == "ClaySurgeonAmbience")
                    {
                        source.volume = Plugin.configAmbience.Value;
                        return;
                    }
                }
            }

            [HarmonyPatch(typeof(ClaySurgeonAI), "SetVisibility")]
            [HarmonyPostfix]

            static void ClaySurgeonAIPostSetVis(ClaySurgeonAI __instance)
            {
                float num = Vector3.Distance(StartOfRound.Instance.audioListener.transform.position, __instance.transform.position + Vector3.up * 0.7f);
                __instance.minDistance = configMinVisibility.Value;
                __instance.maxDistance = configMaxVisibility.Value;
                Material[] barberMats = __instance.skin.sharedMaterials;
                foreach (Material barberMat in barberMats)
                    barberMat.SetFloat("_AlphaCutoff", (num - __instance.minDistance) / (__instance.maxDistance - __instance.minDistance));
                __instance.skin.material.SetFloat("_IridescenceMask", Plugin.configIridescence.Value);
                __instance.skin.sharedMaterials = barberMats;
            }


            [HarmonyPatch(typeof(DanceClock), nameof(DanceClock.Tick))]
            [HarmonyPrefix]
            static bool DanceClockPreTick()
            {
                if (Plugin.configCurve.Value)
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

        /*        public abstract class BaseSkin : Skin
                {

                    [SerializeField]
                    protected string label;
                    public string Label => label;
                    [SerializeField]
                    protected string id;
                    public string Id => id;
                    [SerializeField]
                    protected Texture2D icon;
                    public Texture2D Icon => icon;
                    public abstract string EnemyId { get; }
                    public abstract Skinner CreateSkinner();
                }

                public abstract class BaseNestSkin : BaseSkin, NestSkin
                {
                    public string SkinId => id;

                    public abstract Skinner CreateNestSkinner();
                }


                public abstract class ClaySkinner : Skinner
                {
                    public void Apply(GameObject enemy)
                    {
                        throw new NotImplementedException();
                    }

                    public void Remove(GameObject enemy)
                    {
                        throw new NotImplementedException();
                    }
                }*/
    }
}