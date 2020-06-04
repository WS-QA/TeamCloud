﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.WebJobs;

namespace TeamCloud.Orchestration
{
    public static class FunctionEnvironment
    {
        private static readonly ConcurrentDictionary<string, MethodInfo> FunctionMethodCache = new ConcurrentDictionary<string, MethodInfo>();

        public static MethodInfo GetFunctionMethod(string functionName) => FunctionMethodCache.GetOrAdd(functionName, (key) =>
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(asm => !asm.IsDynamic)
                .SelectMany(asm => asm.GetExportedTypes().Where(type => type.IsClass))
                .SelectMany(type => type.GetMethods())
                .FirstOrDefault(method => method.GetCustomAttribute<FunctionNameAttribute>()?.Name.Equals(functionName, StringComparison.Ordinal) ?? false);

        }) ?? throw new ArgumentOutOfRangeException(nameof(functionName), $"Could not find function by name '{functionName}'");

        public static bool FunctionExists(string functionName)
        {
            try
            {
                return (GetFunctionMethod(functionName) != null);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }
    }
}
