using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Датчик (сенсор) для сбора данных
    /// </summary>
    public class Sensor
    {
        // Уникальный идентификатор датчика.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Имя датчика для идентификации.
        // Обязательное поле, максимальная длина 100 символов.
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        // Внешний ключ на таблицу Devices.
        // Устройство, к которому принадлежит датчик.
        public Guid DeviceId { get; set; }

        // Тип датчика (Temperature, Humidity, Motion, Light (Температура, влажность, движение, свет) и т.д.).
        // Обязательное поле, максимальная длина 50 символов.
        [Required]
        [MaxLength(50)]
        public string SensorType { get; set; }

        // Единица измерения показаний (°C, %, lux).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string Unit { get; set; }

        // Минимальное значение, которое может измерять датчик.
        public decimal MinValue { get; set; }

        // Максимальное значение, которое может измерять датчик.
        public decimal MaxValue { get; set; }

        // Текущее значение датчика (nullable).
        // Может быть null, если датчик не активен или нет данных.
        public decimal? CurrentValue { get; set; }

        // Интервал считывания показаний в секундах.
        // Значение по умолчанию 60 секунд (1 минута).
        public int ReadingInterval { get; set; } = 60;

        // Флаг активности датчика.
        // По умолчанию true (активен).
        public bool IsActive { get; set; } = true;

        // Навигационные свойства:

        // Ссылка на устройство, к которому принадлежит датчик.
        public virtual Device Device { get; set; }

        // История показаний датчика (один-ко-многим)
        public virtual ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    }
}
