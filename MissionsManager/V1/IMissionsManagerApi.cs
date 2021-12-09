using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MissionsManager.V1.Models;
using Raven.Client.Documents;

namespace MissionsManager.V1
{
    public interface IMissionsManagerApi
    {
        public Task<MostIsolationDegreeCountryResponse> FindCountryByIsolation(
            IDocumentStore store);

        public Task<ErrorStatus> AddMissionAsync(Dictionary<string, string> bodyArguments, IDocumentStore store);

        public Task<ClosestMissionResponse> FindClosestMission(Dictionary<string, string> bodyArguments, IDocumentStore store);

        public Task<ErrorStatus> InitMissionAsync(Dictionary<string, string> bodyArguments, IDocumentStore store);
    }
}