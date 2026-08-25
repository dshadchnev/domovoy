using System.Collections.Generic;
using Domovoy.Domain.Entities;

namespace Domovoy.Domain.Services
{
    /// <summary>
    /// Доменный сервис вычисления условий автоматизации и правил сценариев.
    /// </summary>
    public interface IDomainRuleEngine
    {
        /// <summary>
        /// Вычисляет, должен ли сработать доменный сценарий на основе условия и параметров телеметрии.
        /// </summary>
        bool EvaluateScenario(Scenario scenario, string? rawCondition, IDictionary<string, object?> variables);

        /// <summary>
        /// Вычисляет, выполняется ли условие сценария для заданного значения.
        /// </summary>
        bool EvaluateCondition(ScenarioCondition condition, decimal actualValue);

        /// <summary>
        /// Вычисляет NCalc-выражение правила для заданных переменных.
        /// </summary>
        bool EvaluateExpression(string expression, IDictionary<string, object?> variables);

        /// <summary>
        /// Проверяет синтаксическую корректность выражения правила.
        /// </summary>
        bool ValidateExpression(string expression);
    }
}
