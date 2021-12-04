using System;

namespace MissionsManager.V1.Models
{
    public class Mission
    {
        //agent ID that performed the mission
        public string Agent { get; set; }

        public string Country { get; set; }
        public string Address { get; set; }
        public DateTime Date { get; set; }

        //those are needed for spatial queries
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}