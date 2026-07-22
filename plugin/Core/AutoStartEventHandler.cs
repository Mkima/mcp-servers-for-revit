using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace revit_mcp_plugin.Core
{
    public class AutoStartEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public static AutoStartEventHandler Instance { get; private set; }
        public bool HasRun { get; private set; }

        private readonly System.Threading.ManualResetEvent _resetEvent = new System.Threading.ManualResetEvent(false);

        public AutoStartEventHandler()
        {
            Instance = this;
            HasRun = false;
        }

        public void Execute(UIApplication app)
        {
            if (HasRun) return;
            HasRun = true;

            var service = SocketService.Instance;
            if (!service.IsInitialized)
            {
                service.InitializeWithUI(app);
            }
            service.Start();
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public string GetName()
        {
            return "AutoStartEventHandler";
        }
    }
}