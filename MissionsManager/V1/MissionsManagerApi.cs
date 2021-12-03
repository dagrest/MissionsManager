using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Carter.Response;
using GoogleMapsGeocoding;
using Microsoft.AspNetCore.Http;
using MissionsManager.V1.DB;
using MissionsManager.V1.Models;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;

namespace MissionsManager.V1
{
    public class MissionsManagerApi : IMissionsManagerApi
    {
        private readonly IInputData _inputData;
        private readonly IValidation _validation;
        private readonly IGeocoder _geoCoder;

        public MissionsManagerApi(IInputData inputData, IValidation validation, IGeocoder geoCoder)
        {
            _inputData = inputData;
            _validation = validation;
            _geoCoder = geoCoder;
        }

        public Task<FindCountryByIsolationResponse> FindCountryByIsolation(HttpRequest req, HttpResponse res, IDocumentStore store)
        {
            var isolationDegree = 0;
            var countryWithMostIsolationDegree = "Unknown";
            using (var session = store.OpenSession())
            {
                // Find Agents that took part only in one mission (isolated Agents)
                // group by Agent and select only with count 1
                IList<IsolatedAgents_Total.Result> resultIsolatedAgents =
                    session
                        .Query<IsolatedAgents_Total.Result, IsolatedAgents_Total>()
                        .Where(x => x.count == 1)
                        .ToList();

                var isolatedAgentsList = new StringBuilder();
                foreach (var agent in resultIsolatedAgents)
                {
                    isolatedAgentsList.Append("'").Append(agent.Agent).Append("',");
                }
                isolatedAgentsList.Remove(isolatedAgentsList.Length - 1, 1);

                // Get countries of missions that isolated Agents took part in
                var resultCountryWithIsolatedAgents = session.Advanced.RawQuery<Mission>
                (
                    @$"from Missions as m
                        where m.Agent in ({isolatedAgentsList})
                        select {{ Country: m.Country, Agent: m.Agent}}"
                ).ToList();

                // Clear table with countries with missions that isolated agents took part
                var operation = store
                    .Operations
                    .Send(new DeleteByQueryOperation(new IndexQuery
                    {
                        Query = "from IsolatedCountries"
                    }));

                operation.WaitForCompletion(TimeSpan.FromSeconds(15));

                // Store data about countries with missions that isolated agents took part
                foreach (var doc in resultCountryWithIsolatedAgents)
                {
                    session.Store(new IsolatedCountry { Country = doc.Country, Agent = doc.Agent });
                }
                session.SaveChanges();
                operation.WaitForCompletion(TimeSpan.FromSeconds(15));

                // Find country with most isolation degree
                IList<IsolatedCountry> countriesByIsolationDegree = session
                    .Query<Country_Total.Result, Country_Total>()
                    .OfType<IsolatedCountry>()
                    .OrderByDescending(x => (int)x.Count)
                    .ToList();

                if (countriesByIsolationDegree.Any())
                {
                    isolationDegree = countriesByIsolationDegree[0].Count;
                    countryWithMostIsolationDegree = countriesByIsolationDegree[0].Country;
                }
            }

            res.StatusCode = 200;
            return Task.FromResult(new FindCountryByIsolationResponse { MostIsolationDegreeCountry = countryWithMostIsolationDegree, IsolationDegree = isolationDegree });
        }

        public async Task AddMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store)
        {
            var bodyArguments = _inputData.GetBodyArguments(req, res);
            _validation.ValidateMandatoryFields(bodyArguments, 
                    new string[] { "agent", "country", "address", "date" }, res);

            // Add mission to DB
            using (var session = store.OpenAsyncSession())
            {
                var (latitude, longitude) =
                    GetGeolocation(bodyArguments["address"] + " " + bodyArguments["country"]);
                await session.StoreAsync(new Mission 
                    { Agent = bodyArguments["agent"], 
                        Country = bodyArguments["country"], 
                        Address = bodyArguments["address"], 
                        Date = DateTime.Parse(bodyArguments["date"]),
                        Latitude = latitude,
                        Longitude = longitude
                });
                await session.SaveChangesAsync();
                res.StatusCode = 200;
            }
        }

        public Task FindClosestMission(HttpRequest req, HttpResponse res, IDocumentStore store)
        {
            var bodyArguments = _inputData.GetBodyArguments(req, res);
            _validation.ValidateMandatoryFields(bodyArguments,
                new string[] { "target-location" }, res);

            using (var session = store.OpenSession())
            {
                var (latitude, longitude) =
                    GetGeolocation(bodyArguments["target-location"]);

                var closestMissions = session.Query<Mission, Spatial_Index>()
                    .Spatial("Coordinates", factory => factory.WithinRadius(10, latitude, longitude))
                    .Customize(x => x
                        .WaitForNonStaleResults()
                    ).ToList();

                res.StatusCode = 200;
                return Task.FromResult(closestMissions);
            }
        }

        public async Task InitMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store)
        {
            var bodyArguments = _inputData.GetBodyArguments(req, res);
            _validation.ValidateMandatoryFields(bodyArguments,
                new string[] { "sample-data" }, res);

            var sampleDataJson = bodyArguments["sample-data"];
            Mission[] sampleData = null;
            try
            {
                sampleData = JsonConvert.DeserializeObject<Mission[]>(sampleDataJson);
            }
            catch (Exception e)
            {
                var errorMessage = $"{e.Message}:\n{sampleDataJson}";
                res.StatusCode = 500;
                await res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage, e);
            }

            if (sampleData == null)
            {
                var errorMessage = $"Failed to convert provided data:\n{sampleDataJson}";
                res.StatusCode = 500;
                await res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage);
            }

            // Add mission to DB
            using (var session = store.OpenAsyncSession())
            {
                foreach (var mission in sampleData)
                {
                    var (latitude, longitude) =
                        GetGeolocation(mission.Address + " " + mission.Country);
                    await session.StoreAsync(
                        new Mission
                        {
                            Agent = mission.Agent, 
                            Country = mission.Country, 
                            Address = mission.Address, 
                            Date = mission.Date, 
                            Latitude = latitude,
                            Longitude = longitude
                        });
                }
                await session.SaveChangesAsync();
                res.StatusCode = 200;
            }
        }

        #region privateFunctions
        private (double Latitude, double Longitude) GetGeolocation(string address)
        {
            var response = _geoCoder.Geocode(address); // address + country
            if (response.Status != "OK")
            {
                throw new AccessViolationException(response.Status);
            }

            return (response.Results[0].Geometry.Location.Lat, 
                response.Results[0].Geometry.Location.Lng);
        }
        #endregion // privateFunctions
    }
}