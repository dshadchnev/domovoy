using System;
using System.Threading.Tasks;
using Domovoy.CommandDispatcher.Service.Presentation.Sagas;
using Domovoy.Shared.Events;
using MassTransit.Testing;
using Xunit;

namespace Domovoy.CommandDispatcher.Service.Tests;

public class HeatingScenarioSagaTests
{
    [Fact]
    public async Task HeatingScenarioSaga_OnStartScenario_TransitionsToVerifyingAndPublishesTurnOnCommand()
    {
        // Arrange
        var saga = new HeatingScenarioSaga();
        var harness = new InMemoryTestHarness();
        var sagaHarness = harness.StateMachineSaga<HeatingSagaState, HeatingScenarioSaga>(saga);

        await harness.Start();
        try
        {
            var correlationId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            // Act
            await harness.Bus.Publish(new StartHeatingScenarioEvent(
                CorrelationId: correlationId,
                DeviceId: "heater-1",
                TargetTemp: "23",
                UserId: userId
            ));

            // Assert
            Assert.True(await sagaHarness.Consumed.Any<StartHeatingScenarioEvent>());
            Assert.True(await sagaHarness.Created.Any(x => x.CorrelationId == correlationId));

            var instance = sagaHarness.Created.Contains(correlationId);
            Assert.NotNull(instance);
            Assert.Equal("Verifying", instance.CurrentState);

            Assert.True(await harness.Published.Any<ExecuteCommandEvent>(x =>
                x.Context.Message.DeviceId == "heater-1" &&
                x.Context.Message.Command == "turn_on_heating"
            ));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task HeatingScenarioSaga_OnCommandFailed_ExecutesCompensationAndPublishesNotification()
    {
        // Arrange
        var saga = new HeatingScenarioSaga();
        var harness = new InMemoryTestHarness();
        var sagaHarness = harness.StateMachineSaga<HeatingSagaState, HeatingScenarioSaga>(saga);

        await harness.Start();
        try
        {
            var correlationId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            // Start saga
            await harness.Bus.Publish(new StartHeatingScenarioEvent(
                CorrelationId: correlationId,
                DeviceId: "heater-1",
                TargetTemp: "23",
                UserId: userId
            ));

            Assert.True(await sagaHarness.Consumed.Any<StartHeatingScenarioEvent>());

            // Act: Command failed during verification
            await harness.Bus.Publish(new HeatingCommandFailedEvent(
                CorrelationId: correlationId,
                DeviceId: "heater-1",
                ErrorMessage: "Device connection timed out"
            ));

            // Assert
            Assert.True(await sagaHarness.Consumed.Any<HeatingCommandFailedEvent>());

            var instance = sagaHarness.Created.Contains(correlationId);
            Assert.NotNull(instance);
            Assert.Equal("FailedCompensated", instance.CurrentState);

            // Verify compensation command turn_off_heating published
            Assert.True(await harness.Published.Any<ExecuteCommandEvent>(x =>
                x.Context.Message.DeviceId == "heater-1" &&
                x.Context.Message.Command == "turn_off_heating"
            ));

            // Verify failure notification published
            Assert.True(await harness.Published.Any<CommandFailedEvent>(x =>
                x.Context.Message.DeviceId == "heater-1" &&
                x.Context.Message.ErrorMessage == "Device connection timed out"
            ));
        }
        finally
        {
            await harness.Stop();
        }
    }
}
