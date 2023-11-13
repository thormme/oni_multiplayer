using System;
using System.Collections.Generic;
using HarmonyLib;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.ModRuntime;
using MultiplayerMod.ModRuntime.Context;
using MultiplayerMod.ModRuntime.StaticCompatibility;
using MultiplayerMod.Multiplayer;
using MultiplayerMod.Multiplayer.Commands.Chores;
using MultiplayerMod.Multiplayer.CoreOperations;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.Objects.Reference;
using static KSerialization.DebugLog;

namespace MultiplayerMod.Game.Chores;

[HarmonyPatch(typeof(Worker), nameof(Worker.Work))]
public class WorkerEvents
{
    private static Dictionary<Workable, float> workablesMap = new Dictionary<Workable, float>();
    private static readonly Core.Logging.Logger log = LoggerFactory.GetLogger<WorkerEvents>();
    public static event Action<FinishWorkEventArgs>? FinishWork;

    static void Prepare()
    {
        log.Level = LogLevel.Debug;
    }

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    //[RequireExecutionLevel(ExecutionLevel.Game)]
    public static bool Prefix(Worker __instance, ref float dt, ref Workable __state)
    {
        //if (!Runtime.Instance.Dependencies.Get<ExecutionLevelManager>().LevelIsActive(ExecutionLevel.Game))
        //{
        //    return true;
        //}
        if (Dependencies.Get<MultiplayerGame>().Mode != MultiplayerMode.Host)
        {
            // Negative values are a flag for the override to do work on the client
            if (dt < 0)
            {
                dt = -dt;
                return true;
            }
            dt = .0001f;
            return true;
        }

        __state = __instance.workable;
        if (!workablesMap.ContainsKey(__instance.workable))
        {
            workablesMap.Add(__instance.workable, 0f);
        }
        workablesMap[__instance.workable] += dt;
        return true;
    }

    // ReSharper disable once InconsistentNaming, UnusedMember.Global
    [RequireMultiplayerMode(MultiplayerMode.Host)]
    [RequireExecutionLevel(ExecutionLevel.Game)]
    static void Postfix(Worker __instance, ref Worker.WorkResult __result, ref Workable __state)
    {
        var workable = __state;
        if (__result != Worker.WorkResult.InProgress && workable != null)
        {
            log.Debug($"Worker {__instance.name} -  Finished job {workable.GetType()} - {__result}");
            log.Debug($"Job ID {workable.GetReference()}");

            var args = new FinishWorkEventArgs(
                __instance.GetReference(),
                workable.GetReference(),
                workablesMap[workable]
            );
            workablesMap[workable] = 0f;
            log.Debug(
                $"Triggering {args.WorkerID} {args.WorkableID} {args.WorkTime}"
            );
            FinishWork?.Invoke(
                args
            );
        }
    }

}

public record FinishWorkEventArgs(
    ComponentReference<Worker> WorkerID,
    ComponentReference<Workable> WorkableID,
    float WorkTime
);
