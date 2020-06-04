/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.AzureAD.UI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using TeamCloud.API.Formatters;
using TeamCloud.API.Middleware;
using TeamCloud.API.Routing;
using TeamCloud.API.Services;
using TeamCloud.Azure;
using TeamCloud.Azure.Deployment;
using TeamCloud.Azure.Deployment.Providers;
using TeamCloud.Azure.Directory;
using TeamCloud.Configuration;
using TeamCloud.Configuration.Options;
using TeamCloud.Data;
using TeamCloud.Data.CosmosDb;
using TeamCloud.Http;
using TeamCloud.Model.Data;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TeamCloud.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

#pragma warning disable CA1822 // Mark members as static

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
            }

            app.UseWhen(context => !(context.Request.Path.StartsWithSegments("/api/users", StringComparison.OrdinalIgnoreCase)
                                && HttpMethods.IsPost(context.Request.Method)), appBuilder =>
            {
                // ensure TeamCloud to be configured for all paths other than /api/users
                appBuilder.UseMiddleware<EnsureTeamCloudUserMiddleware>();
            });

            // Enable middleware to serve generated Swagger as a JSON endpoint.
            app.UseSwagger();

            // Enable middleware to serve swagger-ui (HTML, JS, CSS, etc.),
            // specifying the Swagger JSON endpoint.
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "TeamCloud API v1");
            });

            app.UseHttpsRedirection()
               .UseRouting()
               .UseAuthentication()
               .UseAuthorization()
               .UseEndpoints(endpoints => endpoints.MapControllers());
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<KestrelServerOptions>(options => options.AllowSynchronousIO = true);
            services.Configure<IISServerOptions>(options => options.AllowSynchronousIO = true);

            services
                .AddMemoryCache()
                .AddTeamCloudOptions(Assembly.GetExecutingAssembly())
                .AddTeamCloudOptionsShared()
                .AddTeamCloudAzure(configuration =>
                {
                    configuration
                        .AddDirectory()
                        .AddDeployment()
                        .SetDeploymentArtifactsProvider<AzureStorageArtifactsProvider>();
                })
                .AddTeamCloudHttp(configuration =>
                {
                    // nothing to configure
                });

            services
                .AddSingleton<IHttpContextAccessor, HttpContextAccessor>()
                .AddSingleton<Orchestrator>()
                .AddSingleton<UserService>()
                .AddScoped<IProjectsRepositoryReadOnly, CosmosDbProjectsRepository>()
                .AddScoped<ITeamCloudRepositoryReadOnly, CosmosDbTeamCloudRepository>()
                .AddScoped<IProjectTypesRepositoryReadOnly, CosmosDbProjectTypesRepository>()
                .AddScoped<EnsureTeamCloudUserMiddleware>()
                .AddSingleton<IClientErrorFactory, ClientErrorFactory>();

            ConfigureAuthentication(services);
            ConfigureAuthorization(services);

            services
                .AddMvc(options =>
                {
                    options.InputFormatters.Add(new YamlInputFormatter(new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()));
                    options.OutputFormatters.Add(new YamlOutputFormatter(new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()));
                    options.FormatterMappings.SetMediaTypeMappingForFormat("application/x-yaml", MediaTypeHeaderValues.ApplicationYaml);
                    options.FormatterMappings.SetMediaTypeMappingForFormat("text/yaml", MediaTypeHeaderValues.TextYaml);
                });

            services
                .AddRouting(options =>
                {
                    options.ConstraintMap.Add("userNameOrId", typeof(UserIdentifierRouteConstraint));
                    options.ConstraintMap.Add("projectNameOrId", typeof(ProjectIdentifierRouteConstraint));
                })
                .AddControllers()
                .AddNewtonsoftJson()
                .ConfigureApiBehaviorOptions(options => options.SuppressMapClientErrors = true);

#pragma warning disable CA1308 // Normalize strings to uppercase

            ValidatorOptions.DisplayNameResolver = (type, memberInfo, lambda) => memberInfo?.Name?.ToLowerInvariant();
            ValidatorOptions.PropertyNameResolver = (type, memberInfo, lambda) => memberInfo?.Name?.ToLowerInvariant();

#pragma warning restore CA1308 // Normalize strings to uppercase

            ConfigureSwagger(services);
        }

#pragma warning restore CA1822 // Mark members as static

        private static void ConfigureSwagger(IServiceCollection services)
        {
            services
                .AddSwaggerGen(options =>
                {
                    options.SwaggerDoc("v1", new OpenApiInfo
                    {
                        Version = "v1",
                        Title = "TeamCloud",
                        Description = "API for working with a TeamCloud instance.",
                        Contact = new OpenApiContact
                        {
                            Url = new Uri("https://github.com/microsoft/TeamCloud/issues/new"),
                            Email = @"Markus.Heiliger@microsoft.com",
                            Name = "TeamCloud Dev Team"
                        },
                        License = new OpenApiLicense
                        {
                            Name = "TeamCloud is licensed under the MIT License",
                            Url = new Uri("https://github.com/microsoft/TeamCloud/blob/master/LICENSE")
                        }
                    });

                    // options.AddFluentValidationRules();
                    options.EnableAnnotations();
                    options.UseInlineDefinitionsForEnums();

                    options.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oauth2" },
                            },
                            new [] { "default", "admin", "projectCreate", "projectRead", "projectDelete" }
                        }
                    });

                    options.OperationFilter<SecurityRequirementsOperationFilter>();
                })
                .AddSwaggerGenNewtonsoftSupport(); // explicit Newtonsoft opt-in - needs to be placed after AddSwaggerGen()
        }

        private static void ConfigureAuthentication(IServiceCollection services)
        {
            var resourceManagerOptions = services
                .BuildServiceProvider()
                .GetRequiredService<AzureResourceManagerOptions>();

            services
                .AddAuthentication(AzureADDefaults.JwtBearerAuthenticationScheme)
                .AddAzureADBearer(options =>
                {
                    options.Instance = AzureEnvironment.AzureGlobalCloud.AuthenticationEndpoint;
                    options.TenantId = resourceManagerOptions.TenantId;
                });

            services
                .AddHttpContextAccessor()
                .Configure<JwtBearerOptions>(AzureADDefaults.JwtBearerAuthenticationScheme, options =>
                {
                    // This is an Microsoft identity platform Web API
                    options.Authority += "/v2.0";

                    // Disable audience validation
                    options.TokenValidationParameters.ValidateAudience = false;

                    // The valid issuers can be based on Azure identity V1 or V2
                    options.TokenValidationParameters.ValidIssuers = new string[]
                    {
                        $"https://login.microsoftonline.com/{resourceManagerOptions.TenantId}/v2.0",
                        $"https://sts.windows.net/{resourceManagerOptions.TenantId}/"
                    };

                    options.Events = new JwtBearerEvents()
                    {
                        OnTokenValidated = async (TokenValidatedContext context) =>
                        {
                            var userId = context.Principal.GetObjectId();

                            var userClaims = await ResolveClaimsAsync(userId, context.HttpContext).ConfigureAwait(false);
                            if (userClaims.Any()) context.Principal.AddIdentity(new ClaimsIdentity(userClaims));
                        }
                    };
                });
        }

        private static void ConfigureAuthorization(IServiceCollection services)
        {
            services
                .AddMvc(options =>
                {
                    // Requires authentication across the API
                    options.Filters.Add(new AuthorizeFilter("default"));
                });

            services
                .AddAuthorization(options =>
                {
                    options.AddPolicy("default", policy =>
                    {
                        policy.RequireAuthenticatedUser();
                    });

                    options.AddPolicy("admin", policy =>
                    {
                        policy.RequireRole(UserRoles.TeamCloud.Admin);
                    });

                    options.AddPolicy("projectCreate", policy =>
                    {
                        policy.RequireRole(UserRoles.TeamCloud.Admin, UserRoles.TeamCloud.Creator);
                    });

                    options.AddPolicy("projectRead", policy =>
                    {
                        policy.RequireRole(UserRoles.TeamCloud.Admin, UserRoles.Project.Owner, UserRoles.Project.Member);
                    });

                    options.AddPolicy("projectDelete", policy =>
                    {
                        policy.RequireRole(UserRoles.TeamCloud.Admin, UserRoles.Project.Owner);
                    });
                });
        }

        private static async Task<IEnumerable<Claim>> ResolveClaimsAsync(Guid userId, HttpContext httpContext)
        {
            // TODO: Try to cache this so every API call doesn't perform two DB calls

            var claims = new List<Claim>();

            var teamCloudRepository = httpContext.RequestServices
                .GetRequiredService<ITeamCloudRepositoryReadOnly>();

            var teamCloudInstance = await teamCloudRepository
                .GetAsync()
                .ConfigureAwait(false);

            var teamCloudClaims = teamCloudInstance?.Users?
                .Where(u => u.Id.Equals(userId))
                .Select(u => new Claim(ClaimTypes.Role, u.Role));

            if (teamCloudClaims != null)
                claims.AddRange(teamCloudClaims);


            if (httpContext.Request.Path.StartsWithSegments("/api/projects", StringComparison.OrdinalIgnoreCase))
            {
                var projectIdRouteValue = httpContext.GetRouteData()
                    .Values.GetValueOrDefault("ProjectId", StringComparison.OrdinalIgnoreCase)?.ToString();

                if (Guid.TryParse(projectIdRouteValue, out Guid projectId))
                {
                    var projectRepository = httpContext.RequestServices
                        .GetRequiredService<IProjectsRepositoryReadOnly>();

                    var project = await projectRepository
                        .GetAsync(projectId)
                        .ConfigureAwait(false);

                    var projectClaims = project?.Users?
                        .Where(u => u.Id == userId)
                        .Select(u => new Claim(ClaimTypes.Role, u.Role));

                    if (projectClaims != null)
                        claims.AddRange(projectClaims);
                }
            }

            return claims;
        }
    }
}
