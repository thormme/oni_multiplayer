using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Game.Chores;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.Objects.Reference;
using MultiplayerMod.Multiplayer.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MultiplayerMod.Multiplayer.Commands.Chores;

[Serializable]
public class FindNextChore : MultiplayerCommand {

    private static Core.Logging.Logger log = LoggerFactory.GetLogger<FindNextChore>();

    private int instanceId;
    private string instanceString;
    private int instanceCell;
    private int choreId;
    private string choreType;
    private int choreCell;
    private bool isAttemptingOverride;
    private bool lastChoreSucceeded;
    private MultiplayerIdReference choreObjectRef;
    private StateMachineReference choreStateMachineRef;

    [System.NonSerialized]
    private static Dictionary<int, List<HostChores.HostChoreInfo>> inProgressSearches = new Dictionary<int, List<HostChores.HostChoreInfo>>();

    public FindNextChore(FindNextChoreEventArgs args) {
        log.Level = LogLevel.Debug;

        instanceId = args.InstanceId;
        instanceString = args.InstanceString;
        instanceCell = args.InstanceCell;
        choreId = args.ChoreId;
        choreType = args.ChoreType.ToString();
        choreCell = args.ChoreCell;
        isAttemptingOverride = args.IsAttemptingOverride;
        lastChoreSucceeded = args.LastChoreSucceeded;
        choreObjectRef = args.ChoreObjectReference;
        choreStateMachineRef = args.ChoreStateMachine;
    }

    public override void Execute(MultiplayerCommandContext context) {
        log.Level = LogLevel.Debug;
        log.Debug(
            $"Received {instanceId} {instanceString} {instanceCell} {choreId} {choreType} {choreCell}"
        );
        var prefabID = Object.FindObjectsOfType<KPrefabID>().FirstOrDefault(a => a.InstanceID == instanceId);
        if (prefabID == null)
        {
            log.Warning(
                $"Multiplayer: Consumer does not exists at KPrefabId with desired ID {instanceId}. Id collision??"
            );
            return;
        }
        var consumer = prefabID.GetComponent<ChoreConsumer>();
        if (consumer == null)
        {
            log.Warning(
                $"Multiplayer: ChoreConsumer does not exists at KPrefabId with desired ID {instanceId}. Id collision??"
            );
            return;
        }
        if (!HostChores.Index.ContainsKey(instanceId))
        {
            HostChores.Index[instanceId] = new Queue<HostChores.HostChoreInfo>();
        }

        HostChores.HostChoreInfo newChore = new HostChores.HostChoreInfo();

        if (!inProgressSearches.ContainsKey(choreId))
        {
            inProgressSearches[choreId] = new List<HostChores.HostChoreInfo>();
        }
        else
        {
            foreach (var choreInfo in inProgressSearches[choreId])
            {
                choreInfo.Skip();
            }
        }
        inProgressSearches[choreId].Add(newChore);
        HostChores.Index[instanceId].Enqueue(newChore);
        var getContext = WaitForChoreContext(newChore, 0.2f, 2.0f);
        consumer.StartCoroutine(getContext);
    }

