using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class RelaysCollection : ConfigurationElementCollection
{
    protected override ConfigurationElement CreateNewElement() =>
        new RelayElement();

    protected override object GetElementKey(ConfigurationElement element) =>
        ((RelayElement)element).Index;
}