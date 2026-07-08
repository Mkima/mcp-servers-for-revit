using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models.Common;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services
{
    public class SwitchActiveViewEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        public ViewSwitchResult ResultInfo { get; private set; } = new ViewSwitchResult();
        public bool TaskCompleted { get; private set; }
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        private string _targetViewId;
        private string _targetViewUniqueId;
        private string _targetViewName;

        public void SetTargetView(string viewId, string viewUniqueId, string viewName)
        {
            _targetViewId = viewId;
            _targetViewUniqueId = viewUniqueId;
            _targetViewName = viewName;
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
                var uiDoc = app.ActiveUIDocument;
                if (uiDoc == null)
                {
                    throw new InvalidOperationException("No active Revit document is available.");
                }

                var doc = uiDoc.Document;
                var currentView = doc.ActiveView;
                View targetView = null;

                if (!string.IsNullOrWhiteSpace(_targetViewUniqueId))
                {
                    targetView = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .FirstOrDefault(v => string.Equals(v.UniqueId, _targetViewUniqueId, StringComparison.OrdinalIgnoreCase));
                }
                else if (!string.IsNullOrWhiteSpace(_targetViewName))
                {
                    targetView = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .FirstOrDefault(v => string.Equals(v.Name, _targetViewName, StringComparison.OrdinalIgnoreCase));
                }
                else if (!string.IsNullOrWhiteSpace(_targetViewId) && int.TryParse(_targetViewId, out var viewId))
                {
                    targetView = doc.GetElement(new ElementId(viewId)) as View;
                }

                if (targetView == null)
                {
                    throw new InvalidOperationException("No matching view was found.");
                }

                uiDoc.ActiveView = targetView;
                ResultInfo = new ViewSwitchResult
                {
                    Success = true,
                    Message = $"Switched to view '{targetView.Name}'.",
                    PreviousViewName = currentView.Name,
                    TargetViewName = targetView.Name,
                    TargetViewId = targetView.UniqueId,
                };
            }
            catch (Exception ex)
            {
                ResultInfo = new ViewSwitchResult
                {
                    Success = false,
                    Message = ex.Message,
                };
                TaskDialog.Show("error", $"Switching active view failed: {ex.Message}");
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Switch active view";
        }
    }
}
