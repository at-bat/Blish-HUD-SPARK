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

            string errorStatus = null;

            try
            {
                await action();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "SPARK UI action failed.");
                errorStatus = failureStatus;
            }
            finally
            {
                SparkUiThread.Queue(() =>
                {
                    if (!string.IsNullOrWhiteSpace(errorStatus))
                        setStatus?.Invoke(errorStatus);

                    if (button.Parent != null)
                        button.Enabled = wasEnabled;
                });
            }
        }

        private sealed class LogContext
        {
        }
    }
}
