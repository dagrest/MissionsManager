using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using GoogleMapsGeocoding;
using GoogleMapsGeocoding.Common;
using Microsoft.AspNetCore.Http;
using MissionsManager.V1;
using MissionsManager.V1.Models;
using NSubstitute;
using NUnit.Framework;
using NUnit.Framework.Internal;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.TestDriver;

namespace MissionsManagerTests
{
    [TestFixture]
    public class MissionManagerUnitTests : RavenTestDriver
    {
        private IValidation _validation;
        private HttpResponse _res;

        private const string Agent = "007";
        private const string Country = "Brazil";
        private const string Address = "Avenida Vieira Souto 168 Ipanema, Rio de Janeiro";
        private const string Date = "Dec 17, 1995, 9:45:17 PM";
        private const double Lat = 11.123;
        private const double Lng = -7.345;
        private Dictionary<string, string> _bodyArguments;
        private DateTime _expectedDate;

        protected override void PreInitialize(IDocumentStore documentStore)
        {
            documentStore.Conventions.MaxNumberOfRequestsPerSession = 50;
        }

        [OneTimeSetUp]
        public virtual void SetUp()
        {
            _validation = new Validation();
            _res = Substitute.For<HttpResponse>();

            ConfigureServer(new TestServerOptions
            {
                DataDirectory = "C:\\Temp\\RavenDBTestDir"
            });

            _expectedDate = Convert.ToDateTime(Date);

            _bodyArguments = new Dictionary<string, string>
            {
                ["agent"] = Agent,
                ["country"] = Country,
                ["address"] = Address,
                ["date"] = Date
            };

        }

        [Test]
        public void Test_ValidateMandatoryFields_HappyFlow()
        {
            var mandatoryFieldsList = new string[] { "agent", "country", "address", "date" };

            _validation.ValidateMandatoryFields(_bodyArguments, mandatoryFieldsList, _res);
            Assert.DoesNotThrow(() => _validation.ValidateMandatoryFields(_bodyArguments, mandatoryFieldsList, _res));
        }

        //https://ravendb.net/docs/article-page/4.2/Csharp/start/test-driver#raventestdriver
        [Test]
        public async Task Test_AddMissionAsync_HappyFlow()
        {
            var geoCoder = Substitute.For<IGeocoder>();
            var location = new Location { Lat = Lat, Lng = Lng };
            var geometry = new Geometry { Location = location};
            var geocodeResponse = new GeocodeResponse()
            {
                Status = "OK",
                Results = new Result[] { new Result { Geometry = geometry } }
            };
            geoCoder.Geocode(Arg.Any<string>()).Returns(geocodeResponse);

            using (var store = GetDocumentStore())
            {
                await store.ExecuteIndexAsync(new MissionDocumentByName());

                var missionsManagerApi = new MissionsManagerApi(geoCoder);
                var status = await missionsManagerApi.AddMissionAsync(_bodyArguments, store);
                Assert.AreEqual(ErrorType.OK, status.ErrorType);
                Thread.Sleep(1000);

                using (var session = store.OpenSession())
                {
                    var query = session.Query<Mission, MissionDocumentByName>()
                        .Where(x => x.Agent == "007").ToList();
                    Assert.AreEqual(1, query.Count);
                    Assert.AreEqual(Agent, query[0].Agent);
                    Assert.AreEqual(Country, query[0].Country);
                    Assert.AreEqual(Address, query[0].Address);
                    Assert.AreEqual(_expectedDate, query[0].Date);
                    Assert.AreEqual(Lat, query[0].Latitude);
                    Assert.AreEqual(Lng, query[0].Longitude);
                }
            }
        }

        public class MissionDocumentByName : AbstractIndexCreationTask<Mission>
        {
            public MissionDocumentByName()
            {
                Map = missions => from mission in missions select new { mission.Agent };
                Indexes.Add(x => x.Agent, FieldIndexing.Search);
            }
        }
    }

}
