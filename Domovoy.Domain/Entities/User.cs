using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domovoy.Domain.Entities
{
    /// <summary>
    /// Пользователь системы умного дома
    /// </summary>
    public class User
    {
        // Уникальный идентификатор пользователя (первичный ключ).
        // Использует Guid для глобальной уникальности. Автоматически инициализируется новым Guid при создании объекта.
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // Имя пользователя для входа в систему.
        // Обязательное поле, максимальная длина 100 символов.
        [Required]
        [MaxLength(100)]
        public string Username { get; set; }

        // Электронная почта пользователя.
        // Обязательное поле, валидируется как email адрес, максимальная длина 200 символов.
        [Required]
        [EmailAddress]
        [MaxLength(200)]
        public string Email { get; set; }

        // Хэш пароля пользователя (хранится в зашифрованном виде).
        // Обязательное поле для безопасности.
        [Required]
        public string PasswordHash { get; set; }

        // Имя пользователя (необязательное поле).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string FirstName { get; set; }

        // Фамилия пользователя (необязательное поле).
        // Максимальная длина 50 символов.
        [MaxLength(50)]
        public string LastName { get; set; }

        // Дата и время создания учетной записи.
        // Автоматически устанавливается текущее UTC время при создании.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Дата и время последнего входа в систему.
        // ? Nullable, так как пользователь может еще не входить.
        public DateTime? LastLoginAt { get; set; }

        // Флаг активности учетной записи.
        // По умолчанию true (активна), можно деактивировать.
        public bool IsActive { get; set; } = true;

        public virtual Role Role { get; set; }

        // Устройства, принадлежащие пользователю (один-ко-многим). Пользователь может иметь множество устройств.
        public virtual ICollection<Device> Devices { get; set; } = [];

        // Сценарии, созданные пользователем (один-ко-многим). Пользователь может создавать сценарии автоматизации.
        public virtual ICollection<Scenario> Scenarios { get; set; } = [];

        // Уведомления, адресованные пользователю (один-ко-многим). Система может отправлять уведомления пользователю.
        public virtual ICollection<Notification> Notifications { get; set; } = [];
    }
}
