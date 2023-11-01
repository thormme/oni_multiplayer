using System;
using System.Collections.Generic;
using System.Linq;
using MultiplayerMod.Core.Logging;
using MultiplayerMod.Game.Chores;
using MultiplayerMod.Multiplayer.Objects;
using MultiplayerMod.Multiplayer.Objects.Reference;
using MultiplayerMod.Multiplayer.World;
using Object = UnityEngine.Object;

namespace MultiplayerMod.Multiplayer.Commands.Chores;

[Serializable]
public class FinishWorkEvent : MultiplayerCommand {

    private static Core.Logging.Logger log = LoggerFactory.GetLogger<FinishWorkEvent>();

    private ComponentReference<Worker> workerID;
    private ComponentReference<Workable> workableID;

    public FinishWorkEvent(FinishWorkEventArgs args) {
        log.Level = LogLevel.Debug;

        workerID = args.WorkerID;
        workableID = args.WorkableID;
    }

    //static Workable? GetMatchingWorkable(Worker worker, Type workableType)
    //{
    //    Workable[] workables = worker.gameObject.GetComponents<Workable>();
    //    for (int index = 0; index < workables.Length; index++)
    //    {
    //        if (workables[index].GetType() == workableType)
    //        {
    //            return workables[index];
    //        }
    //    }
    //    return null;
    //}

    public override void Execute(MultiplayerCommandContext context) {
        if (!workerID.IsValid() || !workableID.IsValid())
        {
            log.Warning(
                $"Received invalid command Worker: {workerID.IsValid()} - Workable: {workableID.IsValid()}"
            );
            return;
        }
        log.Warning(
            $"Received {workerID.GetComponent().name} {workableID.GetComponent().GetType()}"
        );
        var workable = workableID.GetComponent();
        var worker = workerID.GetComponent();

        // Immadiately Finish the work
        // TODO: Use FinishImmediately
        if (worker.workable == workable)
        {
            float dt = 0.5f;
            for (float time = 0; time < 10; time += dt)
            {
                if (worker.Work(dt) != Worker.WorkResult.InProgress)
                {
                    break;
                }
            }
        }
    }

}
