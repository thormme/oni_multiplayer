using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.ModRuntime.Context;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Multiplayer.CoreOperations;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.Objects.Reference;
using UnityEngine;

namespace MultiplayerMod.Game.Chores;

[HarmonyPatch(typeof(ChoreConsumer), nameof(ChoreConsumer.FindNextChore))]
public class ChoreConsumerEvents {

    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<ChoreConsumerEvents>();
    public static event Action<FindNextChoreEventArgs>? FindNextChore;

    private static Dictionary<ChoreConsumer, bool> choreFinishState = new Dictionary<ChoreConsumer, bool>();
    private static Dictionary<ChoreConsumer, Chore.Precondition.Context> lastChoreTriggered = new Dictionary<ChoreConsumer, Chore.Precondition.Context>();

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    [RequireMultiplayerMode(MultiplayerMode.Host)]
    [RequireExecutionLevel(ExecutionLevel.Game)]
    public static void Prefix(ChoreConsumer __instance, ref Chore.Precondition.Context out_context, ref bool __result)
    {
        log.Level = LogLevel.Debug;
        //log.Debug(
        //    $"Previous chore succeeded: {__instance.GetLastPreconditionSnapshot().succeededContexts.Count} -  {(__instance.GetLastPreconditionSnapshot().succeededContexts.Count > 0 ? __instance.GetLastPreconditionSnapshot().succeededContexts[0].chore : "none")} - Failed: {__instance.GetLastPreconditionSnapshot().failedContexts.Count} - {__instance.choreDriver.GetCurrentChore()}"
        //);
        var lastChore = lastChoreTriggered.GetValueSafe(__instance);
        choreFinishState[__instance] = lastChore != null && lastChore.chore != null && lastChore.chore.isComplete;
        // TODO: Do something more generic, like sending cancel events, using the built in chore preemtion, or at least having a list
        if (lastChore != null && lastChore.chore != null && lastChore.chore.GetType() == typeof(IdleChore))
        {
            choreFinishState[__instance] = false;
        }
    }

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    [RequireMultiplayerMode(MultiplayerMode.Host)]
    [RequireExecutionLevel(ExecutionLevel.Game)]
    public static void Postfix(ChoreConsumer __instance, Chore.Precondition.Context out_context, ref bool __result) {
        bool lastChoreSucceeded = choreFinishState[__instance];
        if (!__result)
            return;

        var kPrefabID = __instance.gameObject.GetComponent<KPrefabID>();
        var instanceId = kPrefabID.InstanceID;
        var choreId = out_context.chore.id;

        var choreObjectInstance = out_context.chore.gameObject.GetComponent<MultiplayerInstance>();
        var choreObjectId = choreObjectInstance.Register();
        var choreObjectReference = new MultiplayerIdReference(choreObjectId);


        MethodInfo? getSMIMethod = out_context.chore.GetType().GetMethod("GetSMI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);

        if (getSMIMethod == null )
        {
            log.Warning("getSMIMethod not found");
        }

        object[] getSMIParameters = { };
        StateMachine.Instance smi = (StateMachine.Instance) (getSMIMethod!.Invoke(out_context.chore, getSMIParameters));

        var args = new FindNextChoreEventArgs(
            instanceId,
            __instance.ToString(),
            Grid.PosToCell(__instance.transform.position),
            choreId,
            out_context.chore.GetType(),
            Grid.PosToCell(out_context.chore.gameObject.transform.position),
            out_context.isAttemptingOverride,
            lastChoreSucceeded,
            choreObjectReference,
            smi.GetReference()
        );

        // TODO: Remove smi after chore finished to prevent leak
        StateMachineSyncEvents.SyncedInstances.Add(smi);
        lastChoreTriggered[__instance] = out_context;
        log.Debug(
            $"Triggering {args.InstanceId} {args.InstanceString} {args.InstanceCell} {args.ChoreId} {args.ChoreType} {args.ChoreCell} {args.LastChoreSucceeded} {args.ChoreObjectReference.Id}"
        );
        FindNextChore?.Invoke(
            args
        );
    }

}

public record FindNextChoreEventArgs(
    int InstanceId,
    string InstanceString,
    int InstanceCell,
    int ChoreId,
    Type ChoreType,
    int ChoreCell,
    bool IsAttemptingOverride,
    bool LastChoreSucceeded,
    MultiplayerIdReference ChoreObjectReference,
    StateMachineReference ChoreStateMachine
);
