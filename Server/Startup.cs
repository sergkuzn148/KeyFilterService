using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Gmx.OAuth;

namespace Server
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddScoped(serviceProvider => {
                return new DataService(Configuration.GetConnectionString("AIS"));
            });
            services.AddScoped(serviceProvider => {
                return Configuration.GetSection("DataSources").Get<DataSources>();
            });            
            services.AddMemoryCache();
            services.AddHttpClient();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseDefaultFiles();            
            app.UseHttpsRedirection();
            app.UseStaticFiles(); 
            app.UseRouting();

            app.UseAuthorization();
            app.UseGmxOAuth(Configuration.GetSection("GmxOAuthOptionsSection").Get<GmxOAuthOptions>());

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

        }
    }
}
