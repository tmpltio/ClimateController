using System.ComponentModel;
using System.Configuration;
using System.Net;

namespace ClimateController.Configuration;

internal sealed class DeviceElement : ConfigurationElement
{
    [TypeConverter(typeof(IPAddressConverter))]
    [ConfigurationProperty("address", IsRequired = true)]
    public IPAddress Address =>
        (IPAddress)this["address"];

    [ConfigurationProperty("port", IsRequired = true)]
    public int Port =>
        (int)this["port"];
}