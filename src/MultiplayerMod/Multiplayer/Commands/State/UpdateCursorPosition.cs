﻿using System;
using MultiplayerMod.Multiplayer.State;

namespace MultiplayerMod.Multiplayer.Commands.State;

[Serializable]
public class UpdateCursorPosition : MultiplayerCommand {

    private UpdateCursorPositionEvent @event;

    public UpdateCursorPosition(UpdateCursorPositionEvent @event) {
        this.@event = @event;
    }

    public override void Execute(MultiplayerCommandContext context) {
        context.Multiplayer.State.Players.TryGetValue(@event.Player, out var state);
        if (state == null)
            return;

        state.Cursor = new PlayerCursor(@event.Position);
        context.EventDispatcher.Dispatch(@event);
    }

}