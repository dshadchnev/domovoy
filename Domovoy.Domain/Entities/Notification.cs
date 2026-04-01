using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Уведомление для пользователя
    /// </summary>
    public class Notification
    {
        // Уникальный идентификатор уведомления.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Внешний ключ на таблицу Users.
        // Пользователь, которому адресовано уведомление.
        public Guid UserId { get; set; }

        // Заголовок уведомления.
        // Обязательное поле, максимальная длина 100 символов.
        [Required]
        [MaxLength(100)]
        public string Title { get; set; }

        // Текст сообщения уведомления.
        // Максимальная длина 1000 символов.
        [MaxLength(1000)]
        public string Message { get; set; }

        // Тип уведомления (Info, Warning, Alert (Информация, предупреждение, оповещение) и т.д.).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string NotificationType { get; set; }

        // Флаг прочтения уведомления.
        // По умолчанию false (не прочитано).
        public bool IsRead { get; set; } = false;

        // Дата и время создания уведомления.
        // Автоматически устанавливается при создании.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Дата и время прочтения уведомления.
        // Nullable, так как уведомление может быть не прочитано.
        public DateTime? ReadAt { get; set; }

        // Внешний ключ на таблицу Devices (nullable).
        // Устройство, связанное с уведомлением.
        public Guid? RelatedDeviceId { get; set; }

        // Внешний ключ на таблицу Scenarios (nullable).
        // Сценарий, связанный с уведомлением.
        public Guid? RelatedScenarioId { get; set; }

        // Навигационные свойства:

        // Ссылка на пользователя, которому адресовано уведомление.
        public virtual User User { get; set; }

        // Ссылка на связанное устройство (nullable).
        public virtual Device RelatedDevice { get; set; }

        // Ссылка на связанный сценарий (nullable).
        public virtual Scenario RelatedScenario { get; set; }
    }
}
