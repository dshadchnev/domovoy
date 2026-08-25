using System;
using MassTransit;

namespace Domovoy.CommandDispatcher.Service.Presentation.Sagas;

public class HeatingSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;
    public string TargetTemp { get; set; } = string.Empty;
    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
