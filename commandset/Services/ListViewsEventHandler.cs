using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class ListViewsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public List<ViewSummaryInfo> ResultInfo { get; private set; } = new List<ViewSummaryInfo>();
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var uiDoc = app.ActiveUIDocument;
                if (uiDoc == null)
                {
                    throw new InvalidOperationException("No active Revit document is available.");
                }

                var doc = uiDoc.Document;
                var activeView = doc.ActiveView;

                ResultInfo = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v != null && !v.IsTemplate)
                    .OrderBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(v => new ViewSummaryInfo
                    {
                        Id = v.Id.IntegerValue,
                        UniqueId = v.UniqueId,
                        Name = v.Name,
                        ViewType = v.ViewType.ToString(),
                        IsTemplate = v.IsTemplate,
                        IsCurrent = v.Id.IntegerValue == activeView.Id.IntegerValue,
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                ResultInfo = new List<ViewSummaryInfo>();
                TaskDialog.Show("error", $"Listing views failed: {ex.Message}");
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "List views";
        }
    }
}
