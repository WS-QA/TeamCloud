﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Net;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TeamCloud.Http;
using TeamCloud.Model.Commands;
using TeamCloud.Model.Commands.Core;
using TeamCloud.Model.Data;
using TeamCloud.Orchestration;
using TeamCloud.Serialization;

namespace TeamCloud.Orchestrator.Orchestrations.Utilities.Activities
{
    public static class CommandSendActivity
    {
        [FunctionName(nameof(CommandSendActivity))]
        [RetryOptions(3)]
        public static async Task<ICommandResult> RunActivity(
            [ActivityTrigger] IDurableActivityContext functionContext,
            ILogger log)
        {
            if (functionContext is null)
                throw new ArgumentNullException(nameof(functionContext));

            var (provider, message) = functionContext.GetInput<(Provider, ProviderCommandMessage)>();

            var providerUrl = new Url(provider.Url?.Trim());

            if (!providerUrl.Path.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                providerUrl = providerUrl.AppendPathSegment("api/command");

            log.LogInformation($"Sending command {message.CommandId} ({message.CommandType}) to {providerUrl}. Payload:{JsonConvert.SerializeObject(message)}");

            try
            {
                var response = await providerUrl
                    .WithHeader("x-functions-key", provider.AuthCode)
                    .WithHeader("x-functions-callback", message.CallbackUrl)
                    .PostJsonAsync(message)
                    .ConfigureAwait(false);

                return await response.Content
                    .ReadAsJsonAsync<ICommandResult>()
                    .ConfigureAwait(false);
            }
            catch (FlurlHttpException postException) when (postException.Call.HttpStatus == HttpStatusCode.Conflict)
            {
                // there is no need to retry sending the command - the provider reported a conflict, so the command was already sent
                throw new RetryCanceledException($"Provider '{provider.Id}' failed with status code {postException.Call.HttpStatus}", postException);
            }
            catch (FlurlHttpException postException) when (postException.Call.HttpStatus == HttpStatusCode.BadRequest)
            {
                // there is no need to retry sending the command - the provider reported a bad message payload
                throw new RetryCanceledException($"Provider '{provider.Id}' failed with status code {postException.Call.HttpStatus}", postException);
            }
            catch (FlurlHttpException postException) when (postException.Call.HttpStatus == HttpStatusCode.Unauthorized)
            {
                // there is no need to retry sending the command - seems like our auth code became invalid
                throw new RetryCanceledException($"Provider '{provider.Id}' failed with status code {postException.Call.HttpStatus}", postException);
            }
            catch (Exception exc) when (!exc.IsSerializable(out var serializableExc))
            {
                throw serializableExc;
            }
        }
    }
}
