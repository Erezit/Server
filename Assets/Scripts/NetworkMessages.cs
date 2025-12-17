// // NetworkMessages.cs  (одинаковый в Server и Client проектах)
// using Mirror;
// using UnityEngine;

// public struct GraphMessage : NetworkMessage
// {
//     public int[] nodeIds;        // parallel arrays
//     public Vector2[] positions;
//     public int[] scores;
//     public byte[] owners;        // 0 = neutral, 1 = player1 (red), 2 = player2 (blue)
//     public int[] edgeFrom;       // edges as arrays of pairs
//     public int[] edgeTo;
// }

// public struct SpawnNodeMessage : NetworkMessage
// {
//     public int nodeId;
//     public Vector2 position;
//     public int initialScore;
//     public byte owner;
// }

// public struct ClickMessage : NetworkMessage
// {
//     public int nodeId;
// }

// public struct NodeUpdateMessage : NetworkMessage
// {
//     public int nodeId;
//     public int score;
//     public byte owner; // 0/1/2
// }

// public struct PlayerHudMessage : NetworkMessage
// {
//     public int gold;
//     public int ownedCount;
//     public byte playerOwnerId; // 1 или 2 (чтобы клиент поставил цвет)
// }


using Mirror;
using UnityEngine;

public struct GraphMessage : NetworkMessage
{
    public int[] nodeIds;
    public Vector2[] positions;
    public int[] scores;
    public byte[] owners;        // 0 = neutral, 1 = player1 (red), 2 = player2 (blue)
    public int[] edgeFrom;
    public int[] edgeTo;
}

public struct SpawnNodeMessage : NetworkMessage
{
    public int nodeId;
    public Vector2 position;
    public int initialScore;
    public byte owner;
}

public struct ClickMessage : NetworkMessage
{
    public int nodeId;
}

public struct NodeUpdateMessage : NetworkMessage
{
    public int nodeId;
    public int score;
    public byte owner; // 0/1/2
}

// --- NEW: per-player stats message ---
public struct PlayerStatsMessage : NetworkMessage
{
    public byte ownerId;    // 1 or 2
    public int gold;
    public int ownedNodes;
    public int clickPower;  // сила клика игрока
}

// --- Bonus purchase message ---
public struct BuyBonusMessage : NetworkMessage
{
    public int bonusType;   // 0=IncreaseClickPower, 1=BoostNodeScore, 2=BoostGoldGen, 3=AttackEnemy
    public int targetNodeId; // -1 если бонус не требует цели
}

// --- Bonus purchase response ---
public struct BonusResponseMessage : NetworkMessage
{
    public bool success;
    public string message;
}

// --- Game state message ---
public struct GameStateMessage : NetworkMessage
{
    public byte gameState;   // 0=Playing, 1=Player1Won, 2=Player2Won
    public byte winnerOwnerId; // 1 or 2
}
