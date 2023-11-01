using System;
using System.Collections.Generic;
using HarmonyLib;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.ModRuntime.Context;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Multiplayer.Commands.Chores;
using MultiplayerMod.Multiplayer.CoreOperations;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.Objects.Reference;

namespace MultiplayerMod.Game.Chores;

[HarmonyPatch(typeof(Worker), nameof(Worker.Work))]
public class WorkerEvents
{
    private static Dictionary<Worker, Workable> workablesMap = new Dictionary<Worker, Workable>();
    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<WorkerEvents>();
    public static event Action<FinishWorkEventArgs>? FinishWork;

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    [RequireMultiplayerMode(MultiplayerMode.Host)]
    [RequireExecutionLevel(ExecutionLevel.Game)]
    static void Prefix(Worker __instance)
    {
        workablesMap[__instance] = __instance.workable;
    }

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    [RequireMultiplayerMode(MultiplayerMode.Host)]
    [RequireExecutionLevel(ExecutionLevel.Game)]
    static void Postfix(Worker __instance, ref Worker.WorkResult __result)
    {
        if (__result != Worker.WorkResult.InProgress && workablesMap[__instance] != null)
        {
            log.Warning($"Worker {__instance.name} -  Finished job {workablesMap[__instance].GetType()} - {__result}");
            log.Warning($"Job ID {workablesMap[__instance].GetReference()}");

            var args = new FinishWorkEventArgs(
                __instance.GetReference(),
                workablesMap[__instance].GetReference()
            );
            log.Warning(
                $"Triggering {args.WorkerID} {args.WorkableID}"
            );
            FinishWork?.Invoke(
                args
            );
        }

        workablesMap.Remove(__instance);
    }

}

public record FinishWorkEventArgs(
    ComponentReference<Worker> WorkerID,
    ComponentReference<Workable> WorkableID
);
