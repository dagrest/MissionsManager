using System;
using Raven.Client;

namespace MissionsManager.V1.Models
{
    public class Mission
    {
        public string Agent { get; set; }
        public string Country { get; set; }
        public string Address { get; set; }
        public DateTime Date { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}