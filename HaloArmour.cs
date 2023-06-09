﻿using EFT;
using EFT.Interactive;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using Comfort.Common;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Aki.Reflection.Patching;

namespace HaloArmour
{
    [BepInPlugin("Tyrian.HaloArmour", "HaloArmour", "1.0.0")]
    public class HaloArmour : BaseUnityPlugin
    {
        private static GameWorld gameWorld;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;
        public static Player Player;
        public static HaloArmour instance;
        public static Dictionary<GameObject, HashSet<Material>> objectsMaterials = new Dictionary<GameObject, HashSet<Material>>();
        public static ConfigEntry<bool> applyChangesConfig;
        public static bool applyChanges = false;
        public static ConfigEntry<bool> clearErrantClonesConfig;
        public static bool clearClonesChanges = false;
        public static ConfigEntry<bool> shieldEnabledConfig;
        public static ConfigEntry<int> maxShieldConfig;
        public static ConfigEntry<float> shieldRechargeTimeConfig;
        public static ConfigEntry<float> shieldRechargeWaitTimeConfig;
        public static ConfigEntry<bool> speedChangesConfig;
        public static ConfigEntry<float> speedBuffMultiplierConfig;
        public static ConfigEntry<bool> undersuitArmourConfig;
        public static ConfigEntry<float> undersuitArmourReductionConfig;
        public static ConfigEntry<bool> staminaChangesConfig;
        public static ConfigEntry<float> staminaBuffMultiplierConfig;
        public static ConfigEntry<bool> jumpChangesConfig;
        public static ConfigEntry<float> jumpBuffMultiplierConfig;
        public static ConfigEntry<bool> recoilChangesConfig;
        public static ConfigEntry<float> recoilBuffMultiplierConfig;
        public static ConfigEntry<bool> doorKickerEnabledConfig;
        public static ConfigEntry<bool> radarEnabledConfig;
        public static ConfigEntry<float> radarScaleOffsetConfig;
        public static ConfigEntry<float> radarDistanceScaleOffsetConfig;
        public static ConfigEntry<float> radarHeightThresholdeScaleOffsetConfig;
        public static ConfigEntry<float> radarOffsetYConfig;
        public static ConfigEntry<float> radarOffsetXConfig;
        public static ConfigEntry<float> shieldScaleOffsetConfig;
        public static ConfigEntry<float> shieldOffsetYConfig;
        public static ConfigEntry<float> shieldOffsetXConfig;
        public static ManualLogSource logger;
        public static Renderer[] cachedRenderers;
        public static Material[] materials;
        public static Dictionary<(GameObject, Material), ConfigEntry<Color>> materialColorConfigs = new Dictionary<(GameObject, Material), ConfigEntry<Color>>();
        public static bool isTimerRunning = false;
        public static float delayDuration = 1.0f;
        public static bool sceneLoaded = false;
        public static int colouringRanTimes = 0;
        public static int colouringRanTimesMaxIterations = 64;
        public static string PlayerBody = null;
        public static string PlayerLegs = null;
        public static string PlayerHelmet = null;
        public static bool patchesEnabled = false;
        public static bool HudsEnabled = false;
        public static float playerHeight = 0f;

        public static HaloArmour Instance
        {
            get { return instance; }
        }

        private void Start()
        {
            new VoiceAdd.VoicePatch().Enable();
            new ShieldPatch().Enable();
            new DeadPatch().Enable();
            new HealPatch().Enable();
            new DoorKickPatch().Enable();
        }

        private void Awake()
        {
            var harmony = new Harmony("HaloShield");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            logger = Logger;
            logger.LogInfo("Halo Armour Plugin Enabled.");
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);

