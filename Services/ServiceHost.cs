using Blish_HUD;
using System;
using System.Collections.Generic;

namespace rp.spark.Services
{
    internal sealed class ServiceHost : IDisposable
    {
        private static readonly Logger Logger = Logger.GetLogger<ServiceHost>();

        private readonly List<IDisposable> _services = new List<IDisposable>();
        private readonly List<Action> _startActions = new List<Action>();
        private bool _isStarted;
        private bool _isDisposed;

        public void Add<TService>(TService service, Action<TService> startAction = null)
            where TService : class, IDisposable
        {
            if (service == null)
                return;

            _services.Add(service);

            if (startAction != null)
                _startActions.Add(() => startAction(service));
        }

        public void Start()
        {
            if (_isStarted || _isDisposed)
                return;

            _isStarted = true;

            foreach (var startAction in _startActions)
            {
                try
                {
                    startAction();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to start a SPARK background service.");
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            for (var index = _services.Count - 1; index >= 0; index--)
            {
                try
                {
                    _services[index]?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Failed to dispose a SPARK background service.");
                }
            }

            _startActions.Clear();
            _services.Clear();
        }
    }
}
