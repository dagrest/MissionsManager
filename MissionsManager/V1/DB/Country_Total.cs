using System.Linq;
using MissionsManager.V1.Models;
using Raven.Client.Documents.Indexes;

namespace MissionsManager.V1.DB
{
    public class Country_Total : AbstractIndexCreationTask<IsolatedCountry>
    {
        public class Result
        {
            public string Country { get; set; }
            public int Count { get; set; }

        }

        public Country_Total()
        {
            Map = countries => from country in countries
                select new Result
                {
                    Country = country.Country,
                    Count = 1
                };

            Reduce = results => from result in results
                group result by result.Country into g
                select new
                {
                    Country = g.Key,
                    Count = g.Sum(x => x.Count)
                };
        }
    }
}