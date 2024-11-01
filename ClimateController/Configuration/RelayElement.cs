using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class RelayElement : ConfigurationElement
{
    [ConfigurationProperty("index", IsRequired = true)]
    public ushort Index =>
        (ushort)this["index"];
}