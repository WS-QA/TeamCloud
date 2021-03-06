﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http;

namespace TeamCloud.Http
{
    public class TeamCloudHttpMessageHandler : DelegatingHandler
    {
        private readonly bool passthrough;

        public TeamCloudHttpMessageHandler(HttpMessageHandler innerHandler) : this(innerHandler, true)
        { }

        internal TeamCloudHttpMessageHandler(HttpMessageHandler innerHandler, bool passthrough) : base(innerHandler)
        {
            this.passthrough = passthrough;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request is null)
                throw new ArgumentNullException(nameof(request));

            HttpResponseMessage response;

            if (passthrough)
            {
                Debug.WriteLine($">>> {request.Method.ToString().ToUpperInvariant()} {request.RequestUri}");

                var responseTime = Stopwatch.StartNew();

                try
                {
                    response = await base
                        .SendAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                }
                finally
                {
                    responseTime.Stop();
                }

                Debug.WriteLine($"<<< {request.Method.ToString().ToUpperInvariant()} {request.RequestUri} {response.StatusCode} ({responseTime.ElapsedMilliseconds} msec)");

#if DEBUG
                if (!response.IsSuccessStatusCode)
                    await TraceErrorAsync(request, response).ConfigureAwait(false);
#endif
            }
            else
            {
                Debug.WriteLine($"<=> {request.Method.ToString().ToUpperInvariant()} {request.RequestUri}");

                response = await request.RequestUri.ToString()
                    .AllowAnyHttpStatus()
                    .WithHeaders(request.Headers)
                    .SendAsync(request.Method, request.Content, cancellationToken)
                    .ConfigureAwait(false);
            }

            return response;
        }

        private async Task TraceErrorAsync(HttpRequestMessage request, HttpResponseMessage response)
        {
            if (request is null || response is null) return;

            await response.Content
                .LoadIntoBufferAsync()
                .ConfigureAwait(false);

            var trace = new StringBuilder($"!!! {request.Method.ToString().ToUpperInvariant()} {request.RequestUri} {response.StatusCode}");

            trace.AppendLine("REQUEST:  " + await ReadContentAsync(request.Content).ConfigureAwait(false));
            trace.AppendLine("RESPONSE: " + await ReadContentAsync(response.Content).ConfigureAwait(false));

            Debug.WriteLine(trace);

            static async Task<string> ReadContentAsync(HttpContent httpContent)
            {
                if (httpContent is null)
                    return string.Empty;

                var content = await httpContent
                    .ReadAsStringAsync()
                    .ConfigureAwait(false);

                if (content?.IsJson() ?? false)
                    return content;

                return Regex.Replace(content, "\\s+(?=(?:[^'\"]*['\"][^'\"]*['\"])*[^'\"]*$)", string.Empty);
            }

        }
    }
}
