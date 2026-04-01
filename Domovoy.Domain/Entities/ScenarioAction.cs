using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Действие, выполняемое сценарием
    /// </summary>
    public class ScenarioAction
    {
        // Уникальный идентификатор действия.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Внешний ключ на таблицу Scenarios.
        // Сценарий, к которому относится действие.
        public Guid ScenarioId { get; set; }

        // Тип действия (SetDeviceState, SendNotification, ExecuteScript (Установить состояние устройства, Отправить уведомление, Выполнить скрипт) и т.д.)
        // Обязательное поле, максимальная длина 50 символов.
        [Required]
        [MaxLength(50)]
        public string ActionType { get; set; }

        // Внешний ключ на таблицу Devices (nullable).
        // Целевое устройство для действия.
        public Guid? TargetDeviceId { get; set; }

        // Внешний ключ на таблицу Actuators (nullable).
        // Целевой актуатор для действия.
        public Guid? TargetActuatorId { get; set; }


        // Команда для выполнения.
        // Максимальная длина 500 символов.
        [MaxLength(500)]
        public string Command { get; set; }

        // Параметры действия в формате JSON.
        // Максимальная длина 1000 символов.
        [MaxLength(1000)]
        public string Parameters { get; set; }

        // Порядок выполнения действия (если действий несколько).
        // Значение по умолчанию 1 (первое действие).
        public int Order { get; set; } = 1;

        // Задержка перед выполнением действия в секундах.
        // Значение по умолчанию 0 (без задержки).
        public int DelaySeconds { get; set; } = 0;

        // Навигационные свойства:

        // Ссылка на сценарий, к которому относится действие.
        public virtual Scenario Scenario { get; set; }

        // Ссылка на целевое устройство (nullable).
        public virtual Device TargetDevice { get; set; }

        // Ссылка на целевой актуатор (nullable).
        public virtual Actuator TargetActuator { get; set; }
    }
}
