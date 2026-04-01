using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities

{
    /// <summary>
    /// Условие для выполнения сценария
    /// </summary>
    public class ScenarioCondition
    {
        // Уникальный идентификатор условия.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Внешний ключ на таблицу Scenarios.
        // Сценарий, к которому относится условие.
        public Guid ScenarioId { get; set; }

        // Тип условия (Time, SensorValue, DeviceState (ремя, значение датчика, состояние устройства) и т.д.).
        // Обязательное поле, максимальная длина 50 символов.
        [Required]
        [MaxLength(50)]
        public string ConditionType { get; set; }

        // Оператор сравнения (>, <, ==, >=, <=).
        // Максимальная длина 200 символов.
        [MaxLength(200)]
        public string Operator { get; set; }

        // Значение для сравнения.
        // Максимальная длина 500 символов.
        [MaxLength(500)]
        public string Value { get; set; }

        // Внешний ключ на таблицу Sensors (nullable).
        // Датчик, значение которого проверяется (если условие SensorValue).
        public Guid? SensorId { get; set; }

        // Внешний ключ на таблицу Devices (nullable).
        // Устройство, состояние которого проверяется (если условие DeviceState).
        public Guid? DeviceId { get; set; }

        // Порядок выполнения условия (если условий несколько).
        // Значение по умолчанию 1 (первое условие).
        public int Order { get; set; } = 1;

        // Навигационные свойства:

        // Ссылка на сценарий, к которому относится условие.
        public virtual Scenario Scenario { get; set; }

        // Ссылка на датчик (nullable).
        public virtual Sensor Sensor { get; set; }

        // Ссылка на устройство (nullable).
        public virtual Device Device { get; set; }
    }
}
