using System.Configuration;
using Microsoft.Extensions.Logging;

namespace ClimateController.Configuration;

internal sealed class LogElement : ConfigurationElement
{
    [ConfigurationProperty("level", IsRequired = true)]
    public LogLevel Level =>
        (LogLevel)this["level"];
}