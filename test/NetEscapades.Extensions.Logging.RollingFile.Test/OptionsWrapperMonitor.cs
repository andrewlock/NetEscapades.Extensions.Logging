// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in https://github.com/aspnet/Logging for license information.
// https://github.com/aspnet/Logging/blob/2d2f31968229eddb57b6ba3d34696ef366a6c71b/test/Microsoft.Extensions.Logging.AzureAppServices.Test/OptionsWrapperMonitor.cs

using System;
using Microsoft.Extensions.Options;

namespace NetEscapades.Extensions.Logging.RollingFile.Test
{
    internal class OptionsWrapperMonitor<T> : IOptionsMonitor<T>
    {
        public OptionsWrapperMonitor(T currentValue)
        {
            CurrentValue = currentValue;
        }

        public IDisposable OnChange(Action<T, string> listener)
        {
            return null;
        }

        public T Get(string name) => CurrentValue;

        public T CurrentValue { get; }
    }
}