using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CatalogAPI.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Swagger;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using CatalogAPI.CustomFormatters;

namespace CatalogAPI
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
            services.AddScoped<CatalogContext>();
            services.AddCors(c =>
            {
                //*This is a default policies which is override pre default poilcies*/
                //c.AddDefaultPolicy(x => x.AllowAnyOrigin()
                //.AllowAnyMethod()
                //.AllowAnyHeader()
                //);

                //*Name policies*/
                c.AddPolicy("AllowPartners", x =>
                {
                    x.WithOrigins("http://microsoft.com", "http://synergetics.com").WithMethods("GET", "POST").AllowAnyHeader();
                    x.WithOrigins("http://google.com").WithMethods("GET").AllowAnyHeader();
                });

                c.AddPolicy("AllowAll", x =>
                {
                    x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
                });
            });
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new Info
                {
                    Title = "Catalog API",
                    Description = "Catalog management API methods for Eshop application",
                    Version = "1.0",
                    Contact = new Contact
                    {
                        Name = "Rohit Agarwal",
                        Email = "agarwal.rohit993@gmail.com",
                        Url = "https://github.com/sonusathyadas"
                    }
                });
            });

            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;

            }).AddJwtBearer(c =>
            {
                c.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = Configuration.GetValue<string>("Jwt:issuer"),
                    ValidAudience = Configuration.GetValue<string>("Jwt:audience"),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration.GetValue<string>("Jwt:secret")))
                };
            });

            services.AddMvc(options =>
                {
                    options.OutputFormatters.Add(new CsvOutputFormatter());
                })
                .AddXmlDataContractSerializerFormatters()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            //app.UseCors();
            //app.UseCors("AllowPartners"); //Custom policy or named policies
            app.UseCors("AllowAll"); //Custom policy or named policies
            app.UseSwagger(); //http://localhost:52011/swagger/v1/swagger.json
            if (env.IsDevelopment())
            {
                app.UseSwaggerUI(config =>
                {
                    config.SwaggerEndpoint("/swagger/v1/swagger.json", "Catelog API");
                    config.RoutePrefix = "";
                });
            }
            app.UseAuthentication();
            app.UseMvc();
        }
    }
}
