using Blish_HUD;
using System;

namespace rp.spark
{
    internal static class SparkUiThread
    {
        private static readonly Logger Logger = Logger.GetLogger<LogContext>();

        public static void Queue(Action action)
        {
            if (action == null)
                return;

            GameService.Overlay.QueueMainThreadUpdate(_ =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "SPARK main-thread UI update failed.");
                }
            });
        }

        private sealed class LogContext
        {
        }
    }
}