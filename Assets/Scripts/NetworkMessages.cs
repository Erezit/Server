using Mirror;
using UnityEngine;

public struct GraphMessage : NetworkMessage
{
    public int[] nodeIds;
    public Vector2[] positions;
    public int[] scores;
    public byte[] owners;
    public int[] edgeFrom;
    public int[] edgeTo;
}

public struct ClickMessage : NetworkMessage
{
    public int nodeId;
}

public struct NodeUpdateMessage : NetworkMessage
{
    public int nodeId;
    public int score;
    public byte owner;
}

public struct PlayerStatsMessage : NetworkMessage
{
    public byte ownerId;
    public int gold;
    public int ownedNodes;
}

public struct DirectedEdgePurchaseMessage : NetworkMessage
{
    public int fromNodeId;
    public int toNodeId;
}

public struct DirectedEdgeMessage : NetworkMessage
{
    public int fromNodeId;
    public int toNodeId;
    public byte owner;
}

public struct PurchaseResultMessage : NetworkMessage
{
    public bool success;
    public string reason;
    public int newGold;
}
