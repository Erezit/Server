// // // CustomNetworkManagerServer.cs
// // using UnityEngine;
// // using Mirror;

// // public class CustomNetworkManagerServer : NetworkManager
// // {
// //     public ServerGameController gameController; // drag the ServerGameController GameObject here in Inspector

// //     public override void OnStartServer()
// //     {
// //         base.OnStartServer();
// //         Debug.Log("[CustomNetworkManagerServer] Server started");
// //         if (gameController != null)
// //             gameController.OnServerStarted(); // регистрация хендлеров
// //     }

// //     public override void OnServerConnect(NetworkConnectionToClient conn)
// //     {
// //         base.OnServerConnect(conn);
// //         Debug.Log($"[CustomNetworkManagerServer] OnServerConnect connId={conn.connectionId}");
// //         if (gameController != null)
// //             gameController.HandleClientConnected(conn);
// //     }

// //     public override void OnServerDisconnect(NetworkConnectionToClient conn)
// //     {
// //         Debug.Log($"[CustomNetworkManagerServer] OnServerDisconnect connId={conn.connectionId}");
// //         if (gameController != null)
// //             gameController.HandleClientDisconnected(conn);
// //         base.OnServerDisconnect(conn);
// //     }

// //     public override void OnServerAddPlayer(NetworkConnectionToClient conn)
// //     {
// //         // disable automatic player spawn
// //         Debug.Log($"[CustomNetworkManagerServer] OnServerAddPlayer ignored for conn {conn.connectionId}");
// //     }
// // }


// using UnityEngine;
// using Mirror;
// using System.Collections;

// public class CustomNetworkManagerServer : NetworkManager
// {
//     [Tooltip("Drag ServerGame (scene object) here (the GameObject that has ServerGameController)")]
//     public ServerGameController gameController; // перетащи сценный объект ServerGame сюда

//     public override void OnStartServer()
//     {
//         base.OnStartServer();
//         Debug.Log("[CustomNetworkManagerServer] Server started");

//         if (gameController == null)
//         {
//             Debug.LogError("[CustomNetworkManagerServer] gameController IS NULL! Assign ServerGame scene object in Inspector.");
//             return;
//         }

//         Debug.Log($"[CustomNetworkManagerServer] gameController.name={gameController.gameObject.name}, activeSelf={gameController.gameObject.activeSelf}, activeInHierarchy={gameController.gameObject.activeInHierarchy}");

//         // регистрация хендлеров и подготовка игрового контроллера (без запуска корутин на этом объекте)
//         gameController.OnServerStarted();

//         // безопасно запускаем корутину начисления золота *на NetworkManager*, который гарантированно активен
//         // (корутина выполняет серверную логику внутри gameController)
//         StartCoroutine(gameController.GoldTickCoroutine());
//         Debug.Log("[CustomNetworkManagerServer] Started GoldTickCoroutine on NetworkManager.");
//     }

//     public override void OnServerConnect(NetworkConnectionToClient conn)
//     {
//         base.OnServerConnect(conn);
//         Debug.Log($"[CustomNetworkManagerServer] OnServerConnect connId={conn.connectionId}");

//         if (gameController != null)
//         {
//             gameController.HandleClientConnected(conn);
//         }
//         else
//         {
//             Debug.LogWarning("[CustomNetworkManagerServer] gameController == null on OnServerConnect");
//         }
//     }

//     public override void OnServerDisconnect(NetworkConnectionToClient conn)
//     {
//         Debug.Log($"[CustomNetworkManagerServer] OnServerDisconnect connId={conn.connectionId}");

//         if (gameController != null)
//         {
//             gameController.HandleClientDisconnected(conn);
//         }

//         base.OnServerDisconnect(conn);
//     }

//     // отключаем автоматический спаун player prefab
//     public override void OnServerAddPlayer(NetworkConnectionToClient conn)
//     {
//         Debug.Log($"[CustomNetworkManagerServer] OnServerAddPlayer ignored for conn {conn.connectionId}");
//         // намеренно ничего не делаем — сервер не спавнит Networked player prefab
//     }
// }


// CustomNetworkManagerServer.cs
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

    // Disable automatic player spawn (server won't instantiate player prefab)
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log($"[CustomNetworkManagerServer] OnServerAddPlayer ignored for conn {conn.connectionId}");
    }
}
