using UnityEngine;
using Mirror;

public class CustomNetworkManagerServer : NetworkManager
{
    [Tooltip("Drag the ServerGameController GameObject here in Inspector")]
    public ServerGameController gameController;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[CustomNetworkManagerServer] Server started");

        if (gameController != null)
        {
            gameController.OnServerStarted();
        }
        else
        {
            Debug.LogWarning("[CustomNetworkManagerServer] gameController is not assigned!");
        }
    }

    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[CustomNetworkManagerServer] OnServerConnect connId={conn.connectionId}");
        if (gameController != null) gameController.HandleClientConnected(conn);
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        Debug.Log($"[CustomNetworkManagerServer] OnServerDisconnect connId={conn.connectionId}");
        if (gameController != null) gameController.HandleClientDisconnected(conn);
        base.OnServerDisconnect(conn);
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log($"[CustomNetworkManagerServer] OnServerAddPlayer ignored for conn {conn.connectionId}");
    }
}
