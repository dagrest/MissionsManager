using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MissionsManager.V1.Models;
using Raven.Client.Documents;

namespace MissionsManager.V1
{
    public interface IMissionsManagerApi

    {
        public Task<FindCountryByIsolationResponse> FindCountryByIsolation(HttpRequest req, HttpResponse res, IDocumentStore store);
        public Task AddMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store);
        public Task FindClosestMission(HttpRequest req, HttpResponse res, IDocumentStore store);
        public Task InitMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store);

    }
}