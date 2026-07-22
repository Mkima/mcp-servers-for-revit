using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class MCPServiceConnection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                SocketService service = SocketService.Instance;

                if (service.IsRunning)
                {
                    service.Stop();
                }
                else
                {
                    if (!service.IsInitialized)
                    {
                        service.InitializeWithUI(commandData.Application);
                    }
                    service.Start();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
