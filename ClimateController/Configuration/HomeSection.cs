using System.Configuration;

namespace ClimateController.Configuration;

internal sealed class HomeSection : ConfigurationSection
{
    [ConfigurationProperty("log", IsRequired = true)]
    public LogElement Log =>
        (LogElement)this["log"];

    [ConfigurationProperty("server", IsRequired = true)]
    public ServerElement Server =>
        (ServerElement)this["server"];

    [ConfigurationProperty("control", IsRequired = true)]
    public ControlElement Control =>
        (ControlElement)this["control"];

    [ConfigurationProperty("valve", IsRequired = true)]
    public DeviceElement Valve =>
        (DeviceElement)this["valve"];

    [ConfigurationProperty("levels", IsRequired = true)]
    [ConfigurationCollection(typeof(LevelsCollection), AddItemName = "level")]
    public LevelsCollection Levels =>
        (LevelsCollection)this["levels"];

    [ConfigurationProperty("rooms", IsRequired = true)]
    [ConfigurationCollection(typeof(RoomsCollection), AddItemName = "room")]
    public RoomsCollection Rooms =>
        (RoomsCollection)this["rooms"];
}