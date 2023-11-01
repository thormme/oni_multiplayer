using HarmonyLib;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.ModRuntime.StaticCompatibility;
using MultiplayerMod.Multiplayer.Commands.Chores;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.World;
using System;
using System.Collections.Generic;

namespace MultiplayerMod.Multiplayer.Patches;

[HarmonyPatch(typeof(ChoreConsumer), nameof(ChoreConsumer.FindNextChore))]
// ReSharper disable once UnusedType.Global
public class ChoreConsumerPatch {
    private static Core.Logging.Logger log = LoggerFactory.GetLogger<FindNextChore>();
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    public static bool Prefix(ChoreConsumer __instance, ref Chore.Precondition.Context out_context, ref bool __result) {
        log.Level = LogLevel.Debug;
        if (Dependencies.Get<MultiplayerGame>().Mode != MultiplayerMode.Client)
            return true;

        var instanceId = __instance.gameObject.GetComponent<KPrefabID>().InstanceID;
        var queue = HostChores.Index.GetValueSafe(instanceId);
        var choreInfo = (queue?.Count ?? 0) > 0 ? queue?.Peek() : null;
        bool skippedChore = false;
        while(choreInfo?.SearchState == HostChores.ContextSearchState.Failure || choreInfo?.SearchState == HostChores.ContextSearchState.Cancelled)
        {
            skippedChore = true;
            queue?.Dequeue();
            choreInfo = (queue?.Count ?? 0) > 0 ? queue?.Peek() : null;
        }
        var choreContext = choreInfo?.Context;
        if (choreInfo != null && choreContext != null)
        {
            //log.Debug($"Checking next Host Chore {choreContext.GetType()}");
            // TODO: Don;t wait to complete tasks whose contexts weren't found and so weren't queued
            if (skippedChore || !choreInfo.LastChoreSucceeded || choreContext.Value.isAttemptingOverride || __instance.choreDriver == null || __instance.choreDriver.GetCurrentChore() == null || __instance.choreDriver.GetCurrentChore().isComplete)
            {
                queue?.Dequeue();
            }
            else
            {
                if (__instance.choreDriver != null && __instance.choreDriver.GetCurrentChore() != null/* || __instance.choreDriver.GetCurrentChore().isComplete*/)
                {
                    log.Warning($"Multiplayer: Chore not dequeued {__instance.choreDriver == null || __instance.choreDriver.GetCurrentChore() == null || __instance.choreDriver.GetCurrentChore().isComplete}");
                }

                return false;
            }
        }
        __result = choreContext != null;
        if (choreContext != null) out_context = choreContext.Value;

        return false;
    }
}

