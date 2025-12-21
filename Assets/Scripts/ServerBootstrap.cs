using UnityEngine;
using Mirror;

public class ServerBootstrap : MonoBehaviour
{
    void Start()
    {
        var nm = GetComponent<NetworkManager>() ?? FindObjectOfType<NetworkManager>();
        if (nm == null)
        {
            Debug.LogError("[ServerBootstrap] NetworkManager not found!");
            return;
        }
        nm.StartServer();
        Debug.Log("[ServerBootstrap] StartServer called");
    }
}
