using System.ComponentModel;
using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class ControlElement : ConfigurationElement
{
    [TypeConverter(typeof(TimeSpanConverter))]
    [ConfigurationProperty("tick", IsRequired = true)]
    public TimeSpan Tick =>
        (TimeSpan)this["tick"];
}