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
public class GotoStateEvent : MultiplayerCommand
{

    private static Core.Logging.Logger log = LoggerFactory.GetLogger<GotoStateEvent>();

    private StateMachineReference stateMachineRef;
    private string? previousStateName;
    private string? nextStateName;

    public GotoStateEvent(GotoStateEventArgs args)
    {
        log.Level = LogLevel.Debug;

        stateMachineRef = args.StateMachineRef;
        previousStateName = args.PreviousStateName;
        nextStateName = args.NextStateName;
    }

    public override void Execute(MultiplayerCommandContext context)
    {
        var stateMachineInstance = stateMachineRef.GetInstance();
        if (stateMachineInstance == null)
        {
            log.Warning(
                $"Received invalid state machine reference: {previousStateName} -> {nextStateName}"
            );
            return;
        }
        log.Debug(
            $"Received {stateMachineInstance} : {previousStateName} -> {nextStateName}"
        );

        if (stateMachineInstance.GetCurrentState()?.name == previousStateName)
        {
            //stateMachineInstance.GoTo(nextStateName);
        }
    }

}
