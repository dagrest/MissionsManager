using MissionsManager.V1.DB;
using Raven.Client.Documents;
using Raven.Embedded;

namespace MissionsManager.V1
{
    public interface IInitRavenDb
    {
        public IDocumentStore GetDocumentStore();
    }

    public class InitRavenDb : IInitRavenDb
    {
        private readonly IDocumentStore _store;
        public InitRavenDb()
        {
            // Initialize embedded RavenDB instance
            // note: for production, this would not be embedded instance,
            // this would be a DB cluster with load balancing and failover
            EmbeddedServer.Instance.StartServer();
#if DEBUG
            EmbeddedServer.Instance.OpenStudioInBrowser();            
#endif
            //TODO: move database name to a constant so it won't be hardcoded
            //TODO: consider moving the database name to a configuration file
            _store = EmbeddedServer.Instance.GetDocumentStore("MI6Missions");

            // ensure indexes exist in the DB
            // ( if an index exists, a call to Execute() will be a NOP )
            new MissionLocation_Index().Execute(_store, _store.Conventions, "MI6Missions");
        }

        public IDocumentStore GetDocumentStore() => _store;
    }
}