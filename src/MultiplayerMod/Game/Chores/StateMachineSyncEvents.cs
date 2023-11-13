using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.ModRuntime.Context;
using MultiplayerMod.ModRuntime.Loader;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Multiplayer.Commands.Chores;
using MultiplayerMod.Multiplayer.CoreOperations;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.Objects.Reference;

namespace MultiplayerMod.Game.Chores;

////[HarmonyPatch(typeof(StateMachine.Instance), nameof(StateMachine.Instance.StartSM))]
//public class StateMachineStartSMPatch
//{
//    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<StateMachineStartSMPatch>();
//    delegate void GotoFunc(StateMachine.BaseState base_state);

//    static void Prefix(StateMachine.Instance __instance)
//    {

//        StateMachineInstancePatch.PatchGoTo(__instance);
//    }
//}

//[HarmonyPatch(typeof(StateMachine.Instance), MethodType.Constructor, new Type[] { typeof(StateMachine), typeof(IStateMachineTarget) })]
public class StateMachineInstancePatch
{
    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<StateMachineInstancePatch>();
    public static HashSet<Type> patchedTypes = new HashSet<Type>();
    delegate void GotoFunc(StateMachine.BaseState base_state);
    private static bool patched = false;

    public static void Prefix(StateMachine.Instance __instance)
    {
        log.Level = LogLevel.Debug;
        //if (patchedTypes.Contains(__instance.GetType()))
        //{
        //    return;
        //}

        PatchGoTo(__instance);
    }

    public static void PatchGoTo(StateMachine.Instance __instance)
    {
        log.Level = LogLevel.Debug;

        GotoFunc original = __instance.GoTo;
        var prefix = StateMachineSyncEvents.Prefix;
        var postfix = StateMachineSyncEvents.Postfix;
        var prefixMethod = new HarmonyMethod(prefix.Method);
        var postfixMethod = new HarmonyMethod(postfix.Method);

        ModLoader.HarmonyInstance!.Patch(original.Method, prefix: prefixMethod, postfix: postfixMethod);
        patchedTypes.Add(__instance.GetType());
        log.Debug($"Patched {__instance.GetType()}");
    }
}

