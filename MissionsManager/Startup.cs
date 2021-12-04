using System;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Carter;
using GoogleMapsGeocoding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MissionsManager.V1;


namespace MissionsManager
{
    public class Startup
    {
        private const string GoogleApiKey = "GOOGLE_API_KEY";

        public IConfigurationRoot Configuration { get; private set; }
        public ILifetimeScope AutofacContainer { get; private set; }
        public Startup(IHostEnvironment env)
        {
            //just in case we want to add config later
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            this.Configuration = builder.Build();            
        }

        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.AddCarter();

            var builder = new ContainerBuilder();
            builder.Populate(services);

            builder.RegisterType<MissionsManagerApi>().As<IMissionsManagerApi>().SingleInstance();
            builder.RegisterType<InitRavenDb>().As<IInitRavenDb>().SingleInstance();
            builder.RegisterType<Validation>().As<IValidation>().SingleInstance();
            builder.RegisterType<InputData>().As<IInputData>().SingleInstance();
            builder.Register(c => 
                new Geocoder(Environment.GetEnvironmentVariable(GoogleApiKey)))
                .As<IGeocoder>().SingleInstance();

            AutofacContainer = builder.Build();
            return new AutofacServiceProvider(AutofacContainer);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseEndpoints(builder => builder.MapCarter());
        }
    }
}