            // Add a custom configuration option for the Apply button
            shieldEnabledConfig = Config.Bind("A - Shield Settings", "Shield Enabled", true, "Adds a shield to the undersuit when you wear it.");
            maxShieldConfig = Config.Bind<int>("A - Shield Settings", "Max Shield Value", 100, "The Maximum amount of shield the undersuits have.");
            shieldScaleOffsetConfig = Config.Bind<float>("A - Shield Settings", "Shield HUD Scale Offset", 1f, new BepInEx.Configuration.ConfigDescription("The Scale Offset for the Shield Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(0.1f, 2f)));
            shieldOffsetYConfig = Config.Bind<float>("A - Shield Settings", "Shield HUD Y Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The Y Position Offset for the Shield Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            shieldOffsetXConfig = Config.Bind<float>("A - Shield Settings", "Shield HUD X Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The X Position Offset for the Shield Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            radarEnabledConfig = Config.Bind("B - Radar Settings", "Radar Enabled", true, "Adds a Radar feature to the undersuit when you wear it.");
            radarScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Scale Offset", 1f, new BepInEx.Configuration.ConfigDescription("The Scale Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(0.1f, 2f)));
            radarDistanceScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Blip Disntance Scale Offset", 1f, new BepInEx.Configuration.ConfigDescription("This scales the blips distances from the player, effectively zooming it in and out.", new BepInEx.Configuration.AcceptableValueRange<float>(0.1f, 2f)));
            radarHeightThresholdeScaleOffsetConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Blip Height Threshold Offset", 1f, new BepInEx.Configuration.ConfigDescription("This scales the distance threshold for blips turning into up or down arrows depending on enemies height levels.", new BepInEx.Configuration.AcceptableValueRange<float>(1f, 4f)));
            radarOffsetYConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD Y Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The Y Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            radarOffsetXConfig = Config.Bind<float>("B - Radar Settings", "Radar HUD X Position Offset", 0f, new BepInEx.Configuration.ConfigDescription("The X Position Offset for the Radar Hud.", new BepInEx.Configuration.AcceptableValueRange<float>(-2000f, 2000f)));
            shieldRechargeTimeConfig = Config.Bind<float>("A - Shield Settings", "Shield Max Recharge Time", 4f, new ConfigDescription("The amount of time, in seconds, that the shield will take to recharge.", new AcceptableValueRange<float>(0.01f, 60f)));
            shieldRechargeWaitTimeConfig = Config.Bind<float>("A - Shield Settings", "Shield Recharge Wait Time", 6f, new ConfigDescription("The amount of time, in seconds, that it takes for the shield to begin attempting to recharge.", new AcceptableValueRange<float>(0.01f, 60f)));
            speedChangesConfig = Config.Bind("B - Undersuit Settings", "Walk & Sprint Speed Changes", true, "Increases speed of walking and sprinting to be more spartan-like.");
            speedBuffMultiplierConfig = Config.Bind<float>("B - Undersuit Settings", "Walk & Sprint Speed Multiplier", 1f, new ConfigDescription("Multiplies the Walk & Sprint Speed provided by the Undersuit.", new AcceptableValueRange<float>(0.01f, 2f)));
            staminaChangesConfig = Config.Bind("B - Undersuit Settings", "Stamina Changes", true, "Increases stamina to be more spartan-like.");
            staminaBuffMultiplierConfig = Config.Bind<float>("B - Undersuit Settings", "Stamina Multiplier", 1f, new ConfigDescription("Multiplies the Stamina provided by the Undersuit.", new AcceptableValueRange<float>(0.01f, 2f)));
            jumpChangesConfig = Config.Bind("B - Undersuit Settings", "Jump Height Changes", true, "Increases jump height to be more spartan-like.");
            jumpBuffMultiplierConfig = Config.Bind<float>("B - Undersuit Settings", "Jump Height Multiplier", 1f, new ConfigDescription("Multiplies the Jump Height provided by the Undersuit.", new AcceptableValueRange<float>(0.01f, 2f)));
            recoilChangesConfig = Config.Bind("B - Undersuit Settings", "Weapon Recoil", true, "Decreases recoil so that weapon control will be more spartan-like.");
            recoilBuffMultiplierConfig = Config.Bind<float>("B - Undersuit Settings", "Weapon Recoil Reduction Multiplier", 0.8f, new ConfigDescription("Multiplies the amount of recoil that weapons have by this value, lower values = less recoil.", new AcceptableValueRange<float>(0.01f, 1f)));
            undersuitArmourConfig = Config.Bind("B - Undersuit Settings", "Undersuit Armour", true, "Undersuit natively reduces damage for being worn, as if it were a piece of armour.");
            undersuitArmourReductionConfig = Config.Bind<float>("B - Undersuit Settings", "Undersuit Armour Damage Reduction Multiplier", 0.75f, new ConfigDescription("Multiplies the amount of damage received by this value, lower values = less damage taken.", new AcceptableValueRange<float>(0.01f, 1f)));
            doorKickerEnabledConfig = Config.Bind("B - Undersuit Settings", "Brute Force Doors Open w/ Kicks", true, "Want to smash open a door with your kicks? Enable this.");
            applyChangesConfig = Config.Bind("C - Material Settings", "Enabled & Toggle For Forced Colour Refresh", false, "Recolouring setting enabled. When enabled, list will populate with colourable halo armour textures.");
            applyChanges = applyChangesConfig.Value;
            clearErrantClonesConfig = Config.Bind("C - Material Settings", "Enabled & Toggle To Clear Colourable Object Clones", false, "Use this in the outfit or character view screen to clear clones of colourable helmets, this will clear the clone to allow for recolouring.");
            clearClonesChanges = clearErrantClonesConfig.Value;

            // Register the SettingChanged event
            applyChangesConfig.SettingChanged += OnApplyChangesSettingChanged;
            // Register the SettingChanged event
            clearErrantClonesConfig.SettingChanged += OnClearClonesSettingChanged;

            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnApplyChangesSettingChanged(object sender, EventArgs e)
        {
            applyChanges = applyChangesConfig.Value;
            colouringRanTimes = 0;
            if (applyChanges && colouringRanTimes <= colouringRanTimesMaxIterations)
            {
                SearchAndRecolorObjects();
            }
        }

        private void OnClearClonesSettingChanged(object sender, EventArgs e)
        {
            clearClonesChanges = clearErrantClonesConfig.Value;
            GameObject[] currentObjects = GameObject.FindObjectsOfType<GameObject>();
            if (currentObjects != null)
            {
                //EFT.UI.ConsoleScreen.LogError("Number of current objects: " + currentObjects.Length);

                foreach (GameObject obj in currentObjects)
                {
                    //EFT.UI.ConsoleScreen.LogError("Processing object: " + obj.name);
                    if (!objectsMaterials.ContainsKey(obj))
                    {
                        //EFT.UI.ConsoleScreen.LogError("Trying to get outfit type: " + obj.name);
                        if (obj.name != null)
                        {
                            if (obj.name.ToLower().StartsWith("helmet") && obj.name.ToLower().EndsWith("colourable(clone)"))
                            {
                                Destroy(obj);
                            }
                        }
                    }
                }
            }
        }

        private void Update()
        {
            if (!MapLoaded())
                return;

            gameWorld = Singleton<GameWorld>.Instance;
            if (gameWorld == null || gameWorld.MainPlayer == null)
                return;

            Player = gameWorld.MainPlayer;
            if (Player == null)
                return;

            if (PlayerLegs == null && PlayerBody == null)
            {
                Renderer[] renderers = Player.GetComponentsInChildren<Renderer>();
                float minBoundsY = float.MaxValue;
                float maxBoundsY = float.MinValue;
                foreach (Renderer renderer in renderers)
                {
                    Bounds bounds = renderer.bounds;
                    if (bounds.min.y < minBoundsY)
                        minBoundsY = bounds.min.y;
                    if (bounds.max.y > maxBoundsY)
                        maxBoundsY = bounds.max.y;
                }
                playerHeight = maxBoundsY - minBoundsY;
                foreach (Renderer renderer in renderers)
                {
                    string componentName = renderer.gameObject.name.ToLower();
                    if (componentName.Contains("legs") && componentName.Contains("bodysuit"))
                    {
                        PlayerLegs = componentName;
                        //EFT.UI.ConsoleScreen.LogError("Found Legs: " + PlayerLegs);
                    }
                    else if (componentName.Contains("body") && componentName.Contains("bodysuit"))
                    {
                        PlayerBody = componentName;
                        //EFT.UI.ConsoleScreen.LogError("Found Body: " + PlayerBody);
                    }
                }
            }

            if (PlayerBody != null && PlayerLegs != null)
            {
                if (PlayerLegs.ToLower().Contains("bodysuit") && PlayerBody.ToLower().Contains("bodysuit") && HudsEnabled != true)
                {
                    // Enable our patches and objects if wearing the halo bodysuits.
                    if (patchesEnabled != true)
                    {
                        patchesEnabled = true;
                    }
                    //EFT.UI.ConsoleScreen.LogError("Found Body & Legs Together: " + PlayerBody + " " + PlayerLegs);
                    GameObject gamePlayerObject = Player.gameObject;
                    if (gamePlayerObject.GetComponent<HaloShield>() == null && shieldEnabledConfig.Value)
                    {
                        HaloShield haloShield = gamePlayerObject.AddComponent<HaloShield>();
                    }
                    if (gamePlayerObject.GetComponent<HaloRadar>() == null && radarEnabledConfig.Value)
                    {
                        HaloRadar haloRadar = gamePlayerObject.AddComponent<HaloRadar>();
                    }
                    HudsEnabled = true;
                }
                else if (PlayerLegs.ToLower().Contains("bodysuit") == false && PlayerBody.ToLower().Contains("bodysuit") == false && HudsEnabled != false)
                {
                    EFT.UI.ConsoleScreen.LogError("Body & Legs Not Together: " + PlayerBody + " " + PlayerLegs);
                    // Disable our patches and objects if not wearing the halo bodysuits.
                    if (patchesEnabled != false)
                    {
                        patchesEnabled = false;
                    }
                    GameObject gamePlayerObject = Player.gameObject;
                    HaloShield haloShield = gamePlayerObject.GetComponent<HaloShield>();
                    if (haloShield != null)
                    {
                        Destroy(haloShield);
                    }
                    HaloRadar haloRadar = gamePlayerObject.GetComponent<HaloRadar>();
                    if (haloRadar != null)
                    {
                        Destroy(haloRadar);
                    }
                    HudsEnabled = false;
                }

                if (PlayerBody.ToLower().Contains("_colourable") || PlayerLegs.ToLower().Contains("_colourable"))
                {
                    if (Player != null && colouringRanTimes <= colouringRanTimesMaxIterations)
                    {
                        if (cachedRenderers == null)
                        {
                            cachedRenderers = Player.GetComponentsInChildren<Renderer>(includeInactive: true)
                                    .Where(renderer => renderer.GetComponent<HaloShield>() == null)
                                    .ToArray();
                        }
                        if (cachedRenderers != null)
                        {
                            foreach (Renderer renderer in cachedRenderers)
                            {
                                if (renderer != null && renderer.sharedMaterials != null)
                                {
                                    materials = renderer.sharedMaterials;
                                    if (materials != null)
                                    {
                                        foreach (Material material in materials)
                                        {
                                            if (material != null)
                                            {
                                                if (!objectsMaterials.ContainsKey(Player.gameObject))
                                                {
                                                    objectsMaterials[Player.gameObject] = new HashSet<Material>();
                                                }

                                                objectsMaterials[Player.gameObject].Add(material);

                                                (GameObject, Material) key = (Player.gameObject, material);
                                                string outfitType = GetOutfitType(renderer.name);
                                                if (materialColorConfigs.ContainsKey(key))
                                                {
                                                    ConfigEntry<Color> materialConfig = materialColorConfigs[key];
                                                    if (material.name == "spartan_shield_display_colourable")
                                                    {
                                                        material.SetColor("_EmissionColor", Color.white);
                                                        material.SetColor("_EmissionColor", materialConfig.Value);
                                                    }
                                                    else
                                                    {
                                                        material.SetColor("_Color", Color.white);
                                                        material.SetColor("_Color", materialConfig.Value);
                                                    }
                                                    renderer.enabled = false;
                                                    renderer.enabled = true;
                                                }
                                                else if (outfitType != null)
                                                {
                                                    string configName = $"{outfitType} {PrettifyText(material.name)}";
                                                    ConfigEntry<Color> materialConfig = Config.Bind("C - Material Settings", configName, Color.white, $"Color for material '{PrettifyText(material.name)}' of object '{outfitType}'");
                                                    materialColorConfigs[key] = materialConfig;
                                                    if (material.name == "spartan_shield_display_colourable")
                                                    {
                                                        material.SetColor("_EmissionColor", Color.white);
                                                        material.SetColor("_EmissionColor", materialConfig.Value);
                                                    }
                                                    else
                                                    {
                                                        material.SetColor("_Color", Color.white);
                                                        material.SetColor("_Color", materialConfig.Value);
                                                    }
                                                    renderer.enabled = false;
                                                    renderer.enabled = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        ++colouringRanTimes;
                    }
                }
            }
        }
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            colouringRanTimes = 0;
            if (applyChanges && colouringRanTimes <= colouringRanTimesMaxIterations)
            {
                SearchAndRecolorObjects();
            }
            if (HaloShield.skills != null)
            {
                if (HaloShield.skills.StrengthBuffMeleePowerInc != null && HaloShield.skills.StrengthBuffMeleePowerInc.Value != HaloShield.userJumpSkill)
                {
                    HaloShield.skills.StrengthBuffMeleePowerInc.Value = HaloShield.userJumpSkill;
                }
                if (HaloShield.skills.EnduranceBuffEnduranceInc != null && HaloShield.skills.EnduranceBuffEnduranceInc.Value != HaloShield.userEnduranceSkill)
                {
                    HaloShield.skills.EnduranceBuffEnduranceInc.Value = HaloShield.userEnduranceSkill;
                }
                if (HaloShield.skills.EnduranceBuffRestoration != null && HaloShield.skills.EnduranceBuffRestoration.Value != HaloShield.userEnduranceRegenerationSkill)
                {
                    HaloShield.skills.EnduranceBuffRestoration.Value = HaloShield.userEnduranceRegenerationSkill;
                }
                if (HaloShield.skills.StrengthBuffSprintSpeedInc != null && HaloShield.skills.StrengthBuffSprintSpeedInc.Value != HaloShield.userSprintSkill)
                {
                    HaloShield.skills.StrengthBuffSprintSpeedInc.Value = HaloShield.userSprintSkill;
                }
            }
            PlayerBody = null;
            PlayerLegs = null;
            PlayerHelmet = null;
        }
        private void OnSceneUnloaded(Scene scene)
        {
            colouringRanTimes = 0;
            if (applyChanges && colouringRanTimes <= colouringRanTimesMaxIterations)
            {
                SearchAndRecolorObjects();
            }
            if (HaloShield.skills != null)
            {
                if (HaloShield.skills.StrengthBuffMeleePowerInc != null && HaloShield.skills.StrengthBuffMeleePowerInc.Value != HaloShield.userJumpSkill)
                {
                    HaloShield.skills.StrengthBuffMeleePowerInc.Value = HaloShield.userJumpSkill;
                }
                if (HaloShield.skills.EnduranceBuffEnduranceInc != null && HaloShield.skills.EnduranceBuffEnduranceInc.Value != HaloShield.userEnduranceSkill)
                {
                    HaloShield.skills.EnduranceBuffEnduranceInc.Value = HaloShield.userEnduranceSkill;
                }
                if (HaloShield.skills.EnduranceBuffRestoration != null &&  HaloShield.skills.EnduranceBuffRestoration.Value != HaloShield.userEnduranceRegenerationSkill)
                {
                    HaloShield.skills.EnduranceBuffRestoration.Value = HaloShield.userEnduranceRegenerationSkill;
                }
                if (HaloShield.skills.StrengthBuffSprintSpeedInc != null &&  HaloShield.skills.StrengthBuffSprintSpeedInc.Value != HaloShield.userSprintSkill)
                {
                    HaloShield.skills.StrengthBuffSprintSpeedInc.Value = HaloShield.userSprintSkill;
                }
            }
            PlayerBody = null;
            PlayerLegs = null;
            PlayerHelmet = null;
        }

        private void SearchAndRecolorObjects()
        {
            GameObject[] currentObjects = GameObject.FindObjectsOfType<GameObject>();
            if (currentObjects != null && colouringRanTimes <= colouringRanTimesMaxIterations)
            {
                //EFT.UI.ConsoleScreen.LogError("Number of current objects: " + currentObjects.Length);

                foreach (GameObject obj in currentObjects)
                {
                    //EFT.UI.ConsoleScreen.LogError("Processing object: " + obj.name);
                    if (!objectsMaterials.ContainsKey(obj))
                    {
                        //EFT.UI.ConsoleScreen.LogError("Trying to get outfit type: " + obj.name);
                        if (obj.name != null)
                        {
                            if (obj.name.ToLower().EndsWith("colourable"))
                            {
                                string outfitType = GetOutfitType(obj.name);
                                //EFT.UI.ConsoleScreen.LogError("Colourable object: " + obj.name);
                                cachedRenderers = obj.GetComponentsInChildren<Renderer>(includeInactive: true)
                                    .Where(renderer => renderer.GetComponent<HaloShield>() == null)
                                    .ToArray();
                                bool wasActive = obj.activeSelf;
                                obj.SetActive(true);
                                if (cachedRenderers != null)
                                {
                                    //EFT.UI.ConsoleScreen.LogError("Number of cached renderers: " + cachedRenderers.Length);

                                    foreach (Renderer renderer in cachedRenderers)
                                    {
                                        //EFT.UI.ConsoleScreen.LogError("Processing cached renderer: " + renderer.name);
                                        if (renderer != null && renderer.sharedMaterials != null)
                                        {
                                            materials = renderer.sharedMaterials;
                                            if (materials != null)
                                            {
                                                //EFT.UI.ConsoleScreen.LogError("Number of materials: " + materials.Length);

                                                foreach (Material material in materials)
                                                {
                                                    //EFT.UI.ConsoleScreen.LogError("Processing material: " + material.name);
                                                    if (material != null)
                                                    {
                                                        if (!objectsMaterials.ContainsKey(obj))
                                                        {
                                                            objectsMaterials[obj] = new HashSet<Material>();
                                                        }

                                                        objectsMaterials[obj].Add(material);

                                                        (GameObject, Material) key = (obj, material);

                                                        if (materialColorConfigs.ContainsKey(key))
                                                        {
                                                            if (material.name == "spartan_shield_display_colourable")
                                                            {
                                                                material.SetColor("_EmissionColor", Color.white);
                                                                ConfigEntry<Color> materialConfig = materialColorConfigs[key];
                                                                material.SetColor("_EmissionColor", materialConfig.Value);
                                                            }
                                                            else
                                                            {
                                                                material.SetColor("_Color", Color.white);
                                                                ConfigEntry<Color> materialConfig = materialColorConfigs[key];
                                                                material.SetColor("_Color", materialConfig.Value);
                                                            }
                                                            renderer.enabled = false;
                                                            renderer.enabled = true;
                                                        }
                                                        else if (outfitType != null)
                                                        {
                                                            string configName = $"{outfitType} {PrettifyText(material.name)}";
                                                            if (material.name == "spartan_shield_display_colourable")
                                                            {
                                                                material.SetColor("_EmissionColor", Color.white);
                                                                ConfigEntry<Color> materialConfig = Config.Bind("C - Material Settings", configName, Color.white, $"Color for material '{PrettifyText(material.name)}' of object '{outfitType}'");
                                                                materialColorConfigs[key] = materialConfig;
                                                                material.SetColor("_EmissionColor", materialConfig.Value);
                                                            }
                                                            else
                                                            {
                                                                material.SetColor("_Color", Color.white);
                                                                ConfigEntry<Color> materialConfig = Config.Bind("C - Material Settings", configName, Color.white, $"Color for material '{PrettifyText(material.name)}' of object '{outfitType}'");
                                                                materialColorConfigs[key] = materialConfig;
                                                                material.SetColor("_Color", materialConfig.Value);
                                                            }
                                                            renderer.enabled = false;
                                                            renderer.enabled = true;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                obj.SetActive(wasActive);
                            }
                        }
                    }
                }
                ++colouringRanTimes;
            }
        }

        private string GetOutfitType(string objectName)
        {
            if (objectName.ToLower().Contains("undersuit_body_colourable") || objectName.ToLower().Contains("undersuit_body_cyborg_colourable") || objectName.ToLower().Contains("bodysuit_body_colourable"))
            {
                return "Undersuit Body";
            }
            else if (objectName.ToLower().Contains("undersuit_legs_colourable") || objectName.ToLower().Contains("bodysuit_legs_colourable"))
            {
                return "Undersuit Legs";
            }
            else if (objectName.ToLower().Contains("undersuit_arms_colourable") || objectName.ToLower().Contains("undersuit_arms_cyborg_colourable") || objectName.ToLower().Contains("bodysuit_arms_colourable"))
            {
                return "Undersuit Body";
            }
            else if (objectName.ToLower().Contains("helmet_spartan") && objectName.ToLower().Contains("_colourable"))
            {
                return "Helmet";
            }
            return null;
        }

        private string PrettifyText(string text)
        {
            string[] words = text.Split('_');

            for (int i = 0; i < words.Length; i++)
            {
                words[i] = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(words[i].ToLower());
            }

            string result = string.Join(" ", words);

            result = result.Replace(" Colourable", ""); // Remove the word "Colourable"
            result = result.Replace(" (Instance)", ""); // Remove the word "Instance"
            result = result.Trim(); // Remove leading and trailing whitespace characters

            return result;
        }
    }

    public class HaloShield : MonoBehaviour
    {
        public static GameWorld gameWorld;
        public static Player player;
        public static ActiveHealthControllerClass health;
        public static SkillsClass skills;
        public static EFT.Animations.ProceduralWeaponAnimation weapon;
        public static AssetBundle shieldhudBundle;
        public static AssetBundle shieldhudAudioBundle;
        public static AudioSource shieldHudAudioHit;
        public static AudioSource shieldHudAudioHitFinal;
        public static AudioSource shieldHudAudioDepleted;
        public static AudioSource shieldHudAudioRegenerating;
        /*public static AudioSource shieldHudAudioDeath;*/
        public static bool shieldHudAudioRegeneratingPlayed = false;
        public static AudioSource shieldHudAudioLow;
        public static Sprite ShieldBarFillSprite;
        public static Sprite ShieldBarWarningSprite;
        public static Sprite ShieldBarHealthFullSprite;
        public static Sprite ShieldBarHealth90Sprite;
        public static Sprite ShieldBarHealth80Sprite;
        public static Sprite ShieldBarHealth70Sprite;
        public static Sprite ShieldBarHealth60Sprite;
        public static Sprite ShieldBarHealth50Sprite;
        public static Sprite ShieldBarHealth40Sprite;
        public static Sprite ShieldBarHealth30Sprite;
        public static Sprite ShieldBarHealth20Sprite;
        public static Sprite ShieldBarHealth10Sprite;
        public static Object ShieldhudPrefab { get; private set; }
        public static GameObject playerCamera;
        public static GameObject shieldHud;
        public static Image shieldBarFillRight;
        public static Image shieldBarFillLeft;
        public static Image shieldBarFillRightRed;
        public static Image shieldBarFillLeftRed;
        public static Image shieldBarEdgesImage;
        public static Image shieldBarHealthPipsImage;
        public static float playerMaxHealth = 0f;
        public static float playerCurrentHealth = 0f;
        public static int maxShield = HaloArmour.maxShieldConfig.Value;
        public static int currentShield = maxShield;
        public static int lastShield = maxShield;
        public static bool shieldGotHit = false;
        public static bool playerHealthChanged = false;
        public static bool playerDead = false;
        public static Coroutine shieldRechargeTimerCoroutine;
        public static Coroutine shieldRechargeCoroutine;
        public static RectTransform shieldHudBasePosition { get; private set; }
        public static RectTransform ShieldBarHealthPips { get; private set; }
        public static RectTransform shieldBarEdges { get; private set; }
        public static float shieldRechargeWaitTime = HaloArmour.shieldRechargeWaitTimeConfig.Value;
        public static float shieldRechargeDurationTime = HaloArmour.shieldRechargeTimeConfig.Value;
        public static float userJumpSkill = 0f;
        public static float userEnduranceSkill = 0f;
        public static float userEnduranceRegenerationSkill = 0f;
        public static float userSprintSkill = 0f;
        public static float defaultCharacterSpeed = 0f;
        public static Vector3 shieldScaleStart;
        public static float shieldPositionYStart = 0f;
        public static float shieldPositionXStart = 0f;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;

        private void Start()
        {
            // Create our prefabs from our bundles and shit.
            if (ShieldhudPrefab == null)
            {
                String haloUndersuitHUD = Path.Combine(Environment.CurrentDirectory, "BepInEx/plugins/UNSCEquipment/shieldbarhud.bundle");
                String haloUndersuitAudio = Path.Combine(Environment.CurrentDirectory, "BepInEx/plugins/UNSCEquipment/shieldbarhudaudio.bundle");
                if (!File.Exists(haloUndersuitHUD))
                    return;
                shieldhudBundle = AssetBundle.LoadFromFile(haloUndersuitHUD);
                if (!File.Exists(haloUndersuitAudio))
                    return;
                shieldhudAudioBundle = AssetBundle.LoadFromFile(haloUndersuitAudio);
                if (shieldhudBundle == null)
                    return;
                ShieldhudPrefab = shieldhudBundle.LoadAsset("Assets/Examples/Halo Reach/Hud/ShieldBarHUD.prefab");
            }
        }
        private void Update()
        {
            if (MapLoaded())
            {
                gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld.MainPlayer != null)
                {
                    player = gameWorld.MainPlayer;
                }
                if (player != null)
                {
                    health = player.ActiveHealthController;
                    skills = player.Skills;
                    weapon = player.ProceduralWeaponAnimation;
                    if (skills != null)
                    {
                        if (skills.StrengthBuffJumpHeightInc != null && skills.EnduranceBuffEnduranceInc != null && skills.EnduranceBuffRestoration != null && skills.StrengthBuffSprintSpeedInc != null 
                            && skills.StrengthBuffSprintSpeedInc != null && userJumpSkill == 0f && userEnduranceSkill == 0f && userEnduranceRegenerationSkill == 0f && userSprintSkill == 0f 
                            && defaultCharacterSpeed == 0f)
                        {
                            userJumpSkill = skills.StrengthBuffJumpHeightInc.Value;
                            userEnduranceSkill = skills.EnduranceBuffEnduranceInc.Value;
                            userEnduranceRegenerationSkill = skills.EnduranceBuffRestoration.Value;
                            userSprintSkill = skills.StrengthBuffSprintSpeedInc.Value;
                            defaultCharacterSpeed = skills.StrengthBuffSprintSpeedInc.Value;
                        }
                    }
                            if (playerMaxHealth == 0f && health != null)
                            {
                                var HeadMaxHP = health.GetBodyPartHealth(EBodyPart.Head, true).Maximum;
                                var ChestMaxHP = health.GetBodyPartHealth(EBodyPart.Chest, true).Maximum;
                                var LeftArmMaxHP = health.GetBodyPartHealth(EBodyPart.LeftArm, true).Maximum;
                                var LeftLegMaxHP = health.GetBodyPartHealth(EBodyPart.LeftLeg, true).Maximum;
                                var RightArmMaxHP = health.GetBodyPartHealth(EBodyPart.RightArm, true).Maximum;
                                var RightLegMaxHP = health.GetBodyPartHealth(EBodyPart.RightLeg, true).Maximum;
                                var StomachMaxHP = health.GetBodyPartHealth(EBodyPart.Stomach, true).Maximum;
                                playerMaxHealth = ((HeadMaxHP + ChestMaxHP) * 2f) + ((LeftArmMaxHP + LeftLegMaxHP + RightArmMaxHP + RightLegMaxHP + StomachMaxHP) / 2.5f);
                                //EFT.UI.ConsoleScreen.LogError($"Max Health: " + playerMaxHealth);
                            }
                            if (maxShield != HaloArmour.maxShieldConfig.Value)
                            {
                                maxShield = HaloArmour.maxShieldConfig.Value;
                                currentShield = maxShield;
                            }
                            if (shieldRechargeWaitTime != HaloArmour.shieldRechargeWaitTimeConfig.Value)
                            {
                                shieldRechargeWaitTime = HaloArmour.shieldRechargeWaitTimeConfig.Value;
                            }
                            if (shieldRechargeDurationTime != HaloArmour.shieldRechargeTimeConfig.Value)
                            {
                                shieldRechargeDurationTime = HaloArmour.shieldRechargeTimeConfig.Value;
                            }
                            if (skills != null)
                            {
                                if (skills.StrengthBuffJumpHeightInc != null && skills.StrengthBuffJumpHeightInc.Value != 1f * HaloArmour.jumpBuffMultiplierConfig.Value)
                                {
                                    skills.StrengthBuffJumpHeightInc.Value = Mathf.Max(userJumpSkill, 1f * HaloArmour.jumpBuffMultiplierConfig.Value);
                                }
                                if (skills.StrengthBuffJumpHeightInc != null && !HaloArmour.jumpChangesConfig.Value && skills.StrengthBuffJumpHeightInc.Value == 1f * HaloArmour.jumpBuffMultiplierConfig.Value)
                                {
                                    skills.StrengthBuffJumpHeightInc.Value = userJumpSkill;
                                }
                                if (skills.EnduranceBuffEnduranceInc != null && skills.EnduranceBuffEnduranceInc.Value != 4f * HaloArmour.staminaBuffMultiplierConfig.Value)
                                {
                                    skills.EnduranceBuffEnduranceInc.Value = Mathf.Max(userEnduranceSkill, 4f * HaloArmour.staminaBuffMultiplierConfig.Value);
                                }
                                if (skills.EnduranceBuffEnduranceInc != null && !HaloArmour.staminaChangesConfig.Value && skills.EnduranceBuffEnduranceInc.Value == 4f * HaloArmour.staminaBuffMultiplierConfig.Value)
                                {
                                    skills.EnduranceBuffEnduranceInc.Value = userEnduranceSkill;
                                }
                                if (skills.EnduranceBuffRestoration != null &&  skills.EnduranceBuffRestoration.Value != 4f * HaloArmour.staminaBuffMultiplierConfig.Value)
                                {
                                    skills.EnduranceBuffRestoration.Value = Mathf.Max(userEnduranceRegenerationSkill, 4f * HaloArmour.staminaBuffMultiplierConfig.Value);
                                }
                                if (skills.EnduranceBuffRestoration != null &&  !HaloArmour.staminaChangesConfig.Value && skills.EnduranceBuffRestoration.Value == 4f * HaloArmour.staminaBuffMultiplierConfig.Value)
                                {
                                    skills.EnduranceBuffRestoration.Value = userEnduranceRegenerationSkill;
                                }
                                if (skills.StrengthBuffSprintSpeedInc != null &&  skills.StrengthBuffSprintSpeedInc.Value != 12f * HaloArmour.speedBuffMultiplierConfig.Value)
                                {
                                    skills.StrengthBuffSprintSpeedInc.Value = Mathf.Max(userSprintSkill, 12f * HaloArmour.speedBuffMultiplierConfig.Value);
                                }
                                if (skills.StrengthBuffSprintSpeedInc != null &&  !HaloArmour.speedChangesConfig.Value && skills.StrengthBuffSprintSpeedInc.Value == 12f * HaloArmour.speedBuffMultiplierConfig.Value)
                                {
                                    skills.StrengthBuffSprintSpeedInc.Value = userSprintSkill;
                                }
                            }
                            if (weapon != null && HaloArmour.recoilChangesConfig.Value)
                            {
                                if (weapon.Shootingg != null && weapon.Shootingg.Intensity != HaloArmour.recoilBuffMultiplierConfig.Value)
                                {
                                    weapon.Shootingg.Intensity = HaloArmour.recoilBuffMultiplierConfig.Value;
                                }
                                if (weapon.Shootingg != null && weapon.Shootingg.Stiffness != HaloArmour.recoilBuffMultiplierConfig.Value)
                                {
                                    weapon.Shootingg.Stiffness = HaloArmour.recoilBuffMultiplierConfig.Value;
                                }
                                if (weapon.Shootingg != null && weapon.Breath.Intensity != HaloArmour.recoilBuffMultiplierConfig.Value)
                                {
                                    weapon.Breath.Intensity = HaloArmour.recoilBuffMultiplierConfig.Value;

                                }
                                if (weapon.Shootingg != null && weapon.MotionReact.Intensity != HaloArmour.recoilBuffMultiplierConfig.Value)
                                {
                                    weapon.MotionReact.Intensity = HaloArmour.recoilBuffMultiplierConfig.Value;
                                }
                                if (weapon.Shootingg != null && weapon.ForceReact.Intensity != HaloArmour.recoilBuffMultiplierConfig.Value)
                                {
                                    weapon.ForceReact.Intensity = HaloArmour.recoilBuffMultiplierConfig.Value;
                                }
                            }
                            if (player.MovementContext != null)
                            {
                                if (player.MovementContext != null && !HaloArmour.speedChangesConfig.Value && player.MovementContext.CharacterMovementSpeed != 3f * HaloArmour.speedBuffMultiplierConfig.Value)
                                {
                                    player.MovementContext.SetCharacterMovementSpeed(Mathf.Max(defaultCharacterSpeed, 3f * HaloArmour.speedBuffMultiplierConfig.Value));
                                }
                                if (player.MovementContext != null && !HaloArmour.speedChangesConfig.Value && player.MovementContext.CharacterMovementSpeed == 3f * HaloArmour.speedBuffMultiplierConfig.Value)
                                {
                                    player.MovementContext.SetCharacterMovementSpeed(defaultCharacterSpeed, false);
                                }
                            }
                            if (playerCamera == null)
                            {
                                playerCamera = GameObject.Find("FPS Camera");
                            }
                            if (HaloArmour.shieldEnabledConfig.Value && shieldHud == null && playerCamera != null)
                            {
                                var shieldHudBase = Instantiate(ShieldhudPrefab, playerCamera.transform.position, playerCamera.transform.rotation);
                                shieldHud = shieldHudBase as GameObject;
                                shieldHud.transform.parent = playerCamera.transform;
                                shieldHudBasePosition = shieldHud.transform.Find("ShieldBar") as RectTransform;
                                shieldScaleStart = shieldHudBasePosition.localScale;
                                shieldPositionYStart = shieldHudBasePosition.position.y;
                                shieldPositionXStart = shieldHudBasePosition.position.x;
                                shieldHudAudioHit = shieldHud.AddComponent<AudioSource>();
                                shieldHudAudioHit.clip = shieldhudAudioBundle.LoadAsset<AudioClip>("ShieldHit (" + UnityEngine.Random.Range(1, 7) + ")");
                                shieldHudAudioHit.playOnAwake = false;
                                shieldHudAudioLow = shieldHud.AddComponent<AudioSource>();
                                shieldHudAudioLow.clip = shieldhudAudioBundle.LoadAsset<AudioClip>("ShieldLow");
                                shieldHudAudioLow.playOnAwake = false;
                                shieldHudAudioHitFinal = shieldHud.AddComponent<AudioSource>();
                                shieldHudAudioHitFinal.clip = shieldhudAudioBundle.LoadAsset<AudioClip>("ShieldHitFinal");
                                shieldHudAudioHitFinal.playOnAwake = false;
                                shieldHudAudioDepleted = shieldHud.AddComponent<AudioSource>();
                                shieldHudAudioDepleted.clip = shieldhudAudioBundle.LoadAsset<AudioClip>("Depleted");
                                shieldHudAudioDepleted.playOnAwake = false;
                                shieldHudAudioRegenerating = shieldHud.AddComponent<AudioSource>();
                                shieldHudAudioRegenerating.clip = shieldhudAudioBundle.LoadAsset<AudioClip>("Charge");
                                shieldHudAudioRegenerating.playOnAwake = false;
                                /*shieldHudAudioDeath = shieldHud.AddComponent<AudioSource>();
                                shieldHudAudioDeath.clip = shieldhudAudioBundle.LoadAsset<AudioClip>("DeathGurgle (" + UnityEngine.Random.Range(1, 10) + ")");
                                shieldHudAudioDeath.playOnAwake = false;*/
                                shieldBarEdges = shieldHud.transform.Find("ShieldBar/ShieldBarEdges") as RectTransform;
                                shieldBarEdgesImage = shieldBarEdges.GetComponent<Image>();
                                ShieldBarHealthPips = shieldHud.transform.Find("ShieldBar/ShieldBarEdges/ShieldBarHealthPips") as RectTransform;
                                shieldBarHealthPipsImage = ShieldBarHealthPips.GetComponent<Image>();
                                ShieldBarFillSprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarFill");
                                ShieldBarWarningSprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarWarning");
                                ShieldBarHealthFullSprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPipsFull");
                                ShieldBarHealth90Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips80");
                                ShieldBarHealth80Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips70");
                                ShieldBarHealth70Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips60");
                                ShieldBarHealth60Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips50");
                                ShieldBarHealth50Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips40");
                                ShieldBarHealth40Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips30");
                                ShieldBarHealth30Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips20");
                                ShieldBarHealth20Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips10");
                                ShieldBarHealth10Sprite = shieldhudBundle.LoadAsset<Sprite>("ShieldBarHudHealthPips0");
                                if (shieldBarEdges != null)
                                {
                                    shieldBarFillLeft = shieldBarEdges.Find("ShieldBarFillLeft").GetComponent<Image>();
                                    shieldBarFillRight = shieldBarEdges.Find("ShieldBarFillRight").GetComponent<Image>();
                                    shieldBarFillLeftRed = shieldBarEdges.Find("ShieldBarFillLeftRed").GetComponent<Image>();
                                    shieldBarFillRightRed = shieldBarEdges.Find("ShieldBarFillRightRed").GetComponent<Image>();
                                }
                            }
                            if (!HaloArmour.shieldEnabledConfig.Value && shieldHud != null && playerCamera != null)
                            {
                                if (shieldHud.activeInHierarchy)
                                {
                                    shieldHud.SetActive(false);
                                }
                            }
                            if (HaloArmour.shieldEnabledConfig.Value && shieldHud != null && playerCamera != null)
                            {
                                if (!shieldHud.activeInHierarchy)
                                {
                                    shieldHud.SetActive(true);
                                }
                            }
                            if (HaloArmour.shieldEnabledConfig.Value && shieldHud != null)
                            {
                                if (shieldGotHit || currentShield != maxShield)
                                {
                                    float shieldFillAmount = (float)currentShield / maxShield;
                                    float shieldRedFillAmount = (float)lastShield / maxShield;
                                    shieldBarFillLeft.fillAmount = shieldFillAmount;
                                    shieldBarFillRight.fillAmount = shieldFillAmount;
                                    shieldBarFillLeftRed.fillAmount = shieldRedFillAmount;
                                    shieldBarFillRightRed.fillAmount = shieldRedFillAmount;
                                }
                                if (shieldHudBasePosition.position.x != shieldPositionYStart + HaloArmour.radarOffsetYConfig.Value && shieldHudBasePosition.position.y != shieldPositionXStart + HaloArmour.radarOffsetXConfig.Value)
                                {
                                    shieldHudBasePosition.position = new Vector2(shieldPositionYStart + HaloArmour.shieldOffsetYConfig.Value, shieldPositionXStart + HaloArmour.shieldOffsetXConfig.Value);
                                }
                                if (shieldHudBasePosition.localScale.y != shieldScaleStart.y * HaloArmour.shieldScaleOffsetConfig.Value && shieldHudBasePosition.localScale.x != shieldScaleStart.x * HaloArmour.shieldScaleOffsetConfig.Value)
                                {
                                    shieldHudBasePosition.localScale = new Vector2(shieldScaleStart.y * HaloArmour.shieldScaleOffsetConfig.Value, shieldScaleStart.x * HaloArmour.shieldScaleOffsetConfig.Value);
                                }
                            }
                            if (health != null)
                            {
                                if (!HaloArmour.shieldEnabledConfig.Value)
                                {
                                    if (health.DamageCoeff != HaloArmour.undersuitArmourReductionConfig.Value && HaloArmour.undersuitArmourConfig.Value)
                                    {
                                        health.SetDamageCoeff(HaloArmour.undersuitArmourReductionConfig.Value);
                                    }
                                    if (health.DamageCoeff != 1f && !HaloArmour.undersuitArmourConfig.Value)
                                    {
                                        health.SetDamageCoeff(1f);
                                    }
                                }
                                if (currentShield == 0 && HaloArmour.shieldEnabledConfig.Value)
                                {
                                    if (health.DamageCoeff != HaloArmour.undersuitArmourReductionConfig.Value && HaloArmour.undersuitArmourConfig.Value)
                                    {
                                        health.SetDamageCoeff(HaloArmour.undersuitArmourReductionConfig.Value);
                                    }
                                    if (health.DamageCoeff != 1f && !HaloArmour.undersuitArmourConfig.Value)
                                    {
                                        health.SetDamageCoeff(1f);
                                    }
                                }
                                if (health.DamageCoeff != -0f && currentShield >= 1 && HaloArmour.shieldEnabledConfig.Value)
                                {
                                    health.SetDamageCoeff(-0f);
                                }
                            }
                            if (shieldGotHit)
                            {
                                StartCoroutine(ShieldFlash());
                                shieldHudAudioHit.clip = shieldhudAudioBundle.LoadAsset<AudioClip>("ShieldHit (" + UnityEngine.Random.Range(1, 7) + ")");
                                if (currentShield >= 1)
                                {
                                    shieldHudAudioHit.Play();
                                }
                                if (currentShield <= 30 && !shieldHudAudioLow.isPlaying)
                                {
                                    shieldHudAudioLow.Play();
                                }
                                if (currentShield > 30 && shieldHudAudioLow.isPlaying)
                                {
                                    shieldHudAudioLow.Stop();
                                }
                                if (currentShield == 0 && !shieldHudAudioHitFinal.isPlaying && !shieldHudAudioDepleted.isPlaying)
                                {
                                    shieldHudAudioHitFinal.Play();
                                    shieldHudAudioDepleted.Play();
                                    shieldBarEdgesImage.sprite = ShieldBarWarningSprite;
                                }
                                if (shieldHudAudioRegenerating.isPlaying)
                                {
                                    shieldHudAudioRegenerating.Stop();
                                    shieldHudAudioRegeneratingPlayed = false;
                                }
                                if (shieldRechargeTimerCoroutine != null)
                                    StopCoroutine(shieldRechargeTimerCoroutine);

                                if (shieldRechargeCoroutine != null)
                                    StopCoroutine(shieldRechargeCoroutine);

                                shieldRechargeTimerCoroutine = StartCoroutine(ResetShieldTimer());
                                shieldGotHit = false;
                            }
                            if (playerHealthChanged && !playerDead && shieldHud != null && health != null)
                            {
                                var HeadHP = health.GetBodyPartHealth(EBodyPart.Head, true).Current;
                                var ChestHP = health.GetBodyPartHealth(EBodyPart.Chest, true).Current;
                                var LeftArmHP = health.GetBodyPartHealth(EBodyPart.LeftArm, true).Current;
                                var LeftLegHP = health.GetBodyPartHealth(EBodyPart.LeftLeg, true).Current;
                                var RightArmHP = health.GetBodyPartHealth(EBodyPart.RightArm, true).Current;
                                var RightLegHP = health.GetBodyPartHealth(EBodyPart.RightLeg, true).Current;
                                var StomachHP = health.GetBodyPartHealth(EBodyPart.Stomach, true).Current;
                                playerCurrentHealth = ((HeadHP + ChestHP) * 2f) + ((LeftArmHP + LeftLegHP + RightArmHP + RightLegHP + StomachHP) / 2.5f);
                                float playerCurrentHealthPercentage = (playerCurrentHealth / playerMaxHealth) * 100f;
                                //EFT.UI.ConsoleScreen.LogError($"Current Health: " + playerCurrentHealth);
                                //EFT.UI.ConsoleScreen.LogError($"Current Health Percentage: " + playerCurrentHealthPercentage);
                                if (playerCurrentHealthPercentage >= 91f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealthFullSprite;
                                }
                                else if (playerCurrentHealthPercentage <= 90f && playerCurrentHealthPercentage >= 81f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth90Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 80f && playerCurrentHealthPercentage >= 71f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth80Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 70f && playerCurrentHealthPercentage >= 61f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth70Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 60f && playerCurrentHealthPercentage >= 51f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth60Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 50f && playerCurrentHealthPercentage >= 41f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth50Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 40f && playerCurrentHealthPercentage >= 31f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth40Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 30f && playerCurrentHealthPercentage >= 21f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth30Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 20f && playerCurrentHealthPercentage >= 11f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth20Sprite;
                                }
                                else if (playerCurrentHealthPercentage <= 10f && playerCurrentHealthPercentage >= 0f)
                                {
                                    shieldBarHealthPipsImage.sprite = ShieldBarHealth10Sprite;
                                }
                                playerHealthChanged = false;
                            }
                }
            }
        }
        internal static void DeathHandler(Player __instance)
        {
            playerDead = true;
            if (shieldHud != null && shieldBarHealthPipsImage != null)
            {
                shieldBarHealthPipsImage.sprite = ShieldBarHealth10Sprite;
            }
        }

        private IEnumerator ShieldFlash()
        {
            shieldBarFillLeftRed.color = new Color(1f, 1f, 1f, 1f);
            shieldBarFillRightRed.color = new Color(1f, 1f, 1f, 1f);

            yield return new WaitForSeconds(0.5f);

            shieldBarFillLeftRed.color = new Color(1f, 1f, 1f, 0f);
            shieldBarFillRightRed.color = new Color(1f, 1f, 1f, 0f);
        }

        private IEnumerator ResetShieldTimer()
        {
            yield return new WaitForSeconds(shieldRechargeWaitTime);

            shieldRechargeTimerCoroutine = null;
            shieldRechargeCoroutine = StartCoroutine(RechargeShield());
        }
        private IEnumerator RechargeShield()
        {
            float startTime = Time.time;
            float elapsedTime = 0f;

            int startingShield = currentShield;
            int targetShield = maxShield;

            float reductionAmount = shieldRechargeDurationTime / maxShield;
            float adjustedDuration = shieldRechargeDurationTime - (reductionAmount * startingShield);
            float shieldRechargeDurationTimeNew = Mathf.Clamp(adjustedDuration, shieldRechargeDurationTime / 4, shieldRechargeDurationTime);

            while (elapsedTime < shieldRechargeDurationTimeNew)
            {
                elapsedTime = Time.time - startTime;

                if (!shieldHudAudioRegenerating.isPlaying && shieldHudAudioRegeneratingPlayed == false)
                {
                    shieldHudAudioLow.Stop();
                    shieldHudAudioDepleted.Stop();
                    shieldHudAudioRegenerating.Play();
                    shieldHudAudioRegeneratingPlayed = true;
                    shieldBarEdgesImage.sprite = ShieldBarFillSprite;
                }

                currentShield = Mathf.RoundToInt(Mathf.Lerp(startingShield, targetShield, elapsedTime / shieldRechargeDurationTimeNew));

                yield return null;
            }

            currentShield = targetShield;
            shieldHudAudioRegenerating.Stop();
            shieldHudAudioRegeneratingPlayed = false;
        }
    }

    public class HaloRadar : MonoBehaviour
    {
        public static GameWorld gameWorld;
        public static Player player;
        public static Object RadarhudPrefab { get; private set; }
        public static Object RadarBliphudPrefab { get; private set; }
        public static AssetBundle radarBundle;
        public static GameObject radarHud;
        public static GameObject radarBlipHud;
        public static GameObject playerCamera;
        private GameObject[] enemyObjects;
        private Dictionary<GameObject, GameObject> enemyBlips;
        public static RectTransform radarHudBlipBasePosition { get; private set; }
        public static RectTransform radarHudBasePosition { get; private set; }
        public static RectTransform radarHudPulse { get; private set; }
        public static RectTransform radarHudBlip { get; private set; }
        public static Image blipImage;
        public static Sprite EnemyBlip;
        public static Sprite EnemyBlipDown;
        public static Sprite EnemyBlipUp;
        public static Coroutine pulseCoroutine;
        public static float animationDuration = 1f;
        public static float pauseDuration = 4f;
        public static Vector3 radarScaleStart;
        public static float radarPositionYStart = 0f;
        public static float radarPositionXStart = 0f;
        public static bool MapLoaded() => Singleton<GameWorld>.Instantiated;
        public BifacialTransform playerTransform; // Player's transform component

        public float radarRange = 128; // The range within which enemies are displayed on the radar

        private void Start()
        {
            enemyObjects = new GameObject[0];
            enemyBlips = new Dictionary<GameObject, GameObject>();
            // Create our prefabs from our bundles and shit.
            if (RadarhudPrefab == null)
            {
                String haloRadarHUD = Path.Combine(Environment.CurrentDirectory, "BepInEx/plugins/UNSCEquipment/radarhud.bundle");
                if (!File.Exists(haloRadarHUD))
                    return;
                radarBundle = AssetBundle.LoadFromFile(haloRadarHUD);
                if (radarBundle == null)
                    return;
                RadarhudPrefab = radarBundle.LoadAsset("Assets/Examples/Halo Reach/Hud/RadarHUD.prefab");
                RadarBliphudPrefab = radarBundle.LoadAsset("Assets/Examples/Halo Reach/Hud/RadarBlipHUD.prefab");
            }
        }

        private void Update()
        {
            if (MapLoaded())
            {
                gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld.MainPlayer != null)
                {
                    player = gameWorld.MainPlayer;
                    playerTransform = player.Transform;
                }

                if (playerCamera == null)
                {
                    playerCamera = GameObject.Find("FPS Camera");
                }

                if (playerCamera != null)
                {
                    if (HaloArmour.radarEnabledConfig.Value)
                    {
                        if (radarHud == null)
                        {
                            var radarHudBase = Instantiate(RadarhudPrefab, playerCamera.transform.position, playerCamera.transform.rotation);
                            radarHud = radarHudBase as GameObject;
                            radarHud.transform.parent = playerCamera.transform;
                            radarHudBasePosition = radarHud.transform.Find("Radar") as RectTransform;
                            radarHudBlipBasePosition = radarHud.transform.Find("Radar/RadarBorder") as RectTransform;
                            radarHudPulse = radarHud.transform.Find("Radar/RadarPulse") as RectTransform;
                            radarScaleStart = radarHudBasePosition.localScale;
                            radarPositionYStart = radarHudBasePosition.position.y;
                            radarPositionXStart = radarHudBasePosition.position.x;
                            StartPulseAnimation();
                        }
                        if (!radarHud.activeSelf)
                        {
                            radarHud.SetActive(true);
                        }
                        if (radarHudBasePosition.position.y != radarPositionYStart + HaloArmour.radarOffsetYConfig.Value || radarHudBasePosition.position.x != radarPositionXStart + HaloArmour.radarOffsetXConfig.Value)
                        {
                            radarHudBasePosition.position = new Vector2(radarPositionYStart + HaloArmour.radarOffsetYConfig.Value, radarPositionXStart + HaloArmour.radarOffsetXConfig.Value);
                        }
                        if (radarHudBasePosition.localScale.y != radarScaleStart.y * HaloArmour.radarScaleOffsetConfig.Value && radarHudBasePosition.localScale.x != radarScaleStart.x * HaloArmour.radarScaleOffsetConfig.Value)
                        {
                            radarHudBasePosition.localScale = new Vector2(radarScaleStart.y * HaloArmour.radarScaleOffsetConfig.Value, radarScaleStart.x * HaloArmour.radarScaleOffsetConfig.Value);
                        }
                        UpdateEnemyObjects();
                    }
                    else if (radarHud != null)
                    {
                        radarHud.SetActive(false);
                    }
                    if (radarHud != null)
                    {
                        radarHudBlipBasePosition.GetComponent<RectTransform>().eulerAngles = new Vector3(0, 0, playerCamera.transform.eulerAngles.y);
                    }
                }
            }
        }

        private void StartPulseAnimation()
        {
            // Stop any previous pulse coroutine
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
            }
            // Start the pulse coroutine
            pulseCoroutine = StartCoroutine(PulseCoroutine());
        }

        private IEnumerator PulseCoroutine()
        {
            while (true)
            {
                // Scale from 0 to 1 over the animation duration
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / animationDuration;
                    float scale = Mathf.Lerp(0f, 1f, t);

                    // Apply the scale to all axes
                    radarHudPulse.localScale = new Vector3(scale, scale, scale);

                    yield return null;
                }
                // Reset the scale to 0
                radarHudPulse.localScale = Vector3.zero;
                // Pause for the specified duration
                yield return new WaitForSeconds(pauseDuration);
            }
        }
        private void UpdateEnemyObjects()
        {
            // Get the current players in gameWorld.AllPlayers and convert to a list
            List<Player> players = gameWorld.AllPlayers.ToList();

            // Exclude gameWorld.MainPlayer from the players list
            players.Remove(gameWorld.MainPlayer);

            // Resize the enemyObjects array to match the number of players
            enemyObjects = new GameObject[players.Count];

            // Add the player game objects to the enemyObjects array
            for (int i = 0; i < players.Count; i++)
            {
                enemyObjects[i] = players[i].gameObject;
            }

            List<GameObject> activeEnemyObjects = new List<GameObject>();
            List<GameObject> blipsToRemove = new List<GameObject>();
            foreach (GameObject enemyObject in enemyObjects)
            {
                // Calculate the relative position of the enemy object
                Vector3 relativePosition = enemyObject.transform.position - playerTransform.position;

                // Check if the enemy is within the radar range
                if (relativePosition.magnitude <= radarRange)
                {
                    // Update blips on the radar for the enemies.
                    activeEnemyObjects.Add(enemyObject);
                }
                else
                {
                    // Remove the blip if the enemy is outside the radar range
                    blipsToRemove.Add(enemyObject);
                }
            }
            // Remove blips for any enemy objects that are no longer active
            RemoveInactiveEnemyBlips(activeEnemyObjects, blipsToRemove);
            foreach (GameObject enemyObject in activeEnemyObjects)
            {
                // Update blips on the radar for the enemies.
                UpdateBlips(enemyObject);
            }
            foreach (GameObject enemyObject in blipsToRemove)
            {
                // Remove out of range blips on the radar for the enemies.
                RemoveOutOfRangeBlip(enemyObject);
            }
            foreach (var enemyBlip in enemyBlips.Keys.ToList())
            {
                if (!activeEnemyObjects.Contains(enemyBlip))
                {
                    RemoveOutOfRangeBlip(enemyBlip);
                }
            }
        }
        private void UpdateBlips(GameObject enemyObject)
        {
            if (enemyObject != null && enemyObject.activeInHierarchy)
            {
                float x = enemyObject.transform.position.x - player.Transform.position.x;
                float z = enemyObject.transform.position.z - player.Transform.position.z;

                // Check if a blip already exists for the enemy object
                if (!enemyBlips.TryGetValue(enemyObject, out GameObject blip))
                {
                    // Instantiate a blip game object and set its position relative to the radar HUD
                    var radarHudBlipBase = Instantiate(RadarBliphudPrefab, radarHudBlipBasePosition.position, radarHudBlipBasePosition.rotation);
                    radarBlipHud = radarHudBlipBase as GameObject;
                    radarBlipHud.transform.parent = radarHudBlipBasePosition.transform;
                    EnemyBlip = radarBundle.LoadAsset<Sprite>("EnemyBlip");
                    EnemyBlipUp = radarBundle.LoadAsset<Sprite>("EnemyBlipUp");
                    EnemyBlipDown = radarBundle.LoadAsset<Sprite>("EnemyBlipDown");
                    // Add the enemy object and its blip to the dictionary
                    enemyBlips.Add(enemyObject, radarBlipHud);
                    blip = radarBlipHud;
                }

                if (blip != null)
                {
                    // Update the blip's image component based on y distance to enemy.
                    radarHudBlip = blip.transform.Find("Blip/RadarEnemyBlip") as RectTransform;
                    blipImage = radarHudBlip.GetComponent<Image>();
                    float yDifference = enemyObject.transform.position.y - player.Transform.position.y;
                    float totalThreshold = (HaloArmour.playerHeight + HaloArmour.playerHeight / 2f) * HaloArmour.radarHeightThresholdeScaleOffsetConfig.Value;
                    if (Mathf.Abs(yDifference) <= totalThreshold)
                    {
                        blipImage.sprite = EnemyBlip;
                    }
                    else if (yDifference > totalThreshold)
                    {
                        blipImage.sprite = EnemyBlipUp;
                    }
                    else if (yDifference < -totalThreshold)
                    {
                        blipImage.sprite = EnemyBlipDown;
                    }
                    blip.transform.parent = radarHudBlipBasePosition.transform;
                    // Apply the scale to the blip

                    // Apply the rotation of the parent transform
                    Quaternion parentRotation = radarHudBlipBasePosition.rotation;
                    Vector3 rotatedDirection = parentRotation * Vector3.forward;

                    // Calculate the angle based on the rotated direction
                    float angle = Mathf.Atan2(rotatedDirection.x, rotatedDirection.z) * Mathf.Rad2Deg;

                    // Calculate the position based on the angle and distance
                    float distance = Mathf.Sqrt(x * x + z * z);
                    // Calculate the offset factor based on the distance
                    float offsetFactor = Mathf.Clamp(distance / radarRange, 2f, 4f);
                    float offsetDistance = (distance * offsetFactor) * HaloArmour.radarDistanceScaleOffsetConfig.Value;
                    float angleInRadians = Mathf.Atan2(x, z);
                    Vector2 position = new Vector2(Mathf.Sin(angleInRadians - angle * Mathf.Deg2Rad), Mathf.Cos(angleInRadians - angle * Mathf.Deg2Rad)) * offsetDistance;

                    // Get the scale of the radarHudBlipBasePosition
                    Vector3 scale = radarHudBlipBasePosition.localScale;
                    // Multiply the sizeDelta by the scale to account for scaling
                    Vector2 scaledSizeDelta = radarHudBlipBasePosition.sizeDelta;
                    scaledSizeDelta.x *= scale.x;
                    scaledSizeDelta.y *= scale.y;
                    // Calculate the radius of the circular boundary
                    float radius = Mathf.Min(scaledSizeDelta.x, scaledSizeDelta.y) * 0.5f;
                    // Clamp the position within the circular boundary
                    float distanceFromCenter = position.magnitude;
                    if (distanceFromCenter > radius)
                    {
                        position = position.normalized * radius;
                    }
                    // Set the local position of the blip
                    blip.transform.localPosition = position;
                    Quaternion reverseRotation = Quaternion.Inverse(radarHudBlipBasePosition.rotation);
                    blip.transform.localRotation = reverseRotation;
                }
            }
            else
            {
                // Remove the inactive enemy blips from the dictionary and destroy the blip game objects
                if (enemyBlips.TryGetValue(enemyObject, out GameObject blip))
                {
                    enemyBlips.Remove(enemyObject);
                    Destroy(blip);
                }
            }
        }

        private void RemoveOutOfRangeBlip(GameObject enemyObject)
        {
            if (enemyBlips.ContainsKey(enemyObject))
            {
                // Remove the blip game object from the scene
                GameObject blip = enemyBlips[enemyObject];
                enemyBlips.Remove(enemyObject);
                Destroy(blip);
            }
        }
        private void RemoveInactiveEnemyBlips(List<GameObject> activeEnemyObjects, List<GameObject> blipsToRemove)
        {
            // Create a list to store the enemy objects that need to be removed
            List<GameObject> enemiesToRemove = new List<GameObject>();

            // Iterate through the enemyBlips dictionary
            foreach (var enemyBlip in enemyBlips)
            {
                GameObject enemyObject = enemyBlip.Key;

                // Check if the enemy object is not in the activeEnemyObjects list
                if (!activeEnemyObjects.Contains(enemyObject))
                {
                    // Add the enemy object to the enemiesToRemove list
                    enemiesToRemove.Add(enemyObject);
                }
            }

            // Iterate through the enemiesToRemove list and remove blips
            foreach (GameObject enemyObject in enemiesToRemove)
            {
                // Check if the enemy object exists in the enemyBlips dictionary
                if (enemyBlips.TryGetValue(enemyObject, out GameObject blip))
                {
                    // Remove the blip game object from the scene
                    enemyBlips.Remove(enemyObject);
                    Destroy(blip);
                }
            }
        }


    }
    public class ShieldPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Player).GetMethod("ReceiveDamage", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void PostFix(ref Player __instance, float damage, EBodyPart part, EDamageType type)
        {
            if (HaloArmour.patchesEnabled && HaloShield.currentShield >= 1 && HaloArmour.shieldEnabledConfig.Value)
            {
                    float bodyPartMult = 1f;
                    if (__instance.IsYourPlayer && part == EBodyPart.Head)
                    {
                        bodyPartMult = 1.5f;
                    }
                    if (__instance.IsYourPlayer && (part == EBodyPart.Chest || part == EBodyPart.Stomach))
                    {
                        bodyPartMult = 1.25f;
                    }
                    if (__instance.IsYourPlayer && (part == EBodyPart.LeftArm || part == EBodyPart.RightArm || part == EBodyPart.LeftLeg || part == EBodyPart.RightLeg || part == EBodyPart.Common))
                    {
                        bodyPartMult = 1f;
                    }
                    if (__instance.IsYourPlayer && type != EDamageType.Fall)
                    {
                        HaloShield.shieldGotHit = true;
                        HaloShield.lastShield = HaloShield.currentShield;
                        HaloShield.currentShield = Mathf.Max(0, HaloShield.currentShield - Mathf.RoundToInt((damage / 15) * bodyPartMult));
                    }
                    if (__instance.IsYourPlayer && type == EDamageType.Fall)
                    {
                        HaloShield.shieldGotHit = true;
                        HaloShield.lastShield = HaloShield.currentShield;
                        HaloShield.currentShield = 0;
                    }
            }
        }
    }
    public class HealPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Player).GetMethod("HealthControllerUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void PostFix(ref Player __instance)
        {
            if (HaloArmour.patchesEnabled && __instance.IsYourPlayer)
            {
                HaloShield.playerHealthChanged = true;
            }
        }
    }

    public class DeadPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Player).GetMethod("OnDead", BindingFlags.Instance | BindingFlags.NonPublic);

        [PatchPostfix]
        static void PostFix(ref Player __instance)
        {
            if (HaloArmour.patchesEnabled && __instance.IsYourPlayer)
            {
                HaloShield.DeathHandler(__instance);
            }
        }
    }

    public class DoorKickPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod() => typeof(Door).GetMethod("BreachSuccessRoll", BindingFlags.Instance | BindingFlags.Public);
        [PatchPostfix]
        static void PostFix(ref bool __result)
        {
            if (HaloArmour.patchesEnabled && HaloArmour.doorKickerEnabledConfig.Value)
            {
                __result = true;
            }
        }
    }
}