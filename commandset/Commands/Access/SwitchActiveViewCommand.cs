using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access
{
    public class SwitchActiveViewCommand : ExternalEventCommandBase
    {
        private SwitchActiveViewEventHandler _handler => (SwitchActiveViewEventHandler)Handler;

        public override string CommandName => "switch_active_view";

        public SwitchActiveViewCommand(UIApplication uiApp)
            : base(new SwitchActiveViewEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                string viewId = parameters?["viewId"]?.ToString();
                string viewUniqueId = parameters?["viewUniqueId"]?.ToString();
                string viewName = parameters?["viewName"]?.ToString();

                _handler.SetTargetView(viewId, viewUniqueId, viewName);

                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }

                throw new TimeoutException("Switching active view timed out.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Switching active view failed: {ex.Message}");
            }
        }
    }
}
