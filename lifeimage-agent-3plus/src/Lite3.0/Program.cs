using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System;
using Microsoft.Extensions.Configuration;
using System.IO;
using Serilog;
using System.Diagnostics;

namespace Lite3
{
    public class Program
    {        
        public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(GetRelativePath("appsettings.json"), optional: false, reloadOnChange: true)
            .AddJsonFile(GetRelativePath($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json"), optional: true)
            .AddEnvironmentVariables()
            .Build();

        public static int Main(string[] args)
        {            
            var logger = Configuration.GetSerilogLogger();
            try
            {
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
                string version = fvi.FileVersion;

                logger.Information($"Starting LITE v{version} application");
                logger.Information($"Current date: {DateTime.Now}");

                var currentDir = Directory.GetCurrentDirectory();
                logger.Information($"Application directory: {currentDir}");

                var dir = AppContext.BaseDirectory;
                Directory.SetCurrentDirectory(dir);

                CreateHostBuilder(args).Build().Run();

                return 0;
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Host terminated unexpectedly");
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static string GetRelativePath(string path)
        {
            return Path.Combine(AppContext.BaseDirectory, path);
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseWindowsService()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();

                    // see https://docs.microsoft.com/ru-ru/aspnet/core/fundamentals/servers/kestrel?view=aspnetcore-3.1
                    webBuilder.ConfigureKestrel(serverOpts =>
                    {
                        Log.Logger.Debug(serverOpts.ToString());
                        serverOpts.ConfigureEndpointDefaults(end =>
                        {
                            Log.Logger.Debug(end.ToString());
                        });
                    });

                    webBuilder.UseKestrel(opts =>
                    {
                        // Bind directly to a socket handle or Unix socket
                        // opts.ListenHandle(123554);
                        // opts.ListenUnixSocket("/tmp/kestrel-test.sock");
                        opts.ListenLocalhost(5004, opts => opts.UseHttps());
                        opts.ListenLocalhost(5005, opts => opts.UseHttps());
                    });
                })            
                .UseSerilog()
            ;
    }
}
