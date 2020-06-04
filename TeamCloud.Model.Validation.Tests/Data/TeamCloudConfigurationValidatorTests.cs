﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using TeamCloud.Model.Data;
using Xunit;

namespace TeamCloud.Model.Validation.Tests.Data
{
    public class TeamCloudConfigurationValidatorTests
    {
        [Fact(Skip = "Not implementedd")]
        public void Validate_Success()
        {
            var configuration = new TeamCloudConfiguration()
            {
                ProjectTypes = new List<ProjectType>()
                {
                    new ProjectType()
                    {
                         Id = "default",
                         Region = "WestUS",
                         Subscriptions = new List<Guid>()
                         {
                             Guid.NewGuid()
                         },
                         ResourceGroupNamePrefix = "tc_",
                         Providers = new List<ProviderReference>()
                         {
                             new ProviderReference()
                             {
                                 Id = "providerA"
                             },
                             new ProviderReference()
                             {
                                 Id = "providerB"
                             }
                         }
                    }
                }
            };

            var result = configuration.Validate();

            Assert.True(result.IsValid);
        }
    }
}
