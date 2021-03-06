using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using tomware.Microwf.Core;
using WebApi.Common;
using WebApi.Domain;

namespace tomware.Microwf.Engine
{
  public interface IWorkflowEngine
  {
    IEnumerable<TriggerResult> GetTriggers(
      IWorkflow instance,
      Dictionary<string, WorkflowVariableBase> variables = null
    );

    TriggerResult CanTrigger(TriggerParam param);

    TriggerResult Trigger(TriggerParam param);

    IWorkflow Find(int id, Type type);
  }

  public class WorkflowEngine<TContext> : IWorkflowEngine where TContext : DbContext
  {
    private readonly DomainContext _context;
    private readonly ILogger<WorkflowEngine<TContext>> _logger;
    private readonly IWorkflowDefinitionProvider _workflowDefinitionProvider;

    public WorkflowEngine(
      DomainContext context,
      ILogger<WorkflowEngine<TContext>> logger,
      IWorkflowDefinitionProvider workflowDefinitionProvider
    )
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _logger = logger;
      _workflowDefinitionProvider = workflowDefinitionProvider;
    }

    public TriggerResult CanTrigger(TriggerParam param)
    {
      if (param == null) throw new InvalidOperationException(nameof(param));

      var execution = GetExecution(param.Instance.Type);

      return execution.CanTrigger(param);
    }

    public IEnumerable<TriggerResult> GetTriggers(
      IWorkflow instance,
      Dictionary<string, WorkflowVariableBase> variables = null
    )
    {
      if (instance == null) throw new InvalidOperationException(nameof(instance));

      var execution = GetExecution(instance.Type);

      return execution.GetTriggers(instance, variables);
    }

    public TriggerResult Trigger(TriggerParam param)
    {
      if (param == null) throw new InvalidOperationException(nameof(param));

      var entity = param.Instance as IEntityWorkflow;
      if (entity == null) throw new Exception("No entity given!");

      TriggerResult result = null;
      using (var transaction = this._context.Database.BeginTransaction())
      {
        try
        {
          Workflow workflow = null;

          var execution = GetExecution(param.Instance.Type);

          _context.SaveChanges(); // so entity id gets resolved!

          workflow = FindOrCreate(
            entity.Id,
            param.Instance.Type,
            param.Instance.State,
            entity.Assignee
          );

          result = execution.Trigger(param);
          if (!result.IsAborted)
          {
            PersistWorkflow(workflow, param);

            _context.SaveChanges();
            transaction.Commit();
          }
        }
        catch (Exception ex)
        {
          transaction.Rollback();

          _logger.LogError(
            $"Error in triggering: {param.Instance.Type}, EntityId: {entity.Id}",
            ex.StackTrace
          );
        }
      }

      return result;
    }

    public IWorkflow Find(int id, Type type)
    {
      return (IWorkflow)_context.Workflows.Find(type, id);
    }

    private WorkflowExecution GetExecution(string type)
    {
      var definition = this._workflowDefinitionProvider.GetWorkflowDefinition(type);

      return new WorkflowExecution(definition);
    }

    private Workflow FindOrCreate(int id, string type, string state, string assignee)
    {
      var workflow = this._context.Workflows
        .SingleOrDefault(w => w.CorrelationId == id && w.Type == type);
      if (workflow == null)
      {
        workflow = Workflow.Create(id, type, state, assignee);
        this._context.Workflows.Add(workflow);
      }

      return workflow;
    }

    private void PersistWorkflow(
      Workflow workflow,
      TriggerParam triggerParam,
      DateTime? dueDate = null
    )
    {
      if (workflow == null) throw new ArgumentNullException(nameof(workflow));

      var entityWorkflow = triggerParam.Instance as IEntityWorkflow;
      if (entityWorkflow != null)
      {
        workflow.Type = entityWorkflow.Type;
        workflow.State = entityWorkflow.State;
        workflow.Assignee = entityWorkflow.Assignee;
      }

      if (WorkflowIsCompleted(triggerParam))
      {
        workflow.Completed = SystemTime.Now();
      }
      
      workflow.DueDate = dueDate;
    }

    private bool WorkflowIsCompleted(TriggerParam triggerParam)
    {
      var triggerResults = this.GetTriggers(triggerParam.Instance, triggerParam.Variables);

      return triggerResults.Count() == 0;
    }
  }
}