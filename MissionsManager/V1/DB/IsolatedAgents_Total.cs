using Raven.Client.Documents.Indexes;

namespace MissionsManager.V1.DB
{
    public class IsolatedAgents_Total : AbstractIndexCreationTask
    {
        public class Result
        {
            public string Agent { get; set; }

            public int count { get; set; }
        }

        public override string IndexName => "IsolatedAgents";

        public override IndexDefinition CreateIndexDefinition()
        {
            return new IndexDefinition
            {
                Maps =
                {
                    @"from m in docs.Missions
                    //where company.Address.Country == ""USA""
                    //test string
                    select new {
                         m.Agent,
                         count = 1
                    }"
                },
                Reduce = 
                        @"from result in results
                        group result by new { result.Agent } into g
                        select new {
                           Agent = g.Key.Agent,
                           count = g.Sum(x => x.count)
                        }"
            };
        }
    }
}