    private IEnumerator WaitForChoreContext(HostChores.HostChoreInfo newChore, float waitIncrement, float maxWaitTime)
    {
        var choreContext = FindContext();
        for (float waitTime = 0; waitTime < maxWaitTime; waitTime += waitIncrement)
        {
            if (choreContext != null)
            {
                break;
            }
            log.Debug(
                $"Chore not found {instanceId} {instanceString} {instanceCell} {choreId} {choreType} {choreCell}, waiting {waitIncrement} - {waitTime}"
            );

            yield return new WaitForSeconds(waitIncrement);

            if (newChore.SearchState == HostChores.ContextSearchState.Cancelled)
            {
                break;
            }
            choreContext = FindContext();
        }

        inProgressSearches[choreId].Remove(newChore);

        if (choreContext == null)
        {
            log.Warning(
                $"Chore {choreId} {choreType} context not found after waiting, attempting creation"
            );
            choreContext = CreateMissingContext();
            if (choreContext == null)
            {
                newChore.Fail();
                yield break;
            }
            log.Debug(
                $"Chore {choreId} {choreType} created"
            );
        }

        newChore.SetContext(choreContext.Value, lastChoreSucceeded);
        var newChoreObjectInstance = newChore.Context!.Value.chore.gameObject.GetComponent<MultiplayerInstance>();
        // TODO: Check if overriding
        if (newChoreObjectInstance.Id != null && newChoreObjectInstance.Id != choreObjectRef.Id)
        {
            log.Warning($"Overriding object ID: {newChoreObjectInstance.Id} with {choreObjectRef.Id}");
        }
        newChoreObjectInstance.Id = choreObjectRef.Id;
        newChoreObjectInstance.Register();

        var choreStateMachineInstance = choreStateMachineRef.GetInstance();
        if (choreStateMachineInstance != null)
        {
            StateMachineSyncEvents.SyncedInstances.Add(choreStateMachineInstance);
        }
        else
        {
            log.Warning($"Unable to sync state machine goto events, can't get state machine instance: {newChoreObjectInstance.Id}");
        }

        log.Debug(
            $"Chore found or created {instanceId} {instanceString} {instanceCell} {choreId} {choreType} {choreCell} after waiting"
        );
    }

    private Chore.Precondition.Context? FindContext() {
        var prefabID = Object.FindObjectsOfType<KPrefabID>().FirstOrDefault(a => a.InstanceID == instanceId);
        if (prefabID == null) {
            log.Warning(
                $"Multiplayer: Consumer does not exists at KPrefabId with desired ID {instanceId}. Id collision??"
            );
            return null;
        }
        var consumer = prefabID.GetComponent<ChoreConsumer>();
        if (consumer == null) {
            log.Warning(
                $"Multiplayer: Consumer does not exists at KPrefabId with desired ID {instanceId}. Id collision??"
            );
            return null;
        }
        if (instanceString != consumer.ToString()) {
            log.Warning(
                $"Multiplayer: Consumer type {consumer.GetType()} is not equal to the server {instanceString}."
            );
            return null;
        }
        var localCell = Grid.PosToCell(consumer.transform.position);
        if (instanceCell != localCell) {
            log.Warning(
                $"Multiplayer: Consumer {consumer}-{choreType} found but in different cell. Server {instanceCell} - local {localCell}."
            );
        }

        return FindContext(consumer, choreId, choreCell, choreType, isAttemptingOverride);
    }

    private static Chore.Precondition.Context? FindContext(
        ChoreConsumer instance,
        int choreId,
        int cell,
        string serverChoreType,
        bool isAttemptingOverride
    ) {
        Chore? choreWithIdCollision = null;
        var chore = FindInInstance(
                        instance,
                        choreId,
                        cell,
                        serverChoreType,
                        ref choreWithIdCollision
                    )
                    ?? FindInGlobal(
                        instance,
                        choreId,
                        cell,
                        serverChoreType,
                        ref choreWithIdCollision
                    );

        if (choreWithIdCollision != null) {
            choreWithIdCollision.id = new System.Random().Next();
            choreWithIdCollision.driver = null;
        }

        if (chore == null) {
            log.Warning($"Multiplayer: Chore not found {instance} - id#{choreId}. Type-{serverChoreType}");
            return null;
        }

        log.Level = LogLevel.Debug;

        if (chore.id != choreId) {
            chore.id = choreId;
            log.Trace($"Multiplayer: Corrected {instance}-{serverChoreType} chore id.");
        }

        chore.driver = null;
        log.Debug($"Found {instance}-{serverChoreType} {choreId}-{cell}");
        return new Chore.Precondition.Context(chore, instance.consumerState, isAttemptingOverride);
    }

    private static Chore? FindInInstance(
        ChoreConsumer instance,
        int choreId,
        int cell,
        string serverChoreType,
        ref Chore? choreWithIdCollision
    ) {
        var chores = instance.GetProviders()
            .SelectMany(provider => provider.choreWorldMap.Values.SelectMany(x => x))
            .ToArray();
        return FindFullMatch(
            chores,
            choreId,
            cell,
            serverChoreType,
            ref choreWithIdCollision
        ) ?? FindByTypeAndCell(chores, cell, serverChoreType);
    }

