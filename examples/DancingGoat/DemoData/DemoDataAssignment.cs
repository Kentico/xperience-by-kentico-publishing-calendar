using System;

namespace DancingGoat.DemoData;

/// <summary>
/// Target state of one language variant.
/// </summary>
/// <param name="Variant">The variant.</param>
/// <param name="State">The state to set.</param>
/// <param name="ModifiedWhen">Backdated last modification (the calendar's activity date), server local time.</param>
/// <param name="ScheduledWhen">Scheduled publish or unpublish time for the scheduled states, server local time.</param>
/// <param name="WorkflowStepName">Custom workflow step code name for <see cref="DemoDataState.InWorkflow"/>.</param>
public sealed record DemoDataAssignment(
    DemoDataVariant Variant,
    DemoDataState State,
    DateTime ModifiedWhen,
    DateTime? ScheduledWhen,
    string WorkflowStepName);
