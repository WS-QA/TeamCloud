﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;

namespace TeamCloud.Http.Telemetry
{
    public class HttpTelemetryHandler : DelegatingHandler
    {
        private readonly TelemetryConfiguration telemetryConfiguration;

        public HttpTelemetryHandler(TelemetryConfiguration telemetryConfiguration)
            => this.telemetryConfiguration = telemetryConfiguration ?? throw new System.ArgumentNullException(nameof(telemetryConfiguration));

        public HttpTelemetryHandler(HttpMessageHandler innerHandler, TelemetryConfiguration telemetryConfiguration = null) : base(innerHandler)
            => this.telemetryConfiguration = telemetryConfiguration ?? throw new System.ArgumentNullException(nameof(telemetryConfiguration));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return SendTelemetry(response);
        }

        private HttpResponseMessage SendTelemetry(HttpResponseMessage response)
        {
            const string headerPrefix = "x-ms-ratelimit";

            if (!string.IsNullOrEmpty(telemetryConfiguration?.InstrumentationKey))
            {
                var telemetryClient = new TelemetryClient(telemetryConfiguration);
                var telemetryMetric = telemetryClient.GetMetric("RateLimits", "RateScope");

                TrackRateLimit("remaining-subscription-reads");
                TrackRateLimit("remaining-subscription-writes");
                TrackRateLimit("remaining-tenant-reads");
                TrackRateLimit("remaining-tenant-writes");
                TrackRateLimit("remaining-subscription-resource-requests");
                TrackRateLimit("remaining-subscription-resource-entities-read");
                TrackRateLimit("remaining-tenant-resource-requests");
                TrackRateLimit("remaining-tenant-resource-entities-read");

                void TrackRateLimit(string headerName)
                {
                    var headerValue = response.GetHeaderValue($"{headerPrefix}-{headerName}");
                    if (headerValue is null || !double.TryParse(headerValue, out double rateLimit)) return;

                    telemetryMetric.TrackValue(rateLimit, headerName);
                }
            }

            return response;
        }
    }
}
