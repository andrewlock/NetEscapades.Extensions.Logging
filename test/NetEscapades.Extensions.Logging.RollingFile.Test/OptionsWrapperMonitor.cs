using System;
using Microsoft.Extensions.Options;

namespace NetEscapades.Extensions.Logging.RollingFile.Test
{
    internal class OptionsWrapper<T> : IOptions<T> where T: class, new()
    {
        public OptionsWrapper(T currentValue)
        {
            Value = currentValue;
        }

        public T Value { get; set; }
    }

}
