using System;

namespace rp.spark.Services
{
    public enum ServerSyncState
    {
        Info,
        Disconnected,
        Connected,
        ApiUnavailable,
        BlockedByWindows,
        ServerError
    }

    public class ServerSyncStatus
    {
        public ServerSyncStatus(
            ServerSyncState state,
            string message,
            DateTime lastAttempt = default,
            DateTime lastSuccess = default)
        {
            State = state;
            Message = message ?? string.Empty;
            LastAttempt = lastAttempt;
            LastSuccess = lastSuccess;
        }

        public ServerSyncState State { get; }

        public string Message { get; }

        public DateTime LastAttempt { get; }

        public DateTime LastSuccess { get; }

        public string DisplayName
        {
            get
            {
                switch (State)
                {
                    case ServerSyncState.Info:
                        return string.Empty;
                    case ServerSyncState.Connected:
                        return "Connected";
                    case ServerSyncState.ApiUnavailable:
                        return "SPARK webserver unavailable";
                    case ServerSyncState.BlockedByWindows:
                        return "Blocked by Windows";
                    case ServerSyncState.ServerError:
                        return "SPARK webserver error";
                    default:
                        return "Disconnected";
                }
            }
        }

        public static ServerSyncStatus Disconnected(string message)
        {
            return new ServerSyncStatus(ServerSyncState.Disconnected, message);
        }
    }
}
