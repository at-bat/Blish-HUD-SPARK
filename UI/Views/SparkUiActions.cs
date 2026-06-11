using Blish_HUD;
using Blish_HUD.Controls;
using System;
using System.Threading.Tasks;

namespace rp.spark.UI.Views
{
    internal static class SparkUiActions
    {
        private static readonly Logger Logger = Logger.GetLogger<LogContext>();

        public static void BindClick(
            StandardButton button,
            Func<Task> action,
            Action<string> setStatus = null,
            string failureStatus = "Action failed.")
        {
            if (button == null)
                return;

            button.Click += async (s, e) => await RunAsync(button, action, setStatus, failureStatus);
        }

        private static async Task RunAsync(
            StandardButton button,
            Func<Task> action,
            Action<string> setStatus,
            string failureStatus)
        {
            if (button == null || action == null || !button.Enabled)
                return;

            var wasEnabled = button.Enabled;
            button.Enabled = false;

            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
                // Closing a view while work is in flight is expected.
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "SPARK UI action failed.");
                setStatus?.Invoke(failureStatus);
            }
            finally
            {
                button.Enabled = wasEnabled;
            }
        }

        private sealed class LogContext
        {
        }
    }
}
