﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmallStepsLabs.Azure.ApiManagement.Model
{
    public class Group : EntityBase
    {
        protected override string UriIdFormat {  get { return "groups/"; } }

        /// <summary>
        /// Returns true if the group is one of the three system groups (Administrators, Developers, or Guests); otherwise false. 
        /// This property is read-only.
        /// </summary>
        [JsonProperty("builtIn")]
        public bool BuiltIn { get; set; }

        /// <summary>
        /// The type of group, which is one of the following values: system, custom, or external. This property is read-only.
        /// </summary>
        [JsonProperty("type")]
        public bool Type { get; set; }

        /// <summary>
        /// For external groups, this property contains the id of the group from the external identity provider,
        /// e.g. Azure Active Directory; otherwise the value is null. 
        /// This property is read-only.
        /// </summary>
        [JsonProperty("externalId")]
        public bool ExternalId { get; set; }

    }
}
