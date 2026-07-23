namespace RevitMCPCommandSet.Models.Common
{
    public class ViewSummaryInfo
    {
        public long Id { get; set; }
        public string UniqueId { get; set; }
        public string Name { get; set; }
        public string ViewType { get; set; }
        public bool IsTemplate { get; set; }
        public bool IsCurrent { get; set; }
    }

    public class ViewSwitchResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string PreviousViewName { get; set; }
        public string TargetViewName { get; set; }
        public string TargetViewId { get; set; }
    }
}