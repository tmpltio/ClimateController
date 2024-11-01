using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class RoomsCollection : ConfigurationElementCollection
{
    protected override ConfigurationElement CreateNewElement() =>
        new RoomElement();

    protected override object GetElementKey(ConfigurationElement element) =>
        ((RoomElement)element).Name;
}