using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using tomware.Microwf.Core;
using WebApi.Common;

namespace tomware.Microwf.Engine
{
  public static class MicrowfServicesExtensions
  {
    public static IServiceCollection AddWorkflowEngineServices<TContext>(
      this IServiceCollection services,
      IConfiguration workflowsConfiguration,
      bool enableWorker
    ) where TContext : DbContext
    {
      services.Configure<WorkflowConfiguration>(workflowsConfiguration);
      
      if (enableWorker)
      {
        services.AddSingleton<IHostedService, WorkflowProcessor>();
      }

      services.AddSingleton<IWorkflowDefinitionProvider, WorkflowDefinitionProvider>();
      services.AddTransient<IWorkflowEngine, WorkflowEngine<TContext>>();
      services.AddTransient<
        IWorkflowDefinitionViewModelCreator,
        ConfigurationWorkflowDefinitionViewModelCreator>();
      services.AddTransient<IWorkflowService, WorkflowService>();
      services
            .AddTransient<IUserWorkflowDefinitionService, NoopUserWorkflowDefinitionService>();


      return services;
    }

    public static IServiceCollection AddTestUserWorkflows(
      this IServiceCollection services,
      IEnumerable<UserWorkflows> userWorkflows
    )
    {
      services
       .AddTransient<IUserWorkflowDefinitionService, InMemoryUserWorkflowDefinitionService>();
      services.AddSingleton(new UserWorkflowsStore(userWorkflows));

      return services;
    }
  }
}
