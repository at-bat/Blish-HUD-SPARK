using System;
using System.Threading;
using System.Threading.Tasks;

namespace rp.spark
{
    internal static class TaskCleanup
    {
        public static void DisposeWhenComplete(Task worker, IDisposable disposable)
        {
            if (disposable == null)
                return;

            if (worker == null || worker.IsCompleted)
            {
                disposable.Dispose();
                return;
            }

            worker.ContinueWith(
                task => disposable.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }
}
