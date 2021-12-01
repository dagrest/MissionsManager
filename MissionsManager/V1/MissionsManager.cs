using Carter;
using Carter.Response;

namespace MissionsManager.V1
{
    public class MissionsManager : CarterModule
    {
        public MissionsManager(IMissionsManagerApi missionsManagerApi,
            IInitRavenDb ravenDb)
        {
            var store = ravenDb.GetDocumentStore();

            Get("/countries-by-isolation", async (req, res) =>
                    await res.AsJson(missionsManagerApi.FindCountryByIsolationAsync(req, store)));

            Post("/mission", async (req, res) =>
                await missionsManagerApi.AddMissionAsync(req, res, store));

            Post("/find-closest", async (req, res) =>
                await missionsManagerApi.FindClosestMissionAsync(req, res, store));

            Post("/init_db", async (req, res) =>
                await res.AsJson(missionsManagerApi.InitMissionAsync(req, res, store)));

        }
    }
}