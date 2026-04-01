using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Сценарий автоматизации
    /// </summary>
    public class Scenario
    {
        // Уникальный идентификатор сценария.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Название сценария для идентификации.
        // Обязательное поле, максимальная длина 100 символов.
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        // Внешний ключ на таблицу Users.
        // Владелец/создатель сценария.
        public Guid UserId { get; set; }

        // Описание сценария (необязательное поле).
        // Максимальная длина 500 символов.
        [MaxLength(500)]
        public string Description { get; set; }

        // Флаг активности сценария.
        // По умолчанию true (активен).
        public bool IsActive { get; set; } = true;

        // Флаг повторяемости сценария.
        // По умолчанию false (одноразовый).
        public bool IsRecurring { get; set; } = false;

        // Расписание выполнения (Cron expression (конкретное время)).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string Schedule { get; set; }

        // Дата и время создания сценария.
        // Автоматически устанавливается при создании.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Дата и время последнего выполнения сценария.
        // Nullable, так как сценарий может еще не выполняться.
        public DateTime? LastExecuted { get; set; }


        // Навигационные свойства:

        // Ссылка на владельца сценария.
        public virtual User User { get; set; }

        // Условия выполнения сценария (один-ко-многим).
        public virtual ICollection<ScenarioCondition> Conditions { get; set; } = new List<ScenarioCondition>();

        // Действия, выполняемые сценарием (один-ко-многим).
        public virtual ICollection<ScenarioAction> Actions { get; set; } = new List<ScenarioAction>();
    }
}
