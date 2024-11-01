using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class LevelElement : ConfigurationElement
{
    [ConfigurationProperty("kind", IsRequired = true)]
    public string Kind =>
        (string)this["kind"];

    [ConfigurationProperty("manifold", IsRequired = true)]
    public DeviceElement Manifold =>
        (DeviceElement)this["manifold"];

    [ConfigurationProperty("pump", IsRequired = true)]
    public RelayElement Pump =>
        (RelayElement)this["pump"];
}