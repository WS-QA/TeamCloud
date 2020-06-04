﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

namespace TeamCloud.Azure
{
    public interface IAzureSessionOptions
    {
        string TenantId { get; }

        string ClientId { get; }

        string ClientSecret { get; }
    }

    public sealed class AzureSessionOptions : IAzureSessionOptions
    {
        public static IAzureSessionOptions Default => new AzureSessionOptions();

        private AzureSessionOptions()
        { }

        public string TenantId => default;

        public string ClientId => default;

        public string ClientSecret => default;
    }
}
