using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace MissionsManager.V1
{
    public interface IInputData
    {
        public Dictionary<string, string> GetBodyArguments(HttpRequest req, HttpResponse res);
    }
}