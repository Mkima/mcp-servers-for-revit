using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class ElementInfo
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("uniqueId")]
        public string UniqueId { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("familyName")]
        public string FamilyName { get; set; }

        [JsonProperty("typeName")]
        public string TypeName { get; set; }

        [JsonProperty("level")]
        public string Level { get; set; }

        [JsonProperty("locationX")]
        public double LocationX { get; set; }

        [JsonProperty("locationY")]
        public double LocationY { get; set; }

        [JsonProperty("locationZ")]
        public double LocationZ { get; set; }
    }

    public class GetAllElementsByRoomResult
    {
        [JsonProperty("roomId")]
        public long RoomId { get; set; }

        [JsonProperty("roomName")]
        public string RoomName { get; set; }

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [JsonProperty("elements")]
        public List<ElementInfo> Elements { get; set; } = new List<ElementInfo>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}