using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using MissionsManager.V1;
using NSubstitute;
using NUnit.Framework;

namespace MissionsManagerTests
{
    [TestFixture]
    public class MissionManagerUnitTests
    {
        private IValidation _validation;
        private HttpResponse _res;

        [OneTimeSetUp]
        public virtual void SetUp()
        {
            _validation = new Validation();
            _res = Substitute.For<HttpResponse>();
        }
        
        //[TestCase(new string[] { "target-location" })]
        [Test]
        public void FirstTest(/*string[] mandatoryFieldsList*/)
        {
            var arguments = new Dictionary<string, string>{ {"target-location", "Rue Al-Aidi Ali Al-Maaroufi Casablanca Morocco" } };
            var mandatoryFieldsList = new string[] { "target-location" };

            _validation.ValidateMandatoryFields(arguments, mandatoryFieldsList, _res);
        }
    }

}