//[HarmonyPatch(typeof(GameStateMachine<AggressiveChore.States, AggressiveChore.StatesInstance, AggressiveChore, object>.GenericInstance), nameof(GameStateMachine<AggressiveChore.States, AggressiveChore.StatesInstance, AggressiveChore, object>.GenericInstance.GoTo), new Type[] {typeof(StateMachine.BaseState)})]
public class StateMachineSyncEvents
{
    public static HashSet<StateMachine.Instance> SyncedInstances = new HashSet<StateMachine.Instance>();
    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<StateMachineSyncEvents>();
    public static event Action<GotoStateEventArgs>? GotoState;

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    //[RequireMultiplayerMode(MultiplayerMode.Host)]
    //[RequireExecutionLevel(ExecutionLevel.Game)]
    public static bool Prefix(StateMachine.Instance __instance, ref StateMachine.BaseState base_state, ref StateMachine.BaseState __state)
    {
        log.Level = LogLevel.Debug;
        __state = __instance.GetCurrentState();
        var instanceType = __instance.GetType().BaseType.BaseType;
        var isMasterNullProp = __instance.GetType().GetProperty("isMasterNull", BindingFlags.Public | BindingFlags.Instance);
        bool isMasterNull = (bool) isMasterNullProp.GetValue(__instance);

        if (App.IsExiting || StateMachine.Instance.error || isMasterNull)
            return false;
        if (__instance.IsNullOrDestroyed())
            return false;
        //try
        //{
        if (__instance.IsBreakOnGoToEnabled())
            Debugger.Break();
        if (base_state != null)
        {
            while (base_state.defaultState != null)
                base_state = base_state.defaultState;
        }
        if (__instance.GetCurrentState() == null)
            __instance.SetStatus(StateMachine.Status.Running);
        Stack<StateMachine.BaseState> gotoStack = (Stack<StateMachine.BaseState>) instanceType.GetField("gotoStack", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
        if (gotoStack.Count > 100)
        {
            string str = "Potential infinite transition loop detected in state machine: " + __instance.ToString() + "\nGoto stack:\n";
            foreach (StateMachine.BaseState baseState in gotoStack)
                str = str + "\n" + baseState.name;
            log.Error(str);
            __instance.Error();
        } else
        {
            gotoStack.Push(base_state!);
            if (base_state == null)
            {
                __instance.StopSM("StateMachine.GoTo(null)");
                gotoStack.Pop();
            } else
            {
                int gotoId = (int) instanceType.GetField("gotoId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
                var stateStack = instanceType.GetField("stateStack", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);

                Type InnerStackEntryBaseType = instanceType.GetNestedType("StackEntry");
                Type InnerStackEntryType = InnerStackEntryBaseType.MakeGenericType(__instance.GetType().BaseType.GetGenericArguments());

                int num = ++gotoId;
                instanceType.GetField("gotoId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(__instance, num);
                Type innerStateBaseType = __instance.stateMachine.GetType().BaseType.BaseType.GetNestedType("State");
                Type InnerStateType = innerStateBaseType.MakeGenericType(__instance.GetType().BaseType.GetGenericArguments());
                var GetStackEntryStateField = InnerStackEntryType.GetField("state", BindingFlags.Public | BindingFlags.Instance);
                var StackEntryArrayGetValue = InnerStackEntryType.MakeArrayType().GetMethod("GetValue", new Type[] { typeof(int) });
                StateMachine.BaseState[] branch = (StateMachine.BaseState[]) InnerStateType.GetField("branch", BindingFlags.Public | BindingFlags.Instance).GetValue(base_state);
                int index1 = 0;
                while (index1 < __instance.stackSize && index1 < branch.Length && GetStackEntryStateField.GetValue(StackEntryArrayGetValue.Invoke(stateStack, new object[] { index1 })) == branch[index1])
                    ++index1;
                int index2 = __instance.stackSize - 1;
                if (index2 >= 0 && index2 == index1 - 1)
                {
                    StateMachine.BaseState state2 = (StateMachine.BaseState) GetStackEntryStateField.GetValue(StackEntryArrayGetValue.Invoke(stateStack, new object[] { index2 }));


                    instanceType.GetMethod("FinishStateInProgress", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { state2 });
                }
                while (__instance.stackSize > index1 && num == (int) instanceType.GetField("gotoId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance))
                {
                    instanceType.GetMethod("PopState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
                }
                for (int index3 = index1; index3 < branch.Length && num == (int) instanceType.GetField("gotoId", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance); ++index3)
                {
                    instanceType.GetMethod("PushState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { branch[index3] });

                }
                gotoStack.Pop();
            }
        }
        //} catch (Exception ex)
        //{
        //    if (StateMachine.Instance.error)
        //        return;
        //    __instance.Error();
        //    string str1 = "(Stop)";
        //    if (base_state != null)
        //        str1 = base_state.name;
        //    string str2 = "(NULL).";
        //    if (!__instance.GetMaster().isNull)
        //        str2 = "(" + __instance.gameObject.name + ").";
        //    Debugger.Break();
        //    //DebugUtil.LogErrorArgs((UnityEngine.Object) __instance.controller, (object) ("Exception in: " + str2 + this.stateMachine.ToString() + ".GoTo(" + str1 + ")" + "\n" + ex.ToString()));
        //}
        return false;
    }

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    //[RequireMultiplayerMode(MultiplayerMode.Host)]
    //[RequireExecutionLevel(ExecutionLevel.Game)]
    public static void Postfix(StateMachine.Instance __instance, ref StateMachine.BaseState base_state, ref StateMachine.BaseState __state)
    {
        log.Level = LogLevel.Debug;
        //log.Debug($"Goto state: {__instance} {SyncedInstances.Contains(__instance)} {base_state}");
        if (!SyncedInstances.Contains(__instance))
        {
            return;
        }
        log.Debug($"Goto state: {__instance} {SyncedInstances.Contains(__instance)} {base_state}");

        var args = new GotoStateEventArgs(
            __instance.GetReference(),
            __state?.name,
            base_state?.name
        );
        log.Debug(
            $"Triggering state change {args.StateMachineRef} : {args.PreviousStateName} -> {args.NextStateName}"
        );
        GotoState?.Invoke(
            args
        );
    }

}

public record GotoStateEventArgs(
    StateMachineReference StateMachineRef,
    string? PreviousStateName,
    string? NextStateName
);
