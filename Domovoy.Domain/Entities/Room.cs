using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Комната/помещение в доме
    /// </summary>
    public class Room
    {
        // Уникальный идентификатор комнаты.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Название комнаты (Гостиная, Спальня, Кухня).
        // Обязательное поле, максимальная длина 100 символов.
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } // Гостиная, Спальня, Кухня

        // Тип комнаты (LivingRoom, Bedroom, Kitchen, Bathroom (Гостиная, спальня, кухня, ванная комната)).
        // Максимальная длина 20 символов.
        [MaxLength(20)]
        public string RoomType { get; set; } 

        // Этаж, на котором находится комната.
        // Значение по умолчанию - 1 (первый этаж).
        public int Floor { get; set; } = 1;

        // Описание комнаты (необязательное поле).
        // Максимальная длина 500 символов.
        [MaxLength(500)]
        public string Description { get; set; }


        // Навигационное свойство для связи один-ко-многим с устройствами:

        // В одной комнате может находиться несколько устройств.
        public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
    }
}
