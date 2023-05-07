using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace DualPunches
{
    public class HookArmCloneComp : MonoBehaviour
    {
        public bool attemptedToThrow = false;
    }

    public class DualPunch : MonoBehaviour
    {
        public float delay = 0f;
        public float juiceAmount = 0f;
        private bool juiceGiven = false;

        private GameObject currentPunch = null;
        private Punch currentComp = null;
        private GameObject currentHookarm;
        private HookArm currentHookarmComp;
        private HookArmCloneComp currentHookarmFlag;
        private Animator anim;
        private GameObject copyTarget;
        private PowerUpMeter meter;
        private FistControl fc;

        private HookArm originalInstance;

        static PropertyInfo hookArmInstance = typeof(MonoSingleton<HookArm>).GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        private void Start()
        {
            this.fc = MonoSingleton<FistControl>.Instance;
            copyTarget = fc.currentArmObject;
            this.meter = MonoSingleton<PowerUpMeter>.Instance;
            if (this.juiceAmount == 0f)
            {
                this.juiceAmount = 30f;
            }
            if (this.meter.juice < this.juiceAmount)
            {
                this.meter.latestMaxJuice = this.juiceAmount;
                this.meter.juice = this.juiceAmount;
            }
            this.meter.powerUpColor = new Color(1f, 0.6f, 0f);
            this.juiceGiven = true;

            if (this.fc.currentArmObject)
            {
                this.UpdatePunch();
            }

            originalInstance = HookArm.Instance;
            currentHookarm = GameObject.Instantiate(Plugin.goldenArm/*fc.gameObject.GetComponentInChildren<HookArm>().gameObject*/, transform);
            currentHookarm.SetActive(true);
            hookArmInstance.SetValue(null, originalInstance);

            //currentHookarm.transform.localRotation = Quaternion.identity;
            currentHookarmComp = currentHookarm.GetComponent<HookArm>();
            currentHookarmFlag = currentHookarmComp.gameObject.AddComponent<HookArmCloneComp>();
            //currentHookarmComp.enabled = false;
            //currentHookarmComp.SendMessage("Start");
        }

        private void UpdatePunch()
        {
            if (currentPunch != null)
                Destroy(currentPunch);

            copyTarget = fc.currentArmObject;
            if (copyTarget == null)
                return;

            if(copyTarget.gameObject.name.StartsWith("Arm Blue"))
                currentPunch = Instantiate(Plugin.blueArm, transform);
            else if(copyTarget.gameObject.name.StartsWith("Arm Red"))
                currentPunch = Instantiate(Plugin.redArm, transform);
            else
                currentPunch = Instantiate(copyTarget, transform);

            Punch comp = currentComp = currentPunch.GetComponent<Punch>();
            comp.Invoke("Start", 0f);
            comp.enabled = false;
            anim = currentPunch.GetComponent<Animator>();
        }

        bool createdArm = false;

        static FieldInfo hookarmCooldown = typeof(HookArm).GetField("cooldown", BindingFlags.NonPublic | BindingFlags.Instance);
        private bool readyToPunch = false;
        private void Update()
        {
            if(HookArm.Instance != originalInstance)
                hookArmInstance.SetValue(null, originalInstance);

            if (fc.fistCooldown <= 0)
                readyToPunch = true;

            if (!createdArm)
            {
                UpdatePunch();
                createdArm = true;
            }

            if (this.juiceGiven && this.meter.juice <= 0f)
            {
                this.EndPowerUp();
                return;
            }
            if (!this.copyTarget || this.copyTarget != this.fc.currentArmObject)
            {
                this.UpdatePunch();
            }
            if (this.currentPunch)
            {
                if (!this.fc.currentArmObject.activeInHierarchy && this.currentPunch.activeSelf)
                {
                    this.currentPunch.SetActive(false);
                    return;
                }
                if (this.fc.currentArmObject.activeInHierarchy && !this.currentPunch.activeSelf)
                {
                    this.currentPunch.SetActive(true);
                }
            }

            if(this.currentPunch != null)
            {
                if (MonoSingleton<InputManager>.Instance.InputSource.Hook.IsPressed)
                {
                    currentComp.CancelInvoke("PunchStart");
                    currentComp.SendMessage("CancelAttack");
                    if(MonoSingleton<InputManager>.Instance.InputSource.Hook.WasPerformedThisFrame)
                    {
                        if (currentHookarmComp.state == HookState.Ready)
                            currentHookarmFlag.attemptedToThrow = true;
                    }
                }
                else
                {
                    if (currentHookarmComp.state == HookState.Ready)
                    {
                        hookarmCooldown.SetValue(currentHookarmComp, delay);
                        currentHookarmFlag.attemptedToThrow = false;
                    }

                    bool punchButton = MonoSingleton<InputManager>.Instance.InputSource.Punch.WasPerformedThisFrame;
                    if(Plugin.fastPunchEnabled)
                    {
                        if (currentPunch.name.StartsWith("Arm Red"))
                            punchButton = MonoSingleton<InputManager>.Instance.InputSource.ChangeFist.WasPerformedThisFrame;
                    }

                    if (punchButton && currentComp.ready && (this.fc.fistCooldown <= 0f || readyToPunch) && this.fc.activated && !GameStateManager.Instance.PlayerInputLocked)
                    {
                        currentComp.Invoke("PunchStart", delay);
                        currentHookarmComp.Invoke("Cancel", delay);
                        readyToPunch = false;
                        //currentHookarmComp.SendMessage("Cancel");
                    }
                }
            }
        }

        static FieldInfo hookarmCurrentEid = typeof(HookArm).GetField("caughtEid", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        static FieldInfo hookarmEnemyGroundCheck = typeof(HookArm).GetField("enemyGroundCheck", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        public void EndPowerUp()
        {
            if(currentHookarm != null)
            {
                EnemyIdentifier eid = (EnemyIdentifier)hookarmCurrentEid.GetValue(currentHookarmComp);
                if(eid != null)
                {
                    eid.hooked = false;
                    GroundCheckEnemy gce = (GroundCheckEnemy)hookarmEnemyGroundCheck.GetValue(currentHookarmComp);
                    if (gce != null)
                        gce.StopForceOff();
                    if (eid.gce != null)
                        eid.gce.ForceOff();
                }
            }

            Destroy(base.gameObject);
        }
    }

    [HarmonyPatch(typeof(HookArm), "Update")]
    class HookArm_Update
    {
        // This must be ran as the lastest patch for possible
        // compability with other plugins
        [HarmonyAfter]
        static bool Prefix(HookArm __instance, GameObject ___model, ref float ___cooldown, ref bool ___forcingFistControl,
            LineRenderer ___lr, ref Vector3 ___caughtPoint, ref bool ___returning, ref Vector3 ___hookPoint,
            ref Vector3 ___previousHookPoint, CameraFrustumTargeter ___targeter, ref Vector3 ___throwDirection,
            List<Rigidbody> ___caughtObjects, ref bool ___lightTarget, Animator ___anim, LineRenderer ___inspectLr,
            ref float ___throwWarp, AudioSource ___aud, ref float ___semiBlocked, ref Transform ___caughtTransform,
            ref Collider ___caughtCollider, ref EnemyIdentifier ___caughtEid, float ___returnDistance)
        {
            if (!MonoSingleton<OptionsManager>.Instance || MonoSingleton<OptionsManager>.Instance.paused)
            {
                return false;
            }
            if (!HookArm.Instance.equipped || MonoSingleton<FistControl>.Instance.shopping || !MonoSingleton<FistControl>.Instance.activated)
            {
                if (__instance.state != HookState.Ready || ___returning)
                {
                    __instance.Cancel();
                }
                ___model.SetActive(false);
                return false;
            }
            HookArmCloneComp comp = __instance.GetComponent<HookArmCloneComp>();
            if (  ((comp == null && MonoSingleton<InputManager>.Instance.InputSource.Hook.WasPerformedThisFrame)
                || (comp != null && comp.attemptedToThrow && MonoSingleton<InputManager>.Instance.InputSource.Hook.IsPressed)))//CHANGED
            {
                if (__instance.state == HookState.Pulling)
                {
                    __instance.StopThrow(0f, false);
                }
                else if (___cooldown <= 0f)
                {
                    if(comp)
                        comp.attemptedToThrow = false;
                    ___cooldown = 0.5f;
                    ___model.SetActive(true);
                    if (!___forcingFistControl)
                    {
                        if (MonoSingleton<FistControl>.Instance.currentPunch)
                        {
                            MonoSingleton<FistControl>.Instance.currentPunch.CancelAttack();
                        }
                        MonoSingleton<FistControl>.Instance.forceNoHold++;
                        ___forcingFistControl = true;
                        MonoSingleton<FistControl>.Instance.transform.localRotation = Quaternion.identity;
                    }
                    ___lr.enabled = true;
                    ___hookPoint = __instance.transform.position;
                    ___previousHookPoint = ___hookPoint;
                    if (___targeter.CurrentTarget && ___targeter.IsAutoAimed)
                    {
                        ___throwDirection = (___targeter.CurrentTarget.bounds.center - __instance.transform.position).normalized;
                    }
                    else
                    {
                        ___throwDirection = __instance.transform.forward;
                    }
                    ___returning = false;
                    if (___caughtObjects.Count > 0)
                    {
                        foreach (Rigidbody rigidbody in ___caughtObjects)
                        {
                            if (rigidbody)
                            {
                                rigidbody.velocity = (MonoSingleton<NewMovement>.Instance.transform.position - rigidbody.transform.position).normalized * (100f + ___returnDistance / 2f);
                            }
                        }
                        ___caughtObjects.Clear();
                    }
                    __instance.state = HookState.Throwing;
                    ___lightTarget = false;
                    ___throwWarp = 1f;
                    ___anim.Play("Throw", -1, 0f);
                    ___inspectLr.enabled = false;
                    __instance.hand.transform.localPosition = new Vector3(0.09f, -0.051f, 0.045f);
                    if (MonoSingleton<CameraController>.Instance.defaultFov > 105f)
                    {
                        __instance.hand.transform.localPosition += new Vector3(0.225f * ((MonoSingleton<CameraController>.Instance.defaultFov - 105f) / 55f), -0.25f * ((MonoSingleton<CameraController>.Instance.defaultFov - 105f) / 55f), 0.05f * ((MonoSingleton<CameraController>.Instance.defaultFov - 105f) / 55f));
                    }
                    ___caughtPoint = Vector3.zero;
                    ___caughtTransform = null;
                    ___caughtCollider = null;
                    ___caughtEid = null;
                    GameObject.Instantiate<GameObject>(__instance.throwSound);
                    ___aud.clip = __instance.throwLoop;
                    ___aud.panStereo = 0f;
                    ___aud.Play();
                    ___aud.pitch = UnityEngine.Random.Range(0.9f, 1.1f);
                    ___semiBlocked = 0f;
                    MonoSingleton<RumbleManager>.Instance.SetVibrationTracked("rumble.whiplash.throw", __instance.gameObject);
                }
            }
            if (___cooldown != 0f)
            {
                ___cooldown = Mathf.MoveTowards(___cooldown, 0f, Time.deltaTime);
            }
            if (___lr.enabled)
            {
                ___throwWarp = Mathf.MoveTowards(___throwWarp, 0f, Time.deltaTime * 6.5f);
                ___lr.SetPosition(0, __instance.hand.position);
                for (int i = 1; i < ___lr.positionCount - 1; i++)
                {
                    float d = 3f;
                    if (i % 2 == 0)
                    {
                        d = -3f;
                    }
                    ___lr.SetPosition(i, Vector3.Lerp(__instance.hand.position, ___hookPoint, (float)i / (float)___lr.positionCount) + __instance.transform.up * d * ___throwWarp * (1f / (float)i));
                }
                ___lr.SetPosition(___lr.positionCount - 1, ___hookPoint);
            }
            if (__instance.state == HookState.Pulling && !___lightTarget && MonoSingleton<InputManager>.Instance.InputSource.Jump.WasPerformedThisFrame)
            {
                if (MonoSingleton<NewMovement>.Instance.rb.velocity.y < 1f)
                {
                    MonoSingleton<NewMovement>.Instance.rb.velocity = new Vector3(MonoSingleton<NewMovement>.Instance.rb.velocity.x, 1f, MonoSingleton<NewMovement>.Instance.rb.velocity.z);
                }
                MonoSingleton<NewMovement>.Instance.rb.velocity = Vector3.ClampMagnitude(MonoSingleton<NewMovement>.Instance.rb.velocity, 30f);
                if (!MonoSingleton<NewMovement>.Instance.gc.touchingGround && !Physics.Raycast(MonoSingleton<NewMovement>.Instance.gc.transform.position, Vector3.down, 1.5f, LayerMaskDefaults.Get(LMD.EnvironmentAndBigEnemies)))
                {
                    MonoSingleton<NewMovement>.Instance.rb.AddForce(Vector3.up * 15f, ForceMode.VelocityChange);
                }
                else if (!MonoSingleton<NewMovement>.Instance.jumping)
                {
                    MonoSingleton<NewMovement>.Instance.Jump();
                }
                __instance.StopThrow(1f, false);
            }
            if (MonoSingleton<FistControl>.Instance.currentPunch && MonoSingleton<FistControl>.Instance.currentPunch.holding && ___forcingFistControl)
            {
                MonoSingleton<FistControl>.Instance.currentPunch.heldItem.transform.position = __instance.hook.position + __instance.hook.up * 0.2f;
                if (__instance.state != HookState.Ready || ___returning)
                {
                    MonoSingleton<FistControl>.Instance.heldObject.hooked = true;
                    if (MonoSingleton<FistControl>.Instance.heldObject.gameObject.layer != 22)
                    {
                        Transform[] componentsInChildren = MonoSingleton<FistControl>.Instance.heldObject.GetComponentsInChildren<Transform>();
                        for (int j = 0; j < componentsInChildren.Length; j++)
                        {
                            componentsInChildren[j].gameObject.layer = 22;
                        }
                        return false;
                    }
                }
                else
                {
                    MonoSingleton<FistControl>.Instance.heldObject.hooked = false;
                    if (MonoSingleton<FistControl>.Instance.heldObject.gameObject.layer != 13)
                    {
                        Transform[] componentsInChildren = MonoSingleton<FistControl>.Instance.heldObject.GetComponentsInChildren<Transform>();
                        for (int j = 0; j < componentsInChildren.Length; j++)
                        {
                            componentsInChildren[j].gameObject.layer = 13;
                        }
                    }
                }
            }

            return false;
        }
    }

    [HarmonyPatch(typeof(HookArm), "FixedUpdate")]
    class HookArm_FixedUpdate
    {
        static ItemIdentifier temporaryIdentifier;
        public static bool cloneArmPlacingObject = false;

        struct StateInfo
        {
            public bool cloneArm;
            public bool holding;
            public ItemIdentifier heldObject;

            public StateInfo(bool holding, ItemIdentifier heldObject)
            {
                this.holding = holding;
                this.heldObject = heldObject;
                cloneArm = false;
            }
        }

        static bool Prefix(HookArm __instance, out StateInfo __state)
        {
            __state = new StateInfo(MonoSingleton<FistControl>.Instance.currentPunch.holding, MonoSingleton<FistControl>.Instance.heldObject);
            HookArmCloneComp flag = __instance.GetComponent<HookArmCloneComp>();
            if (flag == null)
                return true;
            __state.cloneArm = true;
            cloneArmPlacingObject = true;

            /*if(temporaryIdentifier == null)
            {
                GameObject obj = new GameObject();
                temporaryIdentifier = obj.AddComponent<ItemIdentifier>();
                temporaryIdentifier.itemType = ItemType.None;
            }

            MonoSingleton<FistControl>.Instance.currentPunch.holding = true;
            MonoSingleton<FistControl>.Instance.heldObject = temporaryIdentifier;*/
            return true;
        }

        static void Postfix(HookArm __instance, StateInfo __state)
        {
            if (!__state.cloneArm)
                return;
            //MonoSingleton<FistControl>.Instance.currentPunch.holding = __state.holding;
            //MonoSingleton<FistControl>.Instance.heldObject = __state.heldObject;
            cloneArmPlacingObject = false;
        }
    }

    [HarmonyPatch(typeof(Punch), "PlaceHeldObject")]
    class Punch_PlaceHeldObject
    {
        static bool Prefix(Punch __instance)
        {
            if (HookArm_FixedUpdate.cloneArmPlacingObject)
                return false;

            if (__instance.transform.parent.GetInstanceID() != FistControl.Instance.transform.GetInstanceID())
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(Punch), "ForceHold")]
    class Punch_ForceHold
    {
        static bool Prefix(Punch __instance)
        {
            if (HookArm_FixedUpdate.cloneArmPlacingObject)
            {
                //Debug.Log("Clone arm placing object");
                return false;
            }

            if (__instance.transform.parent.GetInstanceID() != FistControl.Instance.transform.GetInstanceID())
            {
                //Debug.Log($"Parent mismatch: {__instance.transform.parent.GetInstanceID()} - {FistControl.Instance.transform.GetInstanceID()}");
                return false;
            }

            return true;
        }
    }
}
