using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.MEP
{
    public class FixtureInfo
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

    public class GetFixturesByRoomResult
    {
        [JsonProperty("roomId")]
        public long RoomId { get; set; }

        [JsonProperty("roomName")]
        public string RoomName { get; set; }

        [JsonProperty("totalFixtures")]
        public int TotalFixtures { get; set; }

        [JsonProperty("fixtures")]
        public List<FixtureInfo> Fixtures { get; set; } = new List<FixtureInfo>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}