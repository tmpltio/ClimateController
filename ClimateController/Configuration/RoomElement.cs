using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class RoomElement : ConfigurationElement
{
    [ConfigurationProperty("name", IsRequired = true)]
    public string Name =>
        (string)this["name"];

    [ConfigurationProperty("level", IsRequired = true)]
    public string Level =>
        (string)this["level"];

    [ConfigurationProperty("sensor")]
    public SensorElement Sensor =>
        (SensorElement)this["sensor"];

    [ConfigurationProperty("", IsDefaultCollection = true)]
    [ConfigurationCollection(typeof(RelaysCollection), AddItemName = "loop")]
    public RelaysCollection Loops =>
        (RelaysCollection)this[string.Empty];
}