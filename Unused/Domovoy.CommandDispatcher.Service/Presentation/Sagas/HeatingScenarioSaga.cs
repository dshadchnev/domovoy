using System;
using MassTransit;
using Domovoy.Shared.Events;

namespace Domovoy.CommandDispatcher.Service.Presentation.Sagas;

public class HeatingScenarioSaga : MassTransitStateMachine<HeatingSagaState>
{
    public State ExecutingCommand { get; private set; } = null!;
    public State Verifying { get; private set; } = null!;
    public State Completed { get; private set; } = null!;
    public State FailedCompensated { get; private set; } = null!;

    public Event<StartHeatingScenarioEvent> StartScenario { get; private set; } = null!;
    public Event<HeatingVerifiedEvent> HeatingVerified { get; private set; } = null!;
    public Event<HeatingCommandFailedEvent> CommandFailed { get; private set; } = null!;

    public HeatingScenarioSaga()
    {
        InstanceState(x => x.CurrentState);

        Event(() => StartScenario, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => HeatingVerified, x => x.CorrelateById(context => context.Message.CorrelationId));
        Event(() => CommandFailed, x => x.CorrelateById(context => context.Message.CorrelationId));

        Initially(
            When(StartScenario)
                .Then(context =>
                {
                    context.Saga.DeviceId = context.Message.DeviceId;
                    context.Saga.TargetTemp = context.Message.TargetTemp;
                    context.Saga.UserId = context.Message.UserId;
                    context.Saga.UpdatedAt = DateTime.UtcNow;
                })
                .TransitionTo(ExecutingCommand)
                .Publish(context => new ExecuteCommandEvent(
                    DeviceId: context.Saga.DeviceId,
                    Command: "turn_on_heating",
                    Params: $"{{\"targetTemp\":\"{context.Saga.TargetTemp}\"}}",
                    SourceRuleId: context.Saga.CorrelationId.ToString(),
                    Timestamp: DateTime.UtcNow
                ))
                .TransitionTo(Verifying)
        );

        During(Verifying,
            When(HeatingVerified)
                .Then(context => context.Saga.UpdatedAt = DateTime.UtcNow)
                .TransitionTo(Completed)
                .Finalize(),

            When(CommandFailed)
                .Then(context => context.Saga.UpdatedAt = DateTime.UtcNow)
                // Компенсационное действие: откат ("выключить отопление")
                .Publish(context => new ExecuteCommandEvent(
                    DeviceId: context.Saga.DeviceId,
                    Command: "turn_off_heating",
                    Params: "{\"compensation\": true}",
                    SourceRuleId: context.Saga.CorrelationId.ToString(),
                    Timestamp: DateTime.UtcNow
                ))
                // Уведомление пользователя о сбое и компенсации
                .Publish(context => new CommandFailedEvent(
                    DeviceId: context.Saga.DeviceId,
                    Command: "turn_on_heating (compensated)",
                    ErrorMessage: context.Message.ErrorMessage,
                    Timestamp: DateTime.UtcNow
                ))
                .TransitionTo(FailedCompensated)
        );

        SetCompletedWhenFinalized();
    }
}
