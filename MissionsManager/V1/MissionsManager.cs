using System.Threading.Tasks;
using Carter;
using Carter.Response;

namespace MissionsManager.V1
{

    //TODO: add API versioning
    //TODO: consider how to use "REST" scheme of HTTP verbs
    public class MissionsManager : CarterModule
    {
        private readonly IInputData _inputData;
        private readonly IValidation _validation;

        public MissionsManager(IMissionsManagerApi missionsManagerApi, IInputData inputData, IValidation validation,
            IInitRavenDb ravenDb)
        {
            _inputData = inputData;
            _validation = validation;
            var store = ravenDb.GetDocumentStore();

            Get("/v1/countries-by-isolation", async (req, res) =>
                    await res.AsJson(missionsManagerApi.FindCountryByIsolation(store)));

            Post("/v1/mission", async (req, res) =>
            {
                var bodyArguments = _inputData.GetBodyArguments(req, res);
                _validation.ValidateMandatoryFields(bodyArguments,
                    new string[] { "agent", "country", "address", "date" }, res);
                await res.AsJson(missionsManagerApi.AddMissionAsync(bodyArguments, store));
            });

            Post("/v1/find-closest", (req, res) =>
            {
                var bodyArguments = _inputData.GetBodyArguments(req, res);
                _validation.ValidateMandatoryFields(bodyArguments,
                    new string[] { "target-location" }, res);
                
                return res.AsJson(missionsManagerApi.FindClosestMission(bodyArguments, store));
            });

            Post("/v1/init_db", async (req, res) =>
            {
                var bodyArguments = _inputData.GetBodyArguments(req, res);
                _validation.ValidateMandatoryFields(bodyArguments,
                    new string[] { "sample-data" }, res);

                await res.AsJson(missionsManagerApi.InitMissionAsync(bodyArguments, store));
            });

        }
    }
}