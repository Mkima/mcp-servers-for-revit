using Newtonsoft.Json;

namespace RevitMCPCommandSet.Models.DataExtraction
{
    public class ElementPairDistance
    {
        [JsonProperty("element1Id")]
        public long Element1Id { get; set; }

        [JsonProperty("element1Name")]
        public string Element1Name { get; set; }

        [JsonProperty("element1Category")]
        public string Element1Category { get; set; }

        [JsonProperty("element2Id")]
        public long Element2Id { get; set; }

        [JsonProperty("element2Name")]
        public string Element2Name { get; set; }

        [JsonProperty("element2Category")]
        public string Element2Category { get; set; }

        [JsonProperty("distanceMm")]
        public double DistanceMm { get; set; }
    }

    public class PairwiseDistanceResult
    {
        [JsonProperty("roomId")]
        public long RoomId { get; set; }

        [JsonProperty("roomName")]
        public string RoomName { get; set; }

        [JsonProperty("totalElements")]
        public int TotalElements { get; set; }

        [JsonProperty("elementCount")]
        public int ElementCount { get; set; }

        [JsonProperty("pairCount")]
        public int PairCount { get; set; }

        [JsonProperty("distances")]
        public List<ElementPairDistance> Distances { get; set; } = new List<ElementPairDistance>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}