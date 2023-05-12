using BepInEx;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.AddressableAssets;
using System.Linq;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.SceneManagement;

namespace DualPunches
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, "1.0.2")]
    public class Plugin : BaseUnityPlugin
    {
        public static ResourceLocationMap resourceMap = null;
        public static T LoadObject<T>(string path)
        {
            if (resourceMap == null)
            {
                Addressables.InitializeAsync().WaitForCompletion();
                resourceMap = Addressables.ResourceLocators.First() as ResourceLocationMap;
            }

            Debug.Log($"Loading {path}");
            KeyValuePair<object, IList<IResourceLocation>> obj;
            try
            {
                obj = resourceMap.Locations.Where(
                    (KeyValuePair<object, IList<IResourceLocation>> pair) =>
                    {
                        return (pair.Key as string) == path;
                        //return (pair.Key as string).Equals(path, StringComparison.OrdinalIgnoreCase);
                    }).First();
            }
            catch (Exception) { return default(T); }

            return Addressables.LoadAsset<T>(obj.Value.First()).WaitForCompletion();
        }

        public Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID + "_harmony");
        public static GameObject blueArm;
        public static GameObject redArm;
        public static GameObject goldenArm;

        static PropertyInfo hookArmInstance = typeof(MonoSingleton<HookArm>).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        public static bool fastPunchEnabled = false;
        private void OnSceneLoad(Scene before, Scene after)
        {
            //[HarmonyBefore(new string[] { "tempy.fastpunch" })]
            fastPunchEnabled = Harmony.HasAnyPatches("tempy.fastpunch");

            GameObject player = GameObject.Find("Player");
            if (player == null)
                return;
            HookArm original = HookArm.Instance;
            goldenArm = GameObject.Instantiate(player.transform.Find("Main Camera/Punch/Hook Arm").gameObject);
            goldenArm.SetActive(false);
            hookArmInstance.SetValue(null, original);
        }

        private void Awake()
        {
            // Plugin startup logic
            blueArm = LoadObject<GameObject>("Assets/Prefabs/Weapons/Arm Blue.prefab");
            redArm = LoadObject<GameObject>("Assets/Prefabs/Weapons/Arm Red.prefab");
            SceneManager.activeSceneChanged += OnSceneLoad;
            harmony.PatchAll();


            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }
    }

    /*
    This was an attempt to make projectile boost stop intervals shorter 
    */
    /*[HarmonyPatch(typeof(TimeController), "ParryFlash")]
    public class ParryFlash
    {
        static GameObject currentFlash;
        static GameObject templateFlash;

        static bool Prefix(TimeController __instance, ref GameObject ___parryLight)
        {
            if (templateFlash == null)
            {
                templateFlash = ___parryLight;
                ___parryLight = new GameObject();
            }
            return true;
        }

        static void Postfix(TimeController __instance, GameObject ___parryLight)
        {
            if (currentFlash != null)
                GameObject.Destroy(currentFlash);

            currentFlash = GameObject.Instantiate<GameObject>(templateFlash, MonoSingleton<PlayerTracker>.Instance.GetTarget().position, Quaternion.identity, MonoSingleton<PlayerTracker>.Instance.GetTarget());
            foreach(AudioSource src in currentFlash.GetComponents<AudioSource>())
            {
                src.priority = 1000000;
            }
        }
    }

    [HarmonyPatch(typeof(TimeController), "TrueStop")]
    public class TrueStopShortener
    {
        static float lastTime = 0;

        static int requiredFreezes = 2;
        static int freezeMulti = 1;

        static bool Prefix(TimeController __instance, ref float __0)
        {
            if(lastTime > Time.time)
            {
                lastTime = Time.time;
                return true;
            }

            float deltaTime = Time.time - lastTime;
            lastTime = Time.time;
            if(deltaTime > 1f)
            {
                requiredFreezes = 2;
                freezeMulti = 1;
            }
            else
            {
                if(requiredFreezes == 0)
                {
                    freezeMulti += 1;
                    requiredFreezes = 0;//1 << freezeMulti;
                }
                else
                {
                    requiredFreezes -= 1;
                }
            }

            __0 /= freezeMulti;
            return true;
        }
    }*/

    [HarmonyPatch(typeof(DualWieldPickup), "PickedUp")]
    public class DualWeaponPickup_Patch
    {
        static void Postfix(DualWieldPickup __instance)
        {
            Transform armContainer = FistControl.Instance.transform;
            GameObject newPunch = new GameObject();
            newPunch.transform.SetParent(armContainer, true);
            newPunch.transform.localRotation = Quaternion.identity;

            DualPunch[] componentsInChildren = armContainer.GetComponentsInChildren<DualPunch>();
            if (componentsInChildren != null && componentsInChildren.Length % 2 == 0)
            {
                newPunch.transform.localScale = new Vector3(-1f, 1f, 1f);
            }
            else
            {
                newPunch.transform.localScale = Vector3.one;
            }
            if (componentsInChildren == null || componentsInChildren.Length == 0)
            {
                newPunch.transform.localPosition = Vector3.zero;
            }
            else if (componentsInChildren.Length % 2 == 0)
            {
                newPunch.transform.localPosition = new Vector3((float)((componentsInChildren.Length + 2) / 2) * -1.5f, 0f, 0f);
            }
            else
            {
                newPunch.transform.localPosition = new Vector3((float)((componentsInChildren.Length + 3) / 2) * 1.5f, 0f, 0f);
            }

            DualPunch dualWield = newPunch.AddComponent<DualPunch>();
            dualWield.delay = 0.05f;
            dualWield.juiceAmount = __instance.juiceAmount;
            if (componentsInChildren != null && componentsInChildren.Length != 0)
            {
                dualWield.delay += (float)componentsInChildren.Length / 20f;
            }
        }
    }
}
