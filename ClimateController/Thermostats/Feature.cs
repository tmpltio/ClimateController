namespace ClimateController.Thermostats;

[Flags]
internal enum Feature
{
    Heat = 1,
    TemperatureSensor = 16,
    HumiditySensor = 32
}