# 🔐 Микросервисная архитектура авторизации Domovoy

## 📋 Обзор

Архитектура авторизации разработана для масштабируемости и удобства, с фокусом на:
- ✅ Четкие API контракты
- ✅ Аудит логирование всех операций безопасности
- ✅ Управление токенами (создание, обновление, отзыв)
- ✅ Раздельная аутентификация пользователей и устройств
- ✅ Масштабируемая архитектура с горизонтальным масштабированием

## 🏗️ Архитектура

```
┌─────────────────────────────────────────────────────────┐
│                   Clients & Devices                      │
└────────────────────┬────────────────────────────────────┘
                     │
         ┌───────────┴───────────┐
         │                       │
    ┌────▼────┐          ┌──────▼───────┐
    │   API   │          │  Device IoT  │
    │ Gateway │          │   Endpoints  │
    └────┬────┘          └──────────────┘
         │                       │
         │  JWT                  │
         │  Validation           │
         └───────────┬───────────┘
                     │
         ┌───────────▼──────────────┐
         │   Auth Service           │
         │  ─────────────────      │
         │  • User Management      │
         │  • Device Management    │
         │  • Token Generation     │
         │  • Audit Logging        │
         └───────────┬──────────────┘
                     │
        ┌────────────┼────────────┐
        │            │            │
   ┌────▼───┐  ┌────▼────┐  ┌───▼──────┐
   │PostgreSQL  │ RabbitMQ │  │ Redis    │
   │(Auth DB)   │ (Events) │  │ (Cache)  │
   └──────────┘  └─────────┘  └──────────┘
```

## 📝 API Endpoints

### 👤 User Authentication (`/api/auth`)

#### 1. Регистрация пользователя
```http
POST /api/auth/register
Content-Type: application/json

{
  "username": "john_doe",
  "email": "john@example.com",
  "password": "SecurePassword123!",
  "firstName": "John",
  "lastName": "Doe"
}

Response (201):
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "john_doe",
  "email": "john@example.com",
  "firstName": "John",
  "lastName": "Doe",
  "isActive": true,
  "createdAt": "2026-04-19T10:30:00Z"
}
```

#### 2. Вход в систему
```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "john_doe",
  "password": "SecurePassword123!"
}

Response (200):
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "refresh_token_base64_encoded",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
```

#### 3. Обновление Access Token
```http
POST /api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "refresh_token_base64_encoded"
}

Response (200):
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "new_refresh_token_base64_encoded",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
```

#### 4. Выход из системы
```http
POST /api/auth/logout
Authorization: Bearer {accessToken}

Response (204): No Content
```

### 🔧 Device Management (`/api/devices`)

#### 1. Регистрация устройства
```http
POST /api/devices/register
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "networkDeviceId": "device_001",
  "roomId": "550e8400-e29b-41d4-a716-446655440001"
}

Response (201):
{
  "networkDeviceId": "device_001",
  "secret": "device_secret_base64_encoded",
  "message": "Store the secret securely. It won't be shown again."
}
```

#### 2. Отзыв устройства
```http
POST /api/devices/{deviceId}/revoke
Authorization: Bearer {accessToken}

Response (204): No Content
```

#### 3. Ротация секрета устройства
```http
POST /api/devices/{deviceId}/rotate-secret
Authorization: Bearer {accessToken}

Response (200):
{
  "message": "Secret rotated successfully"
}
```

### 🔌 Device Authentication (`/api/device-auth`)

#### 1. Аутентификация устройства
```http
POST /api/device-auth/authenticate
Content-Type: application/json

{
  "networkDeviceId": "device_001",
  "secret": "device_secret_base64_encoded"
}

Response (200):
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresIn": 86400,
  "tokenType": "Bearer"
}
```

## 🔑 JWT Claims

### User Token
```json
{
  "NameIdentifier": "550e8400-e29b-41d4-a716-446655440000",
  "Name": "john_doe",
  "Email": "john@example.com",
  "FirstName": "John",
  "LastName": "Doe"
}
```

