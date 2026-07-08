using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands.Access
{
    public class ListViewsCommand : ExternalEventCommandBase
    {
        private ListViewsEventHandler _handler => (ListViewsEventHandler)Handler;

        public override string CommandName => "list_views";

        public ListViewsCommand(UIApplication uiApp)
            : base(new ListViewsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                if (RaiseAndWaitForCompletion(60000))
                {
                    return _handler.ResultInfo;
                }

                throw new TimeoutException("Listing views timed out.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Listing views failed: {ex.Message}");
            }
        }
    }
}
