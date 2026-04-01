using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Лог событий устройства
    /// </summary>
    public class DeviceLog
    {
        // Уникальный идентификатор записи лога.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Внешний ключ на таблицу Devices.
        // Устройство, к которому относится лог.
        public Guid DeviceId { get; set; }

        // Уровень лога (Info, Warning, Error (Информация, Предупреждение, Ошибка) и т.д.).
        // Обязательное поле, максимальная длина 50 символов.
        [Required]
        [MaxLength(50)]
        public string LogLevel { get; set; }

        // Тип события (Connection, StateChange, Error (Соединение, изменение состояния, ошибка) и т.д.).
        // Обязательное поле, максимальная длина 200 символов.
        [Required]
        [MaxLength(200)]
        public string EventType { get; set; }

        // Сообщение лога.
        // Максимальная длина 1000 символов.
        [MaxLength(1000)]
        public string Message { get; set; }

        // Детали события в формате JSON. Может содержать стектрейс, дополнительные параметры.
        // Максимальная длина 2000 символов.
        [MaxLength(2000)]
        public string Details { get; set; }

        // Дата и время события.
        // Автоматически устанавливается в текущее UTC время.
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;


        // Навигационное свойство:

        // Ссылка на устройство, к которому относится лог.
        public virtual Device Device { get; set; }
    }
}
