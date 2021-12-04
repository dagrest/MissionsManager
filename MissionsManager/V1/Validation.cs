using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Carter.Response;
using Microsoft.AspNetCore.Http;

namespace MissionsManager.V1
{
    //TODO: consider using https://fluentvalidation.net/ rules instead
    public class Validation : IValidation
    {
        public void ValidateMandatoryFields(Dictionary<string, string> arguments, string[] mandatoryFieldsList, HttpResponse res)
        {
            foreach (var name in mandatoryFieldsList)
            {
                if (arguments.Keys.Contains(name))
                {
                    ValidateArgumentsValue(arguments[name], name, res);
                    continue;
                }

                var errorMessage = $"Mandatory argument is missing: {name}";
                res.StatusCode = 500;
                res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage);
            }
        }

        private void ValidateArgumentsValue(string argument, string name, HttpResponse res)
        {
            if (string.IsNullOrEmpty(argument))
            {
                var errorMessage = $"Mandatory argument value is missing for: {name}";
                res.StatusCode = 500;
                res.AsJson(errorMessage);
                throw new ArgumentException(errorMessage);
            }
        }
    }
}