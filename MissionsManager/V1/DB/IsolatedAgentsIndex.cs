using System.Linq;
using MissionsManager.V1.Models;
using Raven.Client.Documents.Indexes;

namespace MissionsManager.V1.DB
{
    
    public class IsolatedAgentsIndex : AbstractIndexCreationTask<Mission, IsolatedAgentsIndex.ReduceResult>
    {
        public class ReduceResult
        {
            public string Agent { get; set; }

            public int count { get; set; }
        }

        public IsolatedAgentsIndex()
        {
            Map = Missions => from mission in Missions 
                let agentName = LoadDocument<Mission>(mission.Agent).Agent
                select new
                {
                    Agent = agentName,
                    count = 1
                };

            Reduce = results => from result in results
                group result by new { result.Agent } into g
                select new
                {
                    Agent = g.Key.Agent,
                    count = g.Sum(x => x.count)
                };
        }
    }
    
}