    private static Chore? FindInGlobal(
        ChoreConsumer instance,
        int choreId,
        int cell,
        string serverChoreType,
        ref Chore? choreWithIdCollision
    ) {
        var globalChores =
            Object.FindObjectsOfType<ChoreProvider>()
                .SelectMany(provider => provider.choreWorldMap.Values.SelectMany(x => x)).ToArray();

        var chore = FindFullMatch(
            globalChores,
            choreId,
            cell,
            serverChoreType,
            ref choreWithIdCollision
        ) ?? FindByTypeAndCell(globalChores, cell, serverChoreType);
        log.Debug(
            $"Multiplayer: Chore global search. Result is {chore != null}. {instance.GetType()} {serverChoreType}"
        );
        return chore;
    }

    private static Chore? FindFullMatch(
        Chore[] chores,
        int choreId,
        int choreCell,
        string choreType,
        ref Chore? choreWithIdCollision
    ) {
        var result = chores.FirstOrDefault(
            chore => chore.id == choreId
        );

        if (result == null) return null;

        var clientChoreCell = Grid.PosToCell(result.gameObject.transform.position);
        if (!DependsOnConsumerCell(choreType) && choreCell != clientChoreCell) {
            log.Warning(
                $"Multiplayer: Server chore pos != client chore pos. {choreId}. {choreType}. {choreCell} != {clientChoreCell}"
            );
            choreWithIdCollision = result;
            return null;
        }
        if (result.GetType().ToString() == choreType)
            return result;

        log.Warning(
            $"Chore type is not equal client: {result.GetType()} != server {choreType}"
        );
        choreWithIdCollision = result;
        return null;
    }

    private static Chore? FindByTypeAndCell(Chore[] chores, int choreCell, string choreType) {
        var choreOfType = chores.Where(chore => chore.GetType().ToString() == choreType).ToList();
        var results = choreOfType.Where(
            chore => DependsOnConsumerCell(choreType) ||
                     choreCell == Grid.PosToCell(chore.gameObject.transform.position)
        ).ToArray();
        if (results.Length == 0) {
            var cellPoses = string.Join(
                ", ",
                choreOfType.Select(chore => Grid.PosToCell(chore.gameObject.transform.position))
            );
            log.Debug(
                $"FindByTypeAndCell : Not found {choreType}(cell={choreCell}) in total chores {chores.Length}. Chores of type {choreOfType.Count}."
            );
            log.Debug($"FindByTypeAndCell: Positions of typed chores: {cellPoses}");
            return null;
        }
        if (results.Length > 1) {
            var cellPoses = string.Join(
                ", ",
                results.Select(chore => Grid.PosToCell(chore.gameObject.transform.position))
            );
            log.Warning(
                $"FindByTypeAndCell : Not single {choreType} in total chores {chores.Length}. Matches of type {results.Length}."
            );
            log.Warning($"FindByTypeAndCell: Positions of results chores: {cellPoses}");
            return null;
        }
        return results.Single();
    }

    private Chore.Precondition.Context? CreateMissingContext()
    {
        var prefabID = Object.FindObjectsOfType<KPrefabID>().FirstOrDefault(a => a.InstanceID == instanceId);
        var consumer = prefabID.GetComponent<ChoreConsumer>();

        var breather = prefabID.GetComponent<OxygenBreather>();
        if (breather == null)
        {
            log.Warning(
                $"Multiplayer: OxygenBreather does not exists at KPrefabId with desired ID {instanceId}. Perhaps it is not a minion?"
            );
            return null;
        }
        Chore? newChore = null;
        if (choreType.Contains("MoveToSafetyChore"))
        {
            newChore = new MoveToSafetyChore((IStateMachineTarget) breather);
        }
        if (newChore != null)
        {
            var context = new Chore.Precondition.Context(newChore, consumer.consumerState, false);
            return context;
        }

        return null;
    }

    /// <summary>
    /// This chores depends only on consumer position.
    ///
    /// If consumer position is off due to any reason chore must be taken regardless of its position.
    /// </summary>
    /// <returns></returns>
    private static bool DependsOnConsumerCell(string choreType) {
        string[] independent = { "IdleChore", "MoveToSafetyChore" };
        return independent.Any(choreType.Contains);
    }

}
