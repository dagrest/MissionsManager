using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using GoogleMapsGeocoding;
using MissionsManager.V1.DB;
using MissionsManager.V1.Models;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace MissionsManager.V1
{
    public class MissionsManagerApi : IMissionsManagerApi
    {
        private readonly IGeocoder _geoCoder;

        public MissionsManagerApi(IGeocoder geoCoder)
        {

            _geoCoder = geoCoder;
        }

        // this can throw OutOfMemory, NREs, perhaps anything else? Needs to be taken into account
        public Task<MostIsolationDegreeCountryResponse> FindCountryByIsolation(IDocumentStore store)
        {
            var isolationDegree = 0;
            var countryWithMostIsolationDegree = "Unknown";

            try
            {
                //TODO: consider using Polly for queries and perhaps retry on transient network issues?
                using (var session = store.OpenSession())
                {
                    // Group by Agents to count missions that they took part in
                    var agentsByMissionsQuantity = session.Query<Mission>()
                        .GroupBy(x => x.Agent)
                        .Select(g =>
                            new {Agent = g.Key, MissionsCount = g.Count()})
                        .ToList();

                    // Find Agents that took part only in one mission (isolated Agents)
                    // select only with MissionsCount == 1
                    var isolatedAgentsList = agentsByMissionsQuantity
                        .Where(x => x.MissionsCount == 1)
                        .Select(x => x.Agent)
                        .ToList();

                    // Find missions only with isolated agents
                    var resultCountryWithIsolatedAgents =
                        session.Query<Mission>()
                            .Where(x => x.Agent.In(isolatedAgentsList))
                            .ToList();

                    // Group by Country to count isolated agents in it
                    // Find CountryIsolationDegree - Country with maximal isolated Agents
                    var countriesByIsolationDegree =
                        resultCountryWithIsolatedAgents
                            .GroupBy(x => x.Country)
                            .Select(g =>
                                new {Country = g.Key, CountryIsolationDegree = g.Count()})
                            .OrderByDescending(g => g.CountryIsolationDegree)
                            .ToList();

                    if (countriesByIsolationDegree.Any())
                    {
                        isolationDegree = countriesByIsolationDegree[0].CountryIsolationDegree;
                        countryWithMostIsolationDegree = countriesByIsolationDegree[0].Country;
                    }
                }

                return Task.FromResult(new MostIsolationDegreeCountryResponse
                {
                    MostIsolationDegreeCountry = countryWithMostIsolationDegree,
                    IsolationDegree = isolationDegree,
                    ErrorStatus = new ErrorStatus{ErrorType = ErrorType.OK}
                });
            }
            catch (Exception e) //what happens if there is a timeout from the DB? 
            {
                return Task.FromResult(new MostIsolationDegreeCountryResponse
                {
                    MostIsolationDegreeCountry = countryWithMostIsolationDegree,
                    IsolationDegree = isolationDegree,
                    ErrorStatus = new ErrorStatus { ErrorType = ErrorType.Error, ErrorMessage = e.Message, StackTrace = e.StackTrace }
                });
            }
        }

        public async Task<ErrorStatus> AddMissionAsync(Dictionary<string, string> bodyArguments, IDocumentStore store)
        {
            try{
                // Add mission to DB
                using (var session = store.OpenAsyncSession())
                {
                    var (latitude, longitude) =
                        GetGeolocation(bodyArguments["address"] + " " + bodyArguments["country"]);
                    await session.StoreAsync(new Mission
                    {
                        Agent = bodyArguments["agent"],
                        Country = bodyArguments["country"],
                        Address = bodyArguments["address"],
                        Date = DateTime.Parse(bodyArguments["date"]), //what if "date" is malformed? This will throw
                        Latitude = latitude,
                        Longitude = longitude
                    });
                    await session.SaveChangesAsync();
                    return new ErrorStatus {ErrorType = ErrorType.OK};
                }
            }
            catch (Exception e) //what happens if there is a timeout from the DB? 
            {
                return new ErrorStatus
                    {ErrorType = ErrorType.Error, ErrorMessage = e.Message, StackTrace = e.StackTrace};
            }
        }

        public Task<ClosestMissionResponse> FindClosestMission(Dictionary<string, string> bodyArguments, IDocumentStore store)
        {
            try
            {
                using (var session = store.OpenSession())
                {
                    var (latitude, longitude) =
                        GetGeolocation(bodyArguments["target-location"]);

                    // radius should be configurable
                    var closestMissions = session.Query<Mission, MissionLocation_Index>()
                        .Spatial("Coordinates", factory => factory.WithinRadius(10, latitude, longitude))
                        .Customize(x => x
                                .WaitForNonStaleResults() //potential thread starvation, under load this can take A LONG time
                            //TODO: this needs a "timeout" for waiting, 1 or 2 sec probably
                        ).ToList();

                    return Task.FromResult(new ClosestMissionResponse
                    {
                        MissionsList = closestMissions,
                        ErrorStatus = new ErrorStatus() {ErrorType = ErrorType.OK}
                    });
                }
            }
            catch (Exception e)
            {
                return Task.FromResult(new ClosestMissionResponse
                {
                    MissionsList = null,
                    ErrorStatus = new ErrorStatus 
                        { ErrorType = ErrorType.Error, 
                            ErrorMessage = e.Message, 
                            StackTrace = e.StackTrace }
                });
            }
        }

        public async Task<ErrorStatus> InitMissionAsync(Dictionary<string, string> bodyArguments, IDocumentStore store)
        {
            var sampleDataJson = bodyArguments["sample-data"];
            
            Mission[] sampleData = null;
            try
            {
                sampleData = JsonConvert.DeserializeObject<Mission[]>(sampleDataJson);
            }
            catch (Exception e)
            {
                var errorMessage = $"{e.Message}:\n{sampleDataJson}";
                return new ErrorStatus { ErrorType = ErrorType.Error, ErrorMessage = errorMessage, StackTrace = e.StackTrace};
            }

            if (sampleData == null)
            {
                var errorMessage = $"Failed to convert provided data:\n{sampleDataJson}";
                return new ErrorStatus { ErrorType = ErrorType.Error, ErrorMessage = errorMessage };
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

                //for very large number of missions, this will throw a timeout
                //any insert of many documents needs to take this into account
                //TODO: consider using BulkInsert
                //https://ravendb.net/docs/article-page/4.2/csharp/client-api/bulk-insert/how-to-work-with-bulk-insert-operation
                await session.SaveChangesAsync();
                return new ErrorStatus { ErrorType = ErrorType.OK};
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