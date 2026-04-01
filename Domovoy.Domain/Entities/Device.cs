using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Устройство умного дома
    /// </summary>
    public class Device
    {
        // Уникальный идентификатор устройства в базе данных.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Понятное имя устройства для пользователя.
        // Обязательное поле, максимальная длина 100 символов.
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }


        // Уникальный ID устройства в сети (MAC-адрес, серийный номер и т.д.).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string DeviceId { get; set; } // Уникальный ID устройства в сети

        // Внешний ключ на таблицу DeviceTypes.
        // Ссылка на тип устройства.
        public Guid DeviceTypeId { get; set; }

        // Внешний ключ на таблицу Rooms (nullable).
        // Устройство может находиться в комнате или быть без привязки.
        public Guid? RoomId { get; set; }

        // Внешний ключ на таблицу Users.
        // Владелец устройства.
        public Guid UserId { get; set; }

        // Тип подключения (WiFi, Bluetooth, порт MQTT и т.д.).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string ConnectionType { get; set; } // WiFi, Zigbee, Bluetooth, MQTT

        // IP-адрес устройства в сети (если применимо).
        // Максимальная длина 100 символов.
        [MaxLength(100)]
        public string IPAddress { get; set; }

        // Порт для подключения к устройству
        // Значение по умолчанию 1883 (Default MQTT port (стандартный порт MQTT)).
        public int Port { get; set; } = 1883;

        // Флаг онлайн-статуса устройства.
        // По умолчанию false (не в сети).
        public bool IsOnline { get; set; } = false;

        // Дата и время последней активности устройства.
        // Nullable, так как устройство может быть отключено.
        public DateTime? LastSeen { get; set; }

        // Дата и время добавления устройства в систему.
        // Автоматически устанавливается при создании.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Дата и время последнего обслуживания.
        // Nullable, так как обслуживание может не проводиться.
        public DateTime? LastMaintenance { get; set; }


        // Навигационные свойства для связей с другими сущностями:
        
        // Ссылка на тип устройства.
        public virtual string DeviceType { get; set; }

        // Ссылка на комнату (nullable).
        public virtual Room Room { get; set; }

        // Ссылка на владельца (пользователя).
        public virtual User User { get; set; }

        // Датчики, принадлежащие устройству (один-ко-многим).
        public virtual ICollection<Sensor> Sensors { get; set; } = new List<Sensor>();

        // Актуаторы, принадлежащие устройству (один-ко-многим).
        public virtual ICollection<Actuator> Actuators { get; set; } = new List<Actuator>();

        // Логи событий устройства (один-ко-многим).
        public virtual ICollection<DeviceLog> DeviceLogs { get; set; } = new List<DeviceLog>();
    }
}
