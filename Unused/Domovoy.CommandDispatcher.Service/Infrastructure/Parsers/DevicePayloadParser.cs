using System.Text.Json;

namespace Domovoy.CommandDispatcher.Service.Infrastructure.Parsers;

/// <summary>
/// Высокопроизводительный стек-ориентированный парсер полезной нагрузки (Zigbee/MQTT/HTTP payload) без аллокаций в куче (Zero-allocation ref struct).
/// </summary>
public readonly ref struct DevicePayloadParser
{
    private readonly ReadOnlySpan<byte> _utf8Payload;

    public DevicePayloadParser(ReadOnlySpan<byte> utf8Payload)
    {
        _utf8Payload = utf8Payload;
    }

    public bool TryExtractProperty(ReadOnlySpan<byte> propertyName, out Utf8JsonReader reader)
    {
        reader = new Utf8JsonReader(_utf8Payload);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName && reader.ValueTextEquals(propertyName))
            {
                reader.Read();
                return true;
            }
        }
        return false;
    }
}
