using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Показания датчика (временные ряды)
    /// </summary>
    public class SensorReading
    {
        // Уникальный идентификатор записи показаний.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Внешний ключ на таблицу Sensors.
        // Датчик, к которому относятся показания.
        public Guid SensorId { get; set; }

        // Значение показания (температура, влажность и т.д.).
        public decimal Value { get; set; }

        // Единица измерения (может отличаться от единицы датчика).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string Unit { get; set; }

        // Дата и время снятия показания.
        // Автоматически устанавливается в текущее UTC время.
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Дополнительные метаданные в формате JSON (JSON с дополнительными данными).
        // Может содержать калибровочные данные, точность и т.д.
        // Максимальная длина 500 символов.
        [MaxLength(500)]
        public string Metadata { get; set; }


        // Навигационное свойство:

        // Ссылка на датчик, к которому относятся показания.
        public virtual Sensor Sensor { get; set; }
    }
}
