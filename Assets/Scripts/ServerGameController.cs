

// ServerGameController.cs (server project)
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using System.Security.Cryptography;

public class ServerGameController : MonoBehaviour
{
    [Header("Graph settings")]
    public int numNodes = 30;
    public float fieldRadius = 6f;
    public int extraEdgesPerNode = 1;

    [Header("Players")]
    public int maxPlayers = 2;

    [Header("Economy")]
    public float goldTickInterval = 1.0f;
    public int goldPerNodeBase = 1;
    public int directedEdgeCost = 100; // cost to buy directed edge

    // internals
    private bool graphGenerated = false;
    private int nextNodeId = 0;

    private readonly Dictionary<int, Vector2> nodePositions = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, int> nodeScores = new Dictionary<int, int>();
    private readonly Dictionary<int, byte> nodeOwners = new Dictionary<int, byte>(); // 0 neutral,1 player1,2 player2
    private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();

    // directed edges FROM -> TO (owner matters)
    private readonly Dictionary<int, HashSet<int>> directedEdges = new Dictionary<int, HashSet<int>>();

    // connection mapping
    private readonly Dictionary<int, int> connIndex = new Dictionary<int, int>(); // connId -> index
    private readonly Dictionary<int, NetworkConnectionToClient> indexToConn = new Dictionary<int, NetworkConnectionToClient>();

    private int[] playerGold;
    private int[] playerOwnedCount;

    private Coroutine goldCoroutine = null;
    private bool goldStarted = false;

    void Awake()
    {
        playerGold = new int[maxPlayers];
        playerOwnedCount = new int[maxPlayers];
    }

    // called by CustomNetworkManagerServer.OnStartServer()
    public void OnServerStarted()
    {
        NetworkServer.RegisterHandler<ClickMessage>(OnClickMessageReceived, false);
        NetworkServer.RegisterHandler<DirectedEdgePurchaseMessage>(OnDirectedEdgePurchaseReceived, false);
        Debug.Log("[ServerGameController] Handlers registered. Waiting for players...");
        // do not start gold coroutine here — start on first connected player
    }

    public void HandleClientConnected(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Server] Client connected: connId={conn.connectionId}");

        if (connIndex.Count >= maxPlayers)
        {
            Debug.Log("[Server] Room full, disconnecting new client");
            conn.Disconnect();
            return;
        }

        int idx = connIndex.Count;
        connIndex[conn.connectionId] = idx;
        indexToConn[idx] = conn;

        if (!graphGenerated)
        {
            GenerateGraph();
            graphGenerated = true;
            Debug.Log("[Server] Graph generated once.");
        }

        // send full graph to connecting client
        SendGraphToClient(conn);

        // send initial PlayerStats with ownerId and current gold/ownedNodes
        byte ownerId = (byte)(idx == 0 ? 1 : 2);
        SendPlayerStatsToConn(conn, ownerId, playerGold[idx], GetOwnedCountByIndex(idx));

        // start gold coroutine on first player connect
        if (!goldStarted)
        {
            goldStarted = true;
            goldCoroutine = StartCoroutine(GoldTickRoutine());
            Debug.Log("[ServerGameController] Gold coroutine started");
        }

