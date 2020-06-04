﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;

namespace TeamCloud.Orchestrator.Orchestrations.Utilities
{
    public sealed class ProjectCommandNotification
    {
        public string PendingInstanceId { get; set; }

        public Guid ActiveCommandId { get; set; }
    }
}