### Device Token
```json
{
  "DeviceId": "device_001",
  "OwnerId": "550e8400-e29b-41d4-a716-446655440000",
  "RoomId": "550e8400-e29b-41d4-a716-446655440001",
  "Type": "Device"
}
```

## 📊 Аудит логирование

Все операции безопасности логируются в БД и Redis:

### Действия (Actions)
- `USER_REGISTER` - Регистрация пользователя
- `USER_LOGIN` - Вход в систему
- `USER_LOGOUT` - Выход из системы (через событие)
- `TOKEN_REFRESH` - Обновление токена
- `DEVICE_REGISTER` - Регистрация устройства
- `DEVICE_AUTH` - Аутентификация устройства
- `DEVICE_REVOKE` - Отзыв устройства
- `DEVICE_SECRET_ROTATE` - Ротация секрета

### Результаты (Results)
- `Success` - Операция успешна
- `Failure` - Операция не удалась

### Сохранение логов
- **PostgreSQL**: долгосрочное хранилище (90 дней)
- **Redis**: краткосрочный кэш для быстрого доступа
- Автоматическая очистка старых логов (> 90 дней)

## 🔄 События (Message Bus Events)

События публикуются в RabbitMQ для синхронизации между микросервисами:

```csharp
// Публикуемые события
public record UserRegisteredEvent(Guid UserId, string Email, string Role);
public record UserLoggedInEvent(Guid UserId, string IpAddress, DateTime Timestamp);
public record UserLoggedOutEvent(Guid UserId, DateTime Timestamp);
public record TokenRefreshedEvent(Guid UserId, DateTime Timestamp);
public record DeviceLinkedEvent(string NetworkDeviceId, Guid OwnerId, Guid? RoomId);
public record DeviceAuthenticatedEvent(string DeviceId, Guid OwnerId, DateTime Timestamp);
public record DeviceRevokedEvent(string DeviceId, Guid OwnerId, DateTime Timestamp);
public record DeviceSecretRotatedEvent(string NetworkDeviceId);
```

## 🏃 Quick Start

### 1. Запуск инфраструктуры
```bash
cd infra
docker-compose up -d
```

### 2. Миграция БД
```bash
cd Domovoy.Auth.Service
dotnet ef database update
```

### 3. Запуск Auth Service
```bash
cd Domovoy.Auth.Service
dotnet run
# Доступно на: https://localhost:5002
```

### 4. Запуск API Gateway
```bash
cd Domovoy.ApiGateway
dotnet run
# Доступно на: https://localhost:5000
```

## 🔐 Безопасность

### Управление токенами
- **Access Token**: Короткоживущий (60 минут)
- **Refresh Token**: Долгоживущий (7 дней)
- **Rotation**: Старые токены отзываются при обновлении
- **Revocation**: Все токены отзываются при выходе

### Защита от атак
- Хеширование паролей (BCrypt)
- Хеширование секретов устройств
- Валидация JWT подписи
- Проверка истечения токенов
- Логирование всех попыток входа

### Чувствительные данные
- Секреты устройств показываются только один раз при регистрации
- Пароли никогда не логируются
- Токены обновления не передаются в открытом виде

## 📈 Масштабируемость

### Горизонтальное масштабирование
- Stateless сервисы (можно запускать несколько экземпляров)
- Redis для распределенного кэша
- RabbitMQ для асинхронной коммуникации
- PostgreSQL для надежного хранилища

### Производительность
- Кэширование claims в Redis
- Индексы БД для быстрого поиска
-批обработка очистки старых токенов
- Connection pooling для БД

## 🛠️ Разработка

### Добавление нового события
1. Добавьте запись в `Domovoy.Shared/Events/AuthEvents.cs`
2. Создайте Consumer в `Domovoy.ApiGateway/Consumers/`
3. Зарегистрируйте consumer в `Program.cs`

### Добавление нового контроллера
1. Создайте файл в `Domovoy.Auth.Service/Controllers/`
2. Используйте существующие сервисы
3. Документируйте с Swagger атрибутами

## 📚 Дополнительные ресурсы

- [OpenIddict](https://github.com/openiddict/openiddict-core)
- [JWT.io](https://jwt.io)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
