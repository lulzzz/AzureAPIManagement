﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmallStepsLabs.Azure.ApiManagement
{
    internal static class Constants
    {
        internal static class ApiManagement
        {
            internal static readonly string AccessToken = "SharedAccessSignature";

            internal static class Url
            {
                internal static readonly string ServiceFormat = "https://{0}.management.azure-api.net/";

                internal static readonly string VersionQuery = "api-version";
            }

            internal static class Versions
            {
                internal static readonly string Feb2014 = "2014-02-14-preview";
            }

            internal static class Headers
            {
                internal static readonly string ETagMatch = "If-Match";
            }
        }

        internal static class MimeTypes
        {
            ///<summary>JavaScript Object Notation JSON; Defined in RFC 4627</summary>
            public const string ApplicationJson = "application/json";
        }
    }
}
