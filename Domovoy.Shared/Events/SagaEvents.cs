using System;

namespace Domovoy.Shared.Events;

/// <summary>
/// Запуск оркестрации сценария нагрева в Saga
/// </summary>
public record StartHeatingScenarioEvent(
    Guid CorrelationId,
    string DeviceId,
    string TargetTemp,
    Guid UserId);

/// <summary>
/// Подтверждение успешной работы сценария нагрева
/// </summary>
public record HeatingVerifiedEvent(
    Guid CorrelationId,
    string DeviceId);

/// <summary>
/// Сбой выполнения команды в сценарии нагрева
/// </summary>
public record HeatingCommandFailedEvent(
    Guid CorrelationId,
    string DeviceId,
    string ErrorMessage);
