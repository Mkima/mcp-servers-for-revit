using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class MepElementInfo
    {
        [JsonProperty("guid")]
        public string Guid { get; set; }

        [JsonProperty("category")]
        public string Category { get; set; }

        [JsonProperty("family_name")]
        public string FamilyName { get; set; }

        [JsonProperty("type_name")]
        public string TypeName { get; set; }

        [JsonProperty("is_waterproof")]
        public bool IsWaterproof { get; set; }

        [JsonProperty("coordinates_cm")]
        public CoordinatesCm CoordinatesCm { get; set; }
    }

    public class CoordinatesCm
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }

        [JsonProperty("z")]
        public double Z { get; set; }
    }

    public class GetMepElementsByRoomResult
    {
        [JsonProperty("room_id")]
        public string RoomId { get; set; }

        [JsonProperty("elements")]
        public List<MepElementInfo> Elements { get; set; } = new List<MepElementInfo>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}