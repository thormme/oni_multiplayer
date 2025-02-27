﻿using System.Collections.Generic;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.Core.Events;
using MultiplayerMod.Core.Unity;
using MultiplayerMod.Multiplayer.Players;
using MultiplayerMod.Multiplayer.Players.Events;

namespace MultiplayerMod.Multiplayer.Components;

public class CursorManager : MultiplayerMonoBehaviour {

    [InjectDependency] private readonly EventDispatcher eventDispatcher = null!;

    private readonly Dictionary<MultiplayerPlayer, CursorComponent> cursors = new();
    private EventSubscriptions subscriptions = null!;

    private void OnEnable() {
        subscriptions = new EventSubscriptions()
            .Add(eventDispatcher.Subscribe<PlayerCursorPositionUpdatedEvent>(OnCursorUpdated))
            .Add(eventDispatcher.Subscribe<PlayerLeftEvent>(OnPlayerLeft));
    }

    private void OnPlayerLeft(PlayerLeftEvent @event) {
        Destroy(cursors[@event.Player]);
        cursors.Remove(@event.Player);
    }

    private void OnDisable() => subscriptions.Cancel();

    private void OnCursorUpdated(PlayerCursorPositionUpdatedEvent updatedEvent) {
        if (!cursors.TryGetValue(updatedEvent.Player, out var cursorComponent)) {
            cursorComponent = gameObject.AddComponent<CursorComponent>();
            cursorComponent.PlayerName = updatedEvent.Player.Profile.PlayerName;
            cursorComponent.CursorWithinWorld.SetPosition(updatedEvent.MouseMovedEventArgs.Position);
            cursorComponent.CursorWithinScreen.SetPosition(updatedEvent.MouseMovedEventArgs.PositionWithinScreen);
            cursorComponent.ScreenName = updatedEvent.MouseMovedEventArgs.ScreenName;
            cursorComponent.ScreenType = updatedEvent.MouseMovedEventArgs.ScreenType;
            cursors[updatedEvent.Player] = cursorComponent;
            return;
        }
        cursorComponent.ScreenName = updatedEvent.MouseMovedEventArgs.ScreenName;
        cursorComponent.ScreenType = updatedEvent.MouseMovedEventArgs.ScreenType;
        cursorComponent.CursorWithinWorld.Trace(updatedEvent.MouseMovedEventArgs.Position);
        cursorComponent.CursorWithinScreen.Trace(updatedEvent.MouseMovedEventArgs.PositionWithinScreen);
    }

}
