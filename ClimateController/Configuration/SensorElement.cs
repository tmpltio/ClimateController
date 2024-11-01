using System.ComponentModel;
using System.Configuration;
using System.Net.NetworkInformation;

namespace ClimateController.Configuration;

internal sealed class SensorElement : ConfigurationElement
{
    [TypeConverter(typeof(PhysicalAddressConverter))]
    [ConfigurationProperty("address", IsRequired = true)]
    public PhysicalAddress Address =>
        (PhysicalAddress)this["address"];
}