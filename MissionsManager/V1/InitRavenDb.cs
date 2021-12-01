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
            // Initialize embedded RavenDB
            EmbeddedServer.Instance.StartServer();
#if DEBUG
            EmbeddedServer.Instance.OpenStudioInBrowser();            
#endif
            _store = EmbeddedServer.Instance.GetDocumentStore("MI6Missions");
            new Country_Total().Execute(_store, _store.Conventions, "MI6Missions");
        }

        public IDocumentStore GetDocumentStore()
        {
            return _store;
        }
    }
}