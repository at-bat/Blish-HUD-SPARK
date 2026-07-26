using Microsoft.Xna.Framework;
using rp.spark.Services;
using System;

namespace rp.spark.UI.Views
{
    internal sealed class SparkStatusDisplay
    {
        private static readonly Color ReadyColor = new Color(140, 220, 140);
        private static readonly Color StartingColor = new Color(255, 194, 55);

        private SparkStatusDisplay(
            string readinessText,
            string readinessTooltip,
            Color readinessColor,
            string serverText,
            string serverTooltip,
            Color serverColor)
        {
            ReadinessText = readinessText;
            ReadinessTooltip = readinessTooltip;
            ReadinessColor = readinessColor;
            ServerText = serverText;
            ServerTooltip = serverTooltip;
            ServerColor = serverColor;
        }

        public string ReadinessText { get; }
        public string ReadinessTooltip { get; }
        public Color ReadinessColor { get; }
        public string ServerText { get; }
        public string ServerTooltip { get; }
        public Color ServerColor { get; }

        public string CombinedText => $"{ReadinessText} • {ServerText}";
        public string CombinedTooltip => JoinLines(ReadinessTooltip, ServerTooltip);

        public Color CombinedColor => IsReady && IsServerConnected ? ReadyColor : StartingColor;

        private bool IsReady => string.Equals(ReadinessText, "SPARK ready", StringComparison.Ordinal);
        private bool IsServerConnected => string.Equals(ServerText, "Server: Connected", StringComparison.Ordinal);

        public static SparkStatusDisplay Create(Func<string> getImportantNotice, Func<ServerSyncStatus> getServerStatus)
        {
            return Create(SafeInvoke(getImportantNotice), SafeInvoke(getServerStatus));
        }

        public static SparkStatusDisplay Create(string notice, ServerSyncStatus serverStatus)
        {
            notice = notice?.Trim() ?? string.Empty;

            var ready = string.IsNullOrWhiteSpace(notice);
            var connected = serverStatus?.State == ServerSyncState.Connected;

            return new SparkStatusDisplay(
                ready ? "SPARK ready" : "Starting up...",
                ready ? "SPARK is ready." : notice,
                ready ? ReadyColor : StartingColor,
                $"Server: {GetServerText(serverStatus)}",
                GetServerTooltip(serverStatus),
                connected
                    ? ReadyColor
                    : StartingColor);
        }

        private static string GetServerText(ServerSyncStatus status)
        {
            if (status == null)
                return "Disconnected";

            if (!string.IsNullOrWhiteSpace(status.DisplayName))
                return status.DisplayName.Trim();

            return status.State == ServerSyncState.Info
                ? "Getting ready"
                : "Attention needed";
        }

        private static string GetServerTooltip(ServerSyncStatus status)
        {
            if (status == null)
                return "SPARK cannot connect.";

            if (string.IsNullOrWhiteSpace(status.DisplayName))
                return status.Message?.Trim() ?? string.Empty;

            return string.IsNullOrWhiteSpace(status.Message)
                ? status.DisplayName.Trim()
                : $"{status.DisplayName.Trim()}: {status.Message.Trim()}";
        }

        private static string JoinLines(string first, string second)
        {
            first = first?.Trim() ?? string.Empty;
            second = second?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(first))
                return second;

            if (string.IsNullOrWhiteSpace(second))
                return first;

            return $"{first}{Environment.NewLine}{second}";
        }

        private static string SafeInvoke(Func<string> getValue)
        {
            try
            {
                return getValue?.Invoke() ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static ServerSyncStatus SafeInvoke(
            Func<ServerSyncStatus> getValue)
        {
            try
            {
                return getValue?.Invoke();
            }
            catch
            {
                return null;
            }
        }
    }
}