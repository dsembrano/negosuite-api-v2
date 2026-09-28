using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using negosuite_api.Models;
using negosuite_api.Services;
using System.Text;
using negosuite_api.Controllers;
using System;
using System.Net;

namespace negosuite_api
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
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                foreach (var proxy in Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
                    options.KnownProxies.Add(IPAddress.Parse(proxy));
                foreach (var network in Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            });

            services.AddCors(o => o.AddPolicy("MyPolicy", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader();
            }));


            // Add HttpClientFactory with custom configuration
            services.AddHttpClient("DeepSeekClient", client =>
            {
                client.BaseAddress = new Uri("https://api.deepseek.com/");
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                // Timeout can be configured here if needed
                client.Timeout = TimeSpan.FromSeconds(180);
            });


            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "negosuite_api", Version = "v1" });
            });

            var connectionString = this.Configuration.GetValue<string>("ConnectionString:negosuite");
            services.AddDbContext<negosuiteContext>(options => options.UseMySQL(connectionString));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                           .AddJwtBearer(options =>
                           {
                               options.TokenValidationParameters = new TokenValidationParameters
                               {
                                   ValidateIssuer = true,
                                   ValidateAudience = true,
                                   ValidateLifetime = true,
                                   ValidateIssuerSigningKey = true,
                                   ValidIssuer = Configuration["Jwt:Issuer"],
                                   ValidAudience = Configuration["Jwt:Audience"],
                                   IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["Jwt:Key"]))
                               };
                           });

            services.AddAuthorization();
            services.AddHealthChecks();
            services.AddSingleton<IEmailService, EmailService>();
            services.AddScoped<ConfigUuidFilter>();

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "negosuite_api v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseRequestLocalization();

            app.UseCors("MyPolicy");

            app.UseAuthentication();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHealthChecks("/api/health");
            });

        }
    }
}
