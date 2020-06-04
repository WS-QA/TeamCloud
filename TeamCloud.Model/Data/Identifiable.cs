﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace TeamCloud.Model.Data
{
    public interface IIdentifiable
    {
        Guid Id { get; set; }
    }

    public interface IProperties
    {
        IDictionary<string, string> Properties { get; set; }
    }

    public interface ITags
    {
        IDictionary<string, string> Tags { get; set; }
    }


    [JsonObject(NamingStrategyType = typeof(CamelCaseNamingStrategy))]
    public interface IContainerDocument
    {
        const string PartitionKeyPath = "/partitionKey";

        string Id { get; }

        string PartitionKey { get; }

        IList<string> UniqueKeys { get; }
    }
}
