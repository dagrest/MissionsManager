using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Carter.Response;
using GoogleMapsGeocoding;
using GoogleMapsGeocoding.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using MissionsManager.V1.DB;
using MissionsManager.V1.Models;
using Newtonsoft.Json;
using Raven.Client;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;

namespace MissionsManager.V1
{
    public class MissionsManagerApi : IMissionsManagerApi
    {
        public async Task<FindCountryByIsolationResponse> FindCountryByIsolationAsync(HttpRequest req, IDocumentStore store)
        {
            var isolationDegree = 0;
            var countryWithMostIsolationDegree = "Unknown";
            using (var session = store.OpenSession())
            {
                IList<IsolatedAgents_Total.Result> resultIsolatedAgents =
                    session
                        .Query<IsolatedAgents_Total.Result, IsolatedAgents_Total>()
                        .Where(x => x.count == 1)
                        .ToList();

                List<string> list = new List<string>();
                var isolatedAgentsList = new StringBuilder();
                foreach (var agent in resultIsolatedAgents)
                {
                    list.Add(agent.Agent);
                    isolatedAgentsList.Append("'").Append(agent.Agent).Append("',");
                }
                isolatedAgentsList.Remove(isolatedAgentsList.Length - 1, 1);

                var resultCountryWithIsolatedAgents = session.Advanced.RawQuery<Mission>
                (
                    @$"from Missions as m
                        where m.Agent in ({isolatedAgentsList})
                        select {{ Country: m.Country, Agent: m.Agent}}"
                ).ToList();

                var operation = store
                    .Operations
                    .Send(new DeleteByQueryOperation(new IndexQuery
                    {
                        Query = "from IsolatedCountries"
                    }));

                operation.WaitForCompletion(TimeSpan.FromSeconds(15));

                foreach (var doc in resultCountryWithIsolatedAgents)
                {
                    session.Store(new IsolatedCountry { Country = doc.Country, Agent = doc.Agent });
                }
                session.SaveChanges();
                operation.WaitForCompletion(TimeSpan.FromSeconds(15));

                IList<IsolatedCountry> countriesByIsolationDegree = session
                    .Query<Country_Total.Result, Country_Total>()
                    //.Where(x => x.Country)
                    .OfType<IsolatedCountry>()
                    .OrderByDescending(x => (int)x.Count)
                    .ToList();

                if (countriesByIsolationDegree.Any())
                {
                    isolationDegree = countriesByIsolationDegree[0].Count;
                    countryWithMostIsolationDegree = countriesByIsolationDegree[0].Country;
                }
            }

            return new FindCountryByIsolationResponse { MostIsolationDegreeCountry = countryWithMostIsolationDegree, IsolationDegree = isolationDegree };
        }

        public async Task AddMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store)
        {
            var bodyArguments = 
                AddMissionInputValidation(req, res, 
                    new string[] { "agent", "country", "address", "date" });

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
            var bodyArguments = 
                AddMissionInputValidation(req, res, new string[] { "target-location" });

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
            }

            return Task.CompletedTask;
        }

        public async Task InitMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store)
        {
            var bodyArguments = AddMissionInputValidation(req, res, new string[] { "sample-data" });

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

        private Dictionary<string, string> AddMissionInputValidation(HttpRequest req, HttpResponse res, string[] mandatoryFieldsList)
        {
            var bodyArguments = GetBodyArguments(req, res);
            ValidateMandatoryFields(bodyArguments, mandatoryFieldsList, res);

            return bodyArguments;
        }

        private Dictionary<string, string> GetBodyArguments(HttpRequest req, HttpResponse res)
        {
            req.BodyReader.TryRead(out var result);
            var buffer = result.Buffer;

            if (buffer.Length <= 0)
            {
                var errorMessage = "Mandatory arguments are missing";
                res.StatusCode = 500;
                res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage);
            }

            var bodyArgument = new Dictionary<string, string>();
            var bodyContent = Encoding.UTF8.GetString(buffer.ToArray());
            var bodyArgs = bodyContent.Split('&');
            foreach (var argPair in bodyArgs)
            {
                var argValueArray = argPair.Split('=');
                ValidateArgumentsValue(argValueArray[1], argValueArray[0], res);
                bodyArgument[argValueArray[0]] = WebUtility.UrlDecode(argValueArray[1]);
            }
            return bodyArgument;
        }

        private void ValidateMandatoryFields(Dictionary<string, string> arguments, string[] mandatoryFieldsList, HttpResponse res)
        {
            foreach (var name in mandatoryFieldsList)
            {
                if (arguments.Keys.Contains(name)) continue;
                var errorMessage = $"Mandatory argument is missing: {name}";
                res.StatusCode = 500;
                res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage);
            }
        }

        private void ValidateArgumentsValue(string argument, string name, HttpResponse res)
        {
            if (string.IsNullOrEmpty(argument))
            {
                var errorMessage = $"Mandatory argument value is missing for: {name}";
                res.StatusCode = 500;
                res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage);
            }
        }

        private (double Latitude, double Longitude) GetGeolocation(string address)
        {
            //// TODO: move ApiKey to Const
            //var geoCoder = new Geocoder("GOOGLE_API_KEY");

            //var response = geoCoder.Geocode(address); // address + country

            //var latitude = response.Results[0].Geometry.Location.Lat;
            //var longitude = response.Results[0].Geometry.Location.Lng;

            //TODO: fix to real address
            // swietego Tomasza 35, Krakow Poland
            double latitude = 50.062;
            double longitude = 19.943;

            return (latitude, longitude);
        }

        #endregion // privateFunctions
    }
}