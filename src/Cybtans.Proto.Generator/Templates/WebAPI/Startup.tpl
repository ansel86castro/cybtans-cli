using System;
using System.Text;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Cybtans.AspNetCore;
using Cybtans.Entities.EntityFrameworkCore;
using Cybtans.Services.Extensions;
using @{SERVICE}.Data;
using @{SERVICE}.Data.Repositories;
using @{SERVICE}.Services;

namespace @{NAMESPACE}
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddHttpContextAccessor();

            AddSwagger(services);

            AddAuthentication(services);

            #region Cors
            services.AddCors(options =>            
            options.AddDefaultPolicy(
                builder =>
                {
                    builder.SetIsOriginAllowedToAllowWildcardSubdomains()
                    .WithOrigins(Configuration.GetValue<string>("CorsOrigins").Split(','))
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .WithExposedHeaders("Content-Type", "Content-Disposition")
                    //.AllowCredentials()
                    ;
                }));
            #endregion                 
         
            #region Controllers 
            services
            .AddControllers(options => 
            {
                options.Filters.Add<HttpResponseExceptionFilter>();
            })            
            // Uncomment to enable cybtans binary formatting
            //.AddCybtansFormatter()
            ;  
            #endregion            
            
            #region App Services    
            //services.AddAutoMapper(typeof(@{SERVICE}Stub));

            services.AddCybtansServices(typeof(@{SERVICE}Stub).Assembly);         
            #endregion

            //#region Data Access 
            //services.AddDbContext<@{SERVICE}Context>(builder => builder.UseSqlServer(Configuration.GetConnectionString("DefaultConnection")));
            //services.AddUnitOfWork<@{SERVICE}Context>().AddRepositories();
            //#endregion
            
            //#region Validations
            //services.AddDefaultValidatorProvider(p => p.AddValidatorFromAssembly(typeof(AgentsStub).Assembly));
            //services.AddSingleton<IMessageInterceptor, MyMessageInterceptor>();
            //#endregion


            //#region Messaging                                 
            //services.AddRabbitMessageQueue(Configuration)
            // .ConfigureSubscriptions(sm =>
            // {
            //     sm.SubscribeHandlerForEvents<TMessage, THandler>("Test");
            //     sm.SubscribeForEvents<TEntityEvent, TEntity>("Test");
            // });
            //
            //services.AddAccessTokenManager(Configuration);
            //services.AddRabbitBroadCastService(Configuration.GetSection("BroadCastOptions").Get<BroadcastServiceOptions>());
            //#endregion

           //#region Caching
           //services.AddRedisCache(o => o.Connection = "localhost");
           //services.AddDistributedLockProvider();
           //#endregion
        }
        
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
             EfAsyncQueryExecutioner.Setup();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandlingMiddleware();
            }

            app.UseCors();

            UseSwagger(app);

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        #region Private

        void AddSwagger(IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "@{SERVICE}", Version = "v1" });
                c.OperationFilter<SwachBuckleOperationFilters>();
                c.SchemaFilter<SwachBuckleSchemaFilters>();

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                });


                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Implicit = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{Configuration.GetValue<string>("Identity:Swagger")}/connect/authorize"),
                            TokenUrl = new Uri($"{Configuration.GetValue<string>("Identity:Swagger")}/connect/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                { "api", "@{SERVICE} API" }
                            }
                        }
                    }
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                             Reference = new OpenApiReference
                             {
                                  Type = ReferenceType.SecurityScheme,
                                  Id = "oauth2"
                             }
                        },
                        new string[0]
                    },
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[0]
                    }
                });


            });
        }

        void AddAuthentication(IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = Configuration.GetValue<string>("Identity:Authority");
                options.Audience = $"{options.Authority}/resources";                 
                options.RequireHttpsMetadata = false;                    
                options.SaveToken = true;               
            });

            services.AddAuthorization();
		}
    
        void UseSwagger(IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "@{SERVICE} V1");
                c.EnableFilter();
                c.EnableDeepLinking();
                c.ShowCommonExtensions();  
                
                c.OAuthClientId("swagger");
                c.OAuthClientSecret(Configuration.GetValue<string>("Identity:Secret"));
                c.OAuthAppName("@{SERVICE}");
                c.OAuthUsePkce();
            });
            
            app.UseReDoc(c =>
            {
                c.RoutePrefix = "docs";
                c.SpecUrl("/swagger/v1/swagger.json");
                c.DocumentTitle = "@{SERVICE} API";
            });
        }

        #endregion
    }
}