        // notify everyone about stats (optional)
        BroadcastAllPlayerStats();
    }

    public void HandleClientDisconnected(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Server] Client disconnected: connId={conn.connectionId}");
        if (connIndex.TryGetValue(conn.connectionId, out int idx))
        {
            connIndex.Remove(conn.connectionId);
            indexToConn.Remove(idx);
        }
    }

    // ------------------- generation -------------------
    private void GenerateGraph()
    {
        nodePositions.Clear();
        nodeScores.Clear();
        nodeOwners.Clear();
        adjacency.Clear();
        directedEdges.Clear();
        nextNodeId = 0;

        int seed = GenerateRandomSeed();
        Random.InitState(seed);
        Debug.Log($"[Server] GenerateGraph seed = {seed}");

        float scale = fieldRadius / Mathf.Sqrt(numNodes) * 1.5f;
        float jitter = fieldRadius * 0.05f;

        for (int i = 0; i < numNodes; i++)
        {
            Vector2 p = VogelPoint(i, scale) + Random.insideUnitCircle * jitter;
            int id = nextNodeId++;
            nodePositions[id] = p;
            nodeScores[id] = 0;
            nodeOwners[id] = 0;
            adjacency[id] = new List<int>();
            directedEdges[id] = new HashSet<int>();
        }

        // build MST + extra edges (same as before)
        List<(int a, int b, float dist)> allPairs = new List<(int, int, float)>();
        var ids = new List<int>(nodePositions.Keys);
        for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
                allPairs.Add((ids[i], ids[j], Vector2.Distance(nodePositions[ids[i]], nodePositions[ids[j]])));

        // Prim
        HashSet<int> inTree = new HashSet<int>();
        List<(int a, int b)> edges = new List<(int a, int b)>();
        inTree.Add(ids[0]);
        while (inTree.Count < ids.Count)
        {
            float best = float.MaxValue;
            int ba = -1, bb = -1;
            foreach (var e in allPairs)
            {
                if (inTree.Contains(e.a) && !inTree.Contains(e.b))
                {
                    if (e.dist < best) { best = e.dist; ba = e.a; bb = e.b; }
                }
                else if (inTree.Contains(e.b) && !inTree.Contains(e.a))
                {
                    if (e.dist < best) { best = e.dist; ba = e.b; bb = e.a; }
                }
            }
            if (ba != -1 && bb != -1)
            {
                edges.Add((ba, bb));
                inTree.Add(bb);
            }
            else break;
        }

        foreach (int id in ids)
        {
            var neighbors = new List<(int id, float dist)>();
            foreach (int j in ids)
            {
                if (j == id) continue;
                float d = Vector2.Distance(nodePositions[id], nodePositions[j]);
                neighbors.Add((j, d));
            }
            neighbors.Sort((x, y) => x.dist.CompareTo(y.dist));
            int added = 0;
            foreach (var n in neighbors)
            {
                if (added >= extraEdgesPerNode) break;
                if (!EdgeExists(edges, id, n.id))
                {
                    edges.Add((id, n.id));
                    added++;
                }
            }
        }

        foreach (var e in edges)
        {
            if (!adjacency[e.a].Contains(e.b)) adjacency[e.a].Add(e.b);
            if (!adjacency[e.b].Contains(e.a)) adjacency[e.b].Add(e.a);
        }

        // pick seeds randomly and set owners
        int seedA = ids[Random.Range(0, ids.Count)];
        int seedB = seedA;
        float minSeedDist = fieldRadius * 0.6f;
        int attempts = 0;
        while (attempts < 200)
        {
            int candidate = ids[Random.Range(0, ids.Count)];
            if (Vector2.Distance(nodePositions[seedA], nodePositions[candidate]) >= minSeedDist && candidate != seedA)
            {
                seedB = candidate; break;
            }
            attempts++;
        }
        if (seedB == seedA)
        {
            float maxd = -1f;
            foreach (int j in ids) { float d = Vector2.Distance(nodePositions[seedA], nodePositions[j]); if (d > maxd) { maxd = d; seedB = j; } }
        }

        nodeScores[seedA] = 10; nodeOwners[seedA] = 1;
        nodeScores[seedB] = -10; nodeOwners[seedB] = 2;

        Debug.Log($"[Server] Graph generated seeds: {seedA} (1), {seedB} (2). Nodes: {nodePositions.Count} Edges: {edges.Count}");
    }

    private int GenerateRandomSeed()
    {
        byte[] data = new byte[4];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(data);
        int seed = System.BitConverter.ToInt32(data, 0);
        if (seed == int.MinValue) seed = -seed;
        if (seed < 0) seed = -seed;
        return seed;
    }

    // ---------------- networking: send graph / stuff ----------------
    private void SendGraphToClient(NetworkConnectionToClient conn)
    {
        int n = nodePositions.Count;
        int[] nodeIds = new int[n];
        Vector2[] positions = new Vector2[n];
        int[] scores = new int[n];
        byte[] owners = new byte[n];

        var ids = new List<int>(nodePositions.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            nodeIds[i] = id;
            positions[i] = nodePositions[id];
            scores[i] = nodeScores[id];
            owners[i] = nodeOwners[id];
        }

        List<int> edgeFrom = new List<int>();
        List<int> edgeTo = new List<int>();
        var seenPairs = new HashSet<(int, int)>();
        foreach (var kvp in adjacency)
        {
            int a = kvp.Key;
            foreach (int b in kvp.Value)
            {
                var pair = (Mathf.Min(a, b), Mathf.Max(a, b));
                if (!seenPairs.Contains(pair))
                {
                    seenPairs.Add(pair);
                    edgeFrom.Add(pair.Item1);
                    edgeTo.Add(pair.Item2);
                }
            }
        }

        GraphMessage gm = new GraphMessage
        {
            nodeIds = nodeIds,
            positions = positions,
            scores = scores,
            owners = owners,
            edgeFrom = edgeFrom.ToArray(),
            edgeTo = edgeTo.ToArray()
        };

        conn.Send(gm);
        Debug.Log($"[Server] Sent GraphMessage to connId={conn.connectionId} nodes={n} edges={edgeFrom.Count}");

        // Also send existing directed edges to this client
        foreach (var kv in directedEdges)
        {
            int a = kv.Key;
            foreach (int b in kv.Value)
            {
                // determine owner color (owner of source node at the moment)
                byte owner = nodeOwners.ContainsKey(a) ? nodeOwners[a] : (byte)0;
                DirectedEdgeMessage dem = new DirectedEdgeMessage { fromNodeId = a, toNodeId = b, owner = owner };
                conn.Send(dem);
            }
        }
    }

    private void BroadcastDirectedEdge(int from, int to, byte owner)
    {
        DirectedEdgeMessage dem = new DirectedEdgeMessage { fromNodeId = from, toNodeId = to, owner = owner };
        NetworkServer.SendToAll(dem);
        Debug.Log($"[Server] Broadcast directed edge {from} -> {to} owner {owner}");
    }

    private void BroadcastAllPlayerStats()
    {
        for (int idx = 0; idx < maxPlayers; idx++)
        {
            if (indexToConn.TryGetValue(idx, out var conn))
            {
                byte ownerId = (byte)(idx == 0 ? 1 : 2);
                SendPlayerStatsToConn(conn, ownerId, playerGold[idx], GetOwnedCountByIndex(idx));
            }
        }
    }

    private void SendPlayerStatsToConn(NetworkConnectionToClient conn, byte ownerId, int gold, int ownedNodes)
    {
        PlayerStatsMessage m = new PlayerStatsMessage { ownerId = ownerId, gold = gold, ownedNodes = ownedNodes };
        conn.Send(m);
    }

    // ---------------- clicks and purchase handling ----------------
    private void OnClickMessageReceived(NetworkConnectionToClient conn, ClickMessage msg)
    {
        int nodeId = msg.nodeId;
        if (!nodePositions.ContainsKey(nodeId))
        {
            Debug.LogWarning($"[Server] Click: unknown nodeId {nodeId} from conn {conn.connectionId}");
            return;
        }
        if (!connIndex.TryGetValue(conn.connectionId, out int pIndex))
        {
            Debug.LogWarning($"[Server] Click from unknown connection {conn.connectionId}");
            return;
        }
        byte playerOwnerId = (byte)((pIndex == 0) ? 1 : 2);

        bool allowed = false;
        // allowed if player owns this node
        if (nodeOwners.TryGetValue(nodeId, out byte curOwner) && curOwner == playerOwnerId) allowed = true;
        // allowed if any neighbor is owned
        foreach (int nbr in adjacency[nodeId])
            if (nodeOwners.TryGetValue(nbr, out byte o) && o == playerOwnerId) { allowed = true; break; }
        // allowed if there exists directed edge FROM any player's owned node to nodeId (but only if that source is owned by this player)
        foreach (var kv in directedEdges)
        {
            int src = kv.Key;
            if (!nodeOwners.ContainsKey(src)) continue;
            if (nodeOwners[src] != playerOwnerId) continue;
            if (kv.Value.Contains(nodeId)) { allowed = true; break; }
        }

        if (!allowed)
        {
            Debug.Log($"[Server] Click rejected: conn {conn.connectionId} cannot click node {nodeId}");
            return;
        }

        // change score (+/- depending on owner)
        if (playerOwnerId == 1) nodeScores[nodeId] += 1;
        else nodeScores[nodeId] -= 1;

        // recalc owner
        int sc = nodeScores[nodeId];
        byte newOwner = 0;
        if (sc > 0) newOwner = 1;
        else if (sc < 0) newOwner = 2;
        nodeOwners[nodeId] = newOwner;

        // notify all clients
        NodeUpdateMessage um = new NodeUpdateMessage { nodeId = nodeId, score = nodeScores[nodeId], owner = newOwner };
        NetworkServer.SendToAll(um);

        Debug.Log($"[Server] Node {nodeId} updated by conn {conn.connectionId} -> score={nodeScores[nodeId]} owner={newOwner}");

        // update owned counts and notify players
        UpdateOwnedCountsAndNotify();
    }

    // Purchase handler: client sends from/to selection
    private void OnDirectedEdgePurchaseReceived(NetworkConnectionToClient conn, DirectedEdgePurchaseMessage msg)
    {
        if (!connIndex.TryGetValue(conn.connectionId, out int pIndex))
        {
            Debug.LogWarning($"[Server] Purchase from unknown conn {conn.connectionId}");
            conn.Send(new PurchaseResultMessage { success = false, reason = "unknown connection" });
            return;
        }

        byte playerOwnerId = (byte)((pIndex == 0) ? 1 : 2);
        int from = msg.fromNodeId;
        int to = msg.toNodeId;

        // validations
        if (!nodePositions.ContainsKey(from) || !nodePositions.ContainsKey(to))
        {
            conn.Send(new PurchaseResultMessage { success = false, reason = "node not found" });
            return;
        }

        // must own source node
        if (!nodeOwners.ContainsKey(from) || nodeOwners[from] != playerOwnerId)
        {
            conn.Send(new PurchaseResultMessage { success = false, reason = "you must own source node" });
            return;
        }

        // cannot be same node
        if (from == to)
        {
            conn.Send(new PurchaseResultMessage { success = false, reason = "from and to must differ" });
            return;
        }

        // cannot already exist directed edge
        if (directedEdges.TryGetValue(from, out var set) && set.Contains(to))
        {
            conn.Send(new PurchaseResultMessage { success = false, reason = "edge already exists" });
            return;
        }

        // check funds
        if (playerGold[pIndex] < directedEdgeCost)
        {
            conn.Send(new PurchaseResultMessage { success = false, reason = "not enough gold" });
            return;
        }

        // OK: deduct gold, add directed edge, broadcast
        playerGold[pIndex] -= directedEdgeCost;
        directedEdges[from].Add(to);

        // broadcast edge to everyone
        BroadcastDirectedEdge(from, to, playerOwnerId);

        // notify purchaser with success + newGold
        conn.Send(new PurchaseResultMessage { success = true, reason = "ok", newGold = playerGold[pIndex] });

        Debug.Log($"[Server] Player idx {pIndex} (conn {conn.connectionId}) bought directed edge {from}->{to} for {directedEdgeCost} gold, remaining {playerGold[pIndex]}");

        // update owned counts and send stats
        UpdateOwnedCountsAndNotify();
    }

    // ---------------- gold tick ----------------
    IEnumerator GoldTickRoutine()
    {
        // allow one frame
        yield return null;

        while (true)
        {
            yield return new WaitForSeconds(goldTickInterval);

            if (!NetworkServer.active) continue;

            // add gold per owned node
            for (int idx = 0; idx < maxPlayers; idx++) { if (playerGold.Length <= idx) continue; }

            foreach (var kv in nodeOwners)
            {
                int nodeId = kv.Key;
                byte owner = kv.Value;
                if (owner == 0) continue;
                int idx = (owner == 1) ? 0 : 1;
                int sc = nodeScores.ContainsKey(nodeId) ? nodeScores[nodeId] : 0;
                int gain = goldPerNodeBase + Mathf.Abs(sc) / 5;
                playerGold[idx] += Mathf.Clamp(gain, 0, 1000);
            }

            // send updated stats to connected players
            for (int idx = 0; idx < maxPlayers; idx++)
            {
                if (indexToConn.TryGetValue(idx, out var conn))
                {
                    byte ownerId = (byte)(idx == 0 ? 1 : 2);
                    SendPlayerStatsToConn(conn, ownerId, playerGold[idx], GetOwnedCountByIndex(idx));
                }
            }
        }
    }

    private void UpdateOwnedCountsAndNotify()
    {
        for (int i = 0; i < maxPlayers; i++) playerOwnedCount[i] = 0;
        foreach (var kv in nodeOwners)
        {
            if (kv.Value == 0) continue;
            int idx = (kv.Value == 1) ? 0 : 1;
            playerOwnedCount[idx]++;
        }

        for (int idx = 0; idx < maxPlayers; idx++)
        {
            if (indexToConn.TryGetValue(idx, out var conn))
            {
                byte ownerId = (byte)(idx == 0 ? 1 : 2);
                SendPlayerStatsToConn(conn, ownerId, playerGold[idx], playerOwnedCount[idx]);
            }
        }
    }

    private int GetOwnedCountByIndex(int idx)
    {
        int cnt = 0;
        foreach (var kv in nodeOwners) if (kv.Value == (byte)(idx == 0 ? 1 : 2)) cnt++;
        return cnt;
    }

    // ---------------- utilities ----------------
    private Vector2 VogelPoint(int i, float scale)
    {
        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        float theta = i * goldenAngle;
        float r = scale * Mathf.Sqrt(i + 0.5f);
        return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
    }

    private bool EdgeExists(List<(int a, int b)> list, int a, int b)
    {
        foreach (var e in list) if ((e.a == a && e.b == b) || (e.a == b && e.b == a)) return true;
        return false;
    }
}
