using System.Collections.Generic;

namespace MissionsManager.V1.Models
{
    public class ClosestMissionResponse
    {
        public List<Mission> MissionsList { get; set; }
        public ErrorStatus ErrorStatus { get; set; }
    }
}