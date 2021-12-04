using System.Linq;
using MissionsManager.V1.Models;
using Raven.Client.Documents.Indexes;

namespace MissionsManager.V1.DB
{
    public class MissionLocation_Index : AbstractIndexCreationTask<Mission>
    {
        public MissionLocation_Index()
        {
            Map = docs => from e in docs
                select new { Coordinates = CreateSpatialField(e.Latitude, e.Longitude) };
        }
    }
}