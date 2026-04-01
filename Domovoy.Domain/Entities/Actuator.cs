using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Исполнительное устройство (актуатор)
    /// </summary>
    public class Actuator
    {
        // Уникальный идентификатор актуатора.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Имя актуатора для идентификации.
        // Обязательное поле, максимальная длина 100 символов.
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        // Внешний ключ на таблицу Devices.
        // Устройство, к которому принадлежит актуатор.
        public Guid DeviceId { get; set; }

        // Тип актуатора (Switch, Dimmer, Valve, Lock (Выключатель, диммер, клапан, замок) и т.д.).
        // Обязательное поле, максимальная длина 50 символов.
        [Required]
        [MaxLength(50)]
        public string ActuatorType { get; set; }

        // Текущее состояние актуатора (On, Off, 50%, Locked (Вкл., Выкл., 50%, Заблокировано) и т.д.).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string CurrentState { get; set; }

        // Флаг активности актуатора.
        // По умолчанию true (активен).
        public bool IsActive { get; set; } = true;

        // Навигационные свойства:

        // Ссылка на устройство, к которому принадлежит актуатор.
        public virtual Device Device { get; set; }
    }
}
