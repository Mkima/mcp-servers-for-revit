using Newtonsoft.Json;
using System.Collections.Generic;

namespace RevitMCPCommandSet.Models.Spatial
{
    /// <summary>
    /// Model for room information in spatial topology
    /// </summary>
    public class RoomInfo
    {
        [JsonProperty("room_id")]
        public string RoomId { get; set; }

        [JsonProperty("room_name")]
        public string RoomName { get; set; }

        [JsonProperty("area_sqm")]
        public double AreaSqM { get; set; }
    }

    /// <summary>
    /// Model for level information in spatial topology
    /// </summary>
    public class LevelInfo
    {
        [JsonProperty("level_id")]
        public string LevelId { get; set; }

        [JsonProperty("level_name")]
        public string LevelName { get; set; }

        [JsonProperty("elevation_cm")]
        public double ElevationCm { get; set; }

        [JsonProperty("rooms")]
        public List<RoomInfo> Rooms { get; set; } = new List<RoomInfo>();
    }

    /// <summary>
    /// Result model for spatial topology
    /// </summary>
    public class SpatialTopologyResult
    {
        [JsonProperty("project_name")]
        public string ProjectName { get; set; }

        [JsonProperty("levels")]
        public List<LevelInfo> Levels { get; set; } = new List<LevelInfo>();

        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}