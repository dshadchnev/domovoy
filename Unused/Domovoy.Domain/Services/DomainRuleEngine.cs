using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Domovoy.Domain.Entities;
using NCalc;

namespace Domovoy.Domain.Services
{
    /// <summary>
    /// Декларативный доменный сервис оценки правил и сценариев автоматизации.
    /// Использует композицию функциональных предикатов вместо императивных циклов.
    /// </summary>
    public class DomainRuleEngine : IDomainRuleEngine
    {
        public bool EvaluateScenario(Scenario scenario, string? rawCondition, IDictionary<string, object?> variables)
        {
            if (scenario == null || !scenario.IsActive) return false;

            var vars = variables ?? new Dictionary<string, object?>();

            // 1. Декларативная оценка доменных условий Scenario.Conditions через композицию предикатов
            if (scenario.Conditions != null && scenario.Conditions.Count > 0)
            {
                var combinedPredicate = scenario.Conditions
                    .Select(BuildConditionPredicate)
                    .Aggregate((Func<IDictionary<string, object?>, bool>)((_) => false), (acc, pred) => env => acc(env) || pred(env));

                if (combinedPredicate(vars)) return true;

                return false;
            }

            // 2. Декларативный фоллбек на сырое выражение правила
            if (!string.IsNullOrWhiteSpace(rawCondition))
            {
                return EvaluateExpression(rawCondition, vars);
            }

            return false;
        }

        /// <summary>
        /// Построение чистой функции-предиката на основе объекта доменного условия
        /// </summary>
        public Func<IDictionary<string, object?>, bool> BuildConditionPredicate(ScenarioCondition cond)
        {
            return cond.ConditionType switch
            {
                "Telemetry" => env =>
                {
                    var numericVal = env.Values.FirstOrDefault(v => v != null && TryConvertToDecimal(v, out _));
                    return numericVal != null && TryConvertToDecimal(numericVal, out var val) && EvaluateCondition(cond, val);
                },
                "TelemetryExpression" when !string.IsNullOrWhiteSpace(cond.Value) => env => EvaluateExpression(cond.Value, env),
                _ => _ => false
            };
        }

        private static bool TryConvertToDecimal(object? obj, out decimal result)
        {
            result = 0m;
            if (obj == null) return false;
            try
            {
                if (obj is decimal d) { result = d; return true; }
                if (obj is double db) { result = Convert.ToDecimal(db); return true; }
                if (obj is float f) { result = Convert.ToDecimal(f); return true; }
                if (obj is int i) { result = i; return true; }
                if (obj is long l) { result = l; return true; }

                var str = obj.ToString();
                if (string.IsNullOrWhiteSpace(str)) return false;

                return decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
                       decimal.TryParse(str, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
            }
            catch
            {
                return false;
            }
        }

        public bool EvaluateCondition(ScenarioCondition condition, decimal actualValue)
        {
            if (condition == null) throw new ArgumentNullException(nameof(condition));
            if (!decimal.TryParse(condition.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out var targetValue))
            {
                return false;
            }

            var op = condition.Operator?.Trim();
            return op switch
            {
                ">" => actualValue > targetValue,
                ">=" => actualValue >= targetValue,
                "<" => actualValue < targetValue,
                "<=" => actualValue <= targetValue,
                "==" or "=" => actualValue == targetValue,
                "!=" => actualValue != targetValue,
                _ => false
            };
        }

        public bool EvaluateExpression(string expression, IDictionary<string, object?> variables)
        {
            if (string.IsNullOrWhiteSpace(expression)) return false;

            try
            {
                var expr = new Expression(expression)
                {
                    Parameters = variables != null
                        ? variables.ToDictionary(k => k.Key, v => v.Value)
                        : new Dictionary<string, object?>()
                };

                var result = expr.Evaluate();
                return result is bool b && b;
            }
            catch
            {
                return false;
            }
        }

        public bool ValidateExpression(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return false;

            try
            {
                var expr = new Expression(expression);
                return !expr.HasErrors();
            }
            catch
            {
                return false;
            }
        }
    }
}
