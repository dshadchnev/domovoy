```mermaid
erDiagram
DeviceCategory ||--|{ Sensor : ""
SensorType ||--|{ Sensor : ""
DevicePowerSupplyType ||--|{ Sensor : ""
    
    DeviceCategory {
        int id "🔑"
        string name
        string description
    }
    
    DevicePowerSupplyType {
        int id "🔑"
        string name
        string description
    }

    SensorType {
        int id "🔑"
        string name
        string description
    }

    Sensor{
        int id "🔑"
        int idSensorType "🔗"
        int idDeviceCategory "🔗"
        int idDevicePowerSupplyType "🔗"
        boolean isEndDevice
        json settings
    }```