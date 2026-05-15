using Unity.NetCode;
using UnityEngine;

public class CoopNetcodeBootstrap : ClientServerBootstrap
{
    public override bool Initialize(string defaultWorldName)
    {
        Application.runInBackground = true;
        AutoConnectPort = 0;
        CreateLocalWorld(defaultWorldName);
        return true;
    }
}
