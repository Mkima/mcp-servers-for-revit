using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services.Document;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Document
{
    public class OpenDocumentCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private OpenDocumentEventHandler _handler => (OpenDocumentEventHandler)Handler;

        public override string CommandName => "open_revit_document";

        public OpenDocumentCommand(UIApplication uiApp)
            : base(new OpenDocumentEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    if (parameters?["documentPath"] == null || string.IsNullOrWhiteSpace(parameters["documentPath"].ToString()))
                    {
                        throw new ArgumentException("Document path is required");
                    }

                    string documentPath = parameters["documentPath"].ToString();

                    _handler.SetDocumentPath(documentPath);

                    if (RaiseAndWaitForCompletion(120000))
                    {
                        return _handler.ResultInfo;
                    }
                    else
                    {
                        throw new TimeoutException("Open document operation timed out");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Open document failed: {ex.Message}");
                }
            }
        }
    }
}