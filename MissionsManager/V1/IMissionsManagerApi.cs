using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using MissionsManager.V1.Models;
using Raven.Client.Documents;

namespace MissionsManager.V1
{
    public interface IMissionsManagerApi

    {
        public Task<FindCountryByIsolationResponse> FindCountryByIsolationAsync(HttpRequest req, IDocumentStore store);
        public Task AddMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store);
        public Task FindClosestMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store);
        public Task InitMissionAsync(HttpRequest req, HttpResponse res, IDocumentStore store);

    }
}