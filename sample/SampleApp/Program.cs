using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SampleApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                // bind logger configuration from configuration
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddFile(opts =>
                    {
                        context.Configuration.GetSection("FileLoggingOptions").Bind(opts);
                    });
                })
                // or alternatively, manually set the options
                // .ConfigureLogging(builder => builder.AddFile(opts =>
                // {
                //     opts.FileName = "app-logs-";
                //     opts.FileSizeLimit = 1024 * 1024;

                // }))
                .UseStartup<Startup>()
                .Build();
    }
}
