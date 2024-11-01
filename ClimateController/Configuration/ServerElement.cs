using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class ServerElement : ConfigurationElement
{
    [ConfigurationProperty("port", IsRequired = true)]
    public int Port =>
        (int)this["port"];
}