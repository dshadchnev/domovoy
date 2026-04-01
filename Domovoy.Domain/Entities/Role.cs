using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Роль пользователя в системе
    /// </summary>
    public class Role
    {
        // Уникальный идентификатор роли (первичный ключ).
        // Автоматически инициализируется новым Guid.
        public Guid Id { get; set; } = Guid.NewGuid();

        // Название роли (Admin, User, Guest и т.д.).
        // Обязательное поле, максимальная длина 50 символов.
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } // Admin, User, Guest

        // Описание роли (необязательное поле).
        // Максимальная длина 200 символов.
        [MaxLength(200)]
        public string Description { get; set; }
    }
}
