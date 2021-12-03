using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Carter.Response;
using Microsoft.AspNetCore.Http;

namespace MissionsManager.V1
{
    public class InputData : IInputData
    {
        public Dictionary<string, string> GetBodyArguments(HttpRequest req, HttpResponse res)
        {
            req.BodyReader.TryRead(out var result);
            var buffer = result.Buffer;

            if (buffer.Length <= 0)
            {
                var errorMessage = "Mandatory arguments are missing";
                res.StatusCode = 500;
                res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage);
            }

            var bodyArguments = new Dictionary<string, string>();
            var bodyContent = Encoding.UTF8.GetString(buffer.ToArray());
            var bodyArgs = bodyContent.Split('&');
            foreach (var argPair in bodyArgs)
            {
                var argValueArray = argPair.Split('=');
                bodyArguments[argValueArray[0]] = WebUtility.UrlDecode(argValueArray[1]);
            }
            return bodyArguments;
        }
    }
}