﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using TeamCloud.Model.Commands;
using TeamCloud.Model.Commands.Core;
using TeamCloud.Model.Data;
using TeamCloud.Orchestration;
using TeamCloud.Orchestrator.Orchestrations.Commands.Activities;
using TeamCloud.Orchestrator.Orchestrations.Utilities.Activities;

namespace TeamCloud.Orchestrator.Orchestrations.Utilities
{
    public static class CommandSendExtensions
    {
        internal static async Task<TCommandResult> SendCommandAsync<TCommand, TCommandResult>(this IDurableOrchestrationContext functionContext, TCommand command, Provider provider)
            where TCommand : IProviderCommand
            where TCommandResult : ICommandResult
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            if (provider is null)
                throw new ArgumentNullException(nameof(provider));

            var providerResult = (TCommandResult)await functionContext
                .CallSubOrchestratorWithRetryAsync<ICommandResult>(nameof(CommandSendOrchestration), (command, provider))
                .ConfigureAwait(true);

            return providerResult;
        }

        internal static async Task<IDictionary<string, TCommandResult>> SendCommandAsync<TCommand, TCommandResult>(this IDurableOrchestrationContext functionContext, TCommand command, Project project = null)
            where TCommand : IProviderCommand
            where TCommandResult : ICommandResult
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            if (command is ICommand<Project> projectCommand)
                project = projectCommand.Payload ?? project;

            if (command is null && command.ProjectId.HasValue)
            {
                project = await functionContext
                    .CallActivityWithRetryAsync<Project>(nameof(ProjectGetActivity), command.ProjectId.Value)
                    .ConfigureAwait(true);
            }

            var providerBatches = await functionContext
                .CallActivityWithRetryAsync<IEnumerable<IEnumerable<Provider>>>(nameof(CommandProviderActivity), project)
                .ConfigureAwait(true);

            var commandResults = Enumerable.Empty<KeyValuePair<string, TCommandResult>>();

            foreach (var providerBatch in providerBatches)
            {
                foreach (var commandResult in commandResults.Where(cr => cr.Value is ICommandResult<ProviderOutput>))
                {
                    var commandResultOutput = commandResult.Value as ICommandResult<ProviderOutput>;

                    command.Results.TryAdd(commandResult.Key, commandResultOutput?.Result?.Properties ?? new Dictionary<string, string>());
                }

                var providerTasks = providerBatch.Select(async provider =>
                {
                    var providerResult = await functionContext
                        .SendCommandAsync<TCommand, TCommandResult>(command, provider)
                        .ConfigureAwait(true);

                    return new KeyValuePair<string, TCommandResult>(provider.Id, providerResult);
                });

                commandResults = commandResults.Concat(await Task
                    .WhenAll(providerTasks)
                    .ConfigureAwait(true));
            }

            return new Dictionary<string, TCommandResult>(commandResults);
        }
    }
}
