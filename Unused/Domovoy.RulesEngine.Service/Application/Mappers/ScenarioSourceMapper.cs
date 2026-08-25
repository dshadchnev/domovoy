using System;
using Riok.Mapperly.Abstractions;
using Domovoy.Domain.Entities;
using Domovoy.RulesEngine.Service.Infrastructure.Persistence;

namespace Domovoy.RulesEngine.Service.Application.Mappers;

/// <summary>
/// Source Generator mapper for Scenario domain aggregate and Rule entity
/// </summary>
[Mapper]
public partial class ScenarioSourceMapper
{
    [MapProperty(nameof(Scenario.UserId), nameof(Rule.UserId))]
    [MapProperty(nameof(Scenario.Name), nameof(Rule.Name))]
    [MapProperty(nameof(Scenario.IsActive), nameof(Rule.IsActive))]
    public partial Rule ScenarioToRule(Scenario scenario);
}
