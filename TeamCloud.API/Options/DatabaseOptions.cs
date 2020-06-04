﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using TeamCloud.Configuration;
using TeamCloud.Configuration.Options;
using TeamCloud.Data.CosmosDb;

namespace TeamCloud.API.Options
{
    [Options]
    public sealed class DatabaseOptions : ICosmosDbOptions
    {
        private readonly CosmosDbOptions cosmosDbOptions;

        public DatabaseOptions(CosmosDbOptions cosmosDbOptions)
        {
            this.cosmosDbOptions = cosmosDbOptions;
        }

        string ICosmosDbOptions.DatabaseName => cosmosDbOptions.DatabaseName;

        string ICosmosDbOptions.ConnectionString => cosmosDbOptions.ConnectionString;
    }
}
