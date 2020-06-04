﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using TeamCloud.Model.Data;

namespace TeamCloud.Model.Commands
{
    public class ProviderProjectUserUpdateCommand : ProviderCommand<User, ProviderProjectUserUpdateCommandResult>
    {
        public ProviderProjectUserUpdateCommand(User user, User payload, Guid projectId, Guid? commandId = null) : base(user, payload, commandId)
            => this.ProjectId = projectId;
    }
}
