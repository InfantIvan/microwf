using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace tomware.Microwf.Engine
{
  public interface IWorkflowDefinitionViewModelCreator
  {
    WorkflowDefinitionViewModel CreateViewModel(string type);
  }

  public class ConfigurationWorkflowDefinitionViewModelCreator
    : IWorkflowDefinitionViewModelCreator
  {
    private readonly WorkflowConfiguration _workflowConfiguration;

    public ConfigurationWorkflowDefinitionViewModelCreator(
      IOptions<WorkflowConfiguration> workflows
    )
    {
      this._workflowConfiguration = workflows.Value;
    }

    public WorkflowDefinitionViewModel CreateViewModel(string type)
    {
      var workflowType = this._workflowConfiguration.Types.FirstOrDefault(_ => _.Type == type);

      return new WorkflowDefinitionViewModel
      {
        Type = workflowType.Type,
        Title = workflowType.Title,
        Route = workflowType.Route,
        Description = workflowType.Description
      };
    }
  }
}
