using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System;
using System.IO;
using System.Threading;

namespace RevitMCPCommandSet.Services.Document
{
    public class OpenDocumentEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string DocumentPath { get; private set; }
        public OpenDocumentResult ResultInfo { get; private set; } = new OpenDocumentResult();
        public bool TaskCompleted { get; private set; }

        public void SetDocumentPath(string documentPath)
        {
            DocumentPath = documentPath;
        }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(DocumentPath))
                {
                    throw new ArgumentException("Document path is not set");
                }

                if (!File.Exists(DocumentPath))
                {
                    throw new FileNotFoundException($"Revit document not found: {DocumentPath}");
                }

                string extension = Path.GetExtension(DocumentPath);
                if (!extension.Equals(".rvt", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".rte", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".rfa", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"Invalid file type: {extension}. Must be .rvt, .rte, or .rfa");
                }

                var openOptions = new OpenOptions();
                var worksetConfiguration = new WorksetConfiguration(WorksetConfigurationOption.OpenAllWorksets);
                openOptions.SetOpenWorksetsConfiguration(worksetConfiguration);

                var transientErrorHandler = app.TransientMessageHandler;

                Document openedDocument;
                if (app.ActiveUIDocument != null && app.ActiveUIDocument.Document != null)
                {
                    var currentDocTitle = app.ActiveUIDocument.Document.Title;
                    if (string.Equals(Path.GetFileName(DocumentPath), currentDocTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        ResultInfo = new OpenDocumentResult
                        {
                            Success = true,
                            Message = $"Document is already open: {DocumentPath}",
                            DocumentTitle = currentDocTitle,
                            IsNewlyOpened = false,
                        };
                        return;
                    }
                }

                UIDocument uiDoc = null;

                if (extension.Equals(".rfa", StringComparison.OrdinalIgnoreCase))
                {
                    var familyDoc = app.OpenDocumentFile(DocumentPath);
                    uiDoc = new UIDocument(familyDoc);
                }
                else
                {
                    uiDoc = app.OpenAndActivateDocument(DocumentPath, openOptions, false);
                }

                if (uiDoc == null || uiDoc.Document == null)
                {
                    throw new InvalidOperationException("Failed to open document");
                }

                var docTitle = uiDoc.Document.Title;
                ResultInfo = new OpenDocumentResult
                {
                    Success = true,
                    Message = $"Successfully opened and activated: {docTitle}",
                    DocumentPath = DocumentPath,
                    DocumentTitle = docTitle,
                    IsNewlyOpened = true,
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new OpenDocumentResult
                {
                    Success = false,
                    Message = ex.Message,
                    DocumentPath = DocumentPath,
                };
                TaskDialog.Show("Open Document Error", $"Failed to open document: {ex.Message}");
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Open Revit Document";
        }
    }

    public class OpenDocumentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string DocumentPath { get; set; }
        public string DocumentTitle { get; set; }
        public bool IsNewlyOpened { get; set; }
    }
}