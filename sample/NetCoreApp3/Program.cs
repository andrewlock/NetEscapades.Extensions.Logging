using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NetCoreApp3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
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
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
