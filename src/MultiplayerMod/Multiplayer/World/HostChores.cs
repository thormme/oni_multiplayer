using MultiplayerMod.Multiplayer.Commands.Chores;
using System.Collections.Generic;

namespace MultiplayerMod.Multiplayer.World;

public static class HostChores {

    public enum ContextSearchState
    {
        Searching,
        Success,
        Failure,
        Cancelled
    }

    public class HostChoreInfo
    {
        public Chore.Precondition.Context? Context;
        public bool LastChoreSucceeded;
        public ContextSearchState SearchState;

        public HostChoreInfo()
        {
            SearchState = ContextSearchState.Searching;
            Context = null;
            LastChoreSucceeded = false;
        }

        public void SetContext(Chore.Precondition.Context context, bool lastChoreSucceeded)
        {
            SearchState = ContextSearchState.Success;
            Context = context;
            LastChoreSucceeded = lastChoreSucceeded;
        }

        public void Fail()
        {
            SearchState = ContextSearchState.Failure;
        }

        public void Skip()
        {
            SearchState = ContextSearchState.Cancelled;
        }
    }

    //public class HostGotoStateInfo
    //{
    //    public GotoStateEvent GotoEvent;
    //    public ContextSearchState SearchState;

    //    public HostGotoStateInfo(GotoStateEvent gotoEvent)
    //    {
    //        GotoEvent = gotoEvent;
    //        SearchState = ContextSearchState.Searching;
    //    }

    //    public void Fail()
    //    {
    //        SearchState = ContextSearchState.Failure;
    //    }

    //    public void Skip()
    //    {
    //        SearchState = ContextSearchState.Cancelled;
    //    }
    //}

    public static Dictionary<int, Queue<HostChoreInfo>> Index { get; } = new();
    //public static Queue<HostGotoStateInfo> GotoState { get; } = new();

}
