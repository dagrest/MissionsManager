using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Carter;
using GoogleMapsGeocoding;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MissionsManager.V1;


namespace MissionsManager
{
    public class Startup
    {
        private const string GoogleApiKey = "AIzaSyD23uaUWMgEeFeXESUtttRB_fbJBliXcQQ";
        public IConfigurationRoot Configuration { get; private set; }
        public ILifetimeScope AutofacContainer { get; private set; }
        public Startup(IHostingEnvironment env)
        {
            // In ASP.NET Core 3.0 env will be an IWebHostEnvironment , not IHostingEnvironment.
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            this.Configuration = builder.Build();
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            // Add services to the collection
            services.AddOptions();
            services.AddCarter();

            // Create a container-builder and register dependencies
            var builder = new ContainerBuilder();

            // Populate the service-descriptors added to `IServiceCollection`
            // BEFORE you add things to Autofac so that the Autofac
            // registrations can override stuff in the `IServiceCollection`
            // as needed
            builder.Populate(services);

            // ************************************************************************************
            // Register your own classes directly with Autofac
            builder.RegisterType<MissionsManagerApi>().As<IMissionsManagerApi>().SingleInstance();
            builder.RegisterType<InitRavenDb>().As<IInitRavenDb>().SingleInstance();
            builder.RegisterType<Validation>().As<IValidation>().SingleInstance();
            builder.RegisterType<InputData>().As<IInputData>().SingleInstance();
            builder.Register(c => new Geocoder(GoogleApiKey)).As<IGeocoder>().SingleInstance();
            // ************************************************************************************

            AutofacContainer = builder.Build();

            // this will be used as the service-provider for the application!
            return new AutofacServiceProvider(AutofacContainer);
        }

        // Configure is where you add middleware.
        // You can use IApplicationBuilder.ApplicationServices
        // here if you need to resolve things from the container.
        public void Configure(
            IApplicationBuilder app,
            ILoggerFactory loggerFactory)
        {
            //loggerFactory.AddConsole(this.Configuration.GetSection("Logging"));
            //loggerFactory.AddDebug();
            //app.UseMvc();
            app.UseRouting();
            app.UseEndpoints(builder => builder.MapCarter());
        }
    }
}
