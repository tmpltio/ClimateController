using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class LevelsCollection : ConfigurationElementCollection
{
    protected override ConfigurationElement CreateNewElement() =>
        new LevelElement();

    protected override object GetElementKey(ConfigurationElement element) =>
        ((LevelElement)element).Kind;
}