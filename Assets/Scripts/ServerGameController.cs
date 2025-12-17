// // // // ServerGameController.cs
// // // using System.Collections.Generic;
// // // using UnityEngine;
// // // using Mirror;
// // // using System.Security.Cryptography;

// // // public class ServerGameController : MonoBehaviour
// // // {
// // //     [Header("Graph settings")]
// // //     public int numNodes = 30;
// // //     public float fieldRadius = 6f;
// // //     public float nodeHitRadius = 0.6f; // серверный радиус попадания (в мире)
// // //     public int extraEdgesPerNode = 1; // добавляем несколько дополнительных ребер поверх MST

// // //     [Header("Players")]
// // //     public int maxPlayers = 2;

// // //     // internal
// // //     private bool graphGenerated = false;
// // //     private int nextNodeId = 0;

// // //     // node data
// // //     private readonly Dictionary<int, Vector2> nodePositions = new Dictionary<int, Vector2>();
// // //     private readonly Dictionary<int, int> nodeScores = new Dictionary<int, int>();
// // //     private readonly Dictionary<int, byte> nodeOwners = new Dictionary<int, byte>(); // 0 neutral,1 player1,2 player2

// // //     // adjacency list
// // //     private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();

// // //     // conn -> player index (0..)
// // //     private readonly Dictionary<int, int> connIndex = new Dictionary<int, int>();

// // //     void Awake()
// // //     {
// // //         // ничего здесь не посылаем — SendToAll работает только когда сервер активен
// // //     }

// // //     // вызывается из CustomNetworkManagerServer.OnStartServer
// // //     public void OnServerStarted()
// // //     {
// // //         // регистрируем хендлер кликов (работает, когда сервер активен)
// // //         NetworkServer.RegisterHandler<ClickMessage>(OnClickMessageReceived, false);
// // //         Debug.Log("[ServerGameController] Registered ClickMessage handler");
// // //     }

// // //     private void SendGraphToClient(NetworkConnectionToClient conn)
// // // {
// // //     int n = nodePositions.Count;
// // //     int[] nodeIds = new int[n];
// // //     Vector2[] positions = new Vector2[n];
// // //     int[] scores = new int[n];
// // //     byte[] owners = new byte[n];

// // //     var ids = new List<int>(nodePositions.Keys);
// // //     for (int i = 0; i < ids.Count; i++)
// // //     {
// // //         int id = ids[i];
// // //         nodeIds[i] = id;
// // //         positions[i] = nodePositions[id];
// // //         scores[i] = nodeScores[id];
// // //         owners[i] = nodeOwners[id];
// // //     }

// // //     // edges -> two arrays
// // //     List<int> edgeFrom = new List<int>();
// // //     List<int> edgeTo = new List<int>();
// // //     var seenPairs = new HashSet<(int, int)>();
// // //     foreach (var kvp in adjacency)
// // //     {
// // //         int a = kvp.Key;
// // //         foreach (int b in kvp.Value)
// // //         {
// // //             var pair = (Mathf.Min(a, b), Mathf.Max(a, b));
// // //             if (!seenPairs.Contains(pair))
// // //             {
// // //                 seenPairs.Add(pair);
// // //                 edgeFrom.Add(pair.Item1);
// // //                 edgeTo.Add(pair.Item2);
// // //             }
// // //         }
// // //     }

// // //     GraphMessage gm = new GraphMessage
// // //     {
// // //         nodeIds = nodeIds,
// // //         positions = positions,
// // //         scores = scores,
// // //         owners = owners,
// // //         edgeFrom = edgeFrom.ToArray(),
// // //         edgeTo = edgeTo.ToArray()
// // //     };

// // //     conn.Send(gm);
// // //     Debug.Log($"[Server] Sent GraphMessage to connId={conn.connectionId} nodes={n} edges={edgeFrom.Count}");
// // // }


// // //     // вызывается из CustomNetworkManagerServer.OnServerConnect
// // //     public void HandleClientConnected(NetworkConnectionToClient conn)
// // //     {
// // //         Debug.Log($"[Server] Client connected: connId={conn.connectionId}");

// // //         // назначаем индекс игрока
// // //         if (!connIndex.ContainsKey(conn.connectionId))
// // //         {
// // //             int idx = connIndex.Count; // 0,1,...
// // //             connIndex[conn.connectionId] = idx;
// // //         }

// // //         // если граф ещё не сгенерирован — сгенерировать единожды
// // //         if (!graphGenerated)
// // //         {
// // //             GenerateGraph();
// // //             graphGenerated = true;
// // //             Debug.Log("[Server] Graph generated once and locked.");
// // //         }

// // //         // отправляем текущий граф новому клиенту (в одном сообщении)
// // //         SendGraphToClient(conn);
// // //     }

// // //     public void HandleClientDisconnected(NetworkConnectionToClient conn)
// // //     {
// // //         Debug.Log($"[Server] Client disconnected: connId={conn.connectionId}");
// // //         if (connIndex.ContainsKey(conn.connectionId))
// // //             connIndex.Remove(conn.connectionId);
// // //     }

// // //     // -------------------- генерация графа --------------------
// // //     private void GenerateGraph()
// // // {
// // //     nodePositions.Clear();
// // //     nodeScores.Clear();
// // //     nodeOwners.Clear();
// // //     adjacency.Clear();
// // //     nextNodeId = 0;

// // //     // --- 0) Инициализируем UnityEngine.Random неожиданным seed'ом (для неповторимости)
// // //     int seed = GenerateRandomSeed();
// // //     Random.InitState(seed);
// // //     Debug.Log($"[Server] GenerateGraph seed = {seed}"); // можно сохранить/логировать, чтобы воспроизводить

// // //     // --- 1) позиции — Vogel spiral + небольшой джиттер
// // //     float scale = fieldRadius / Mathf.Sqrt(numNodes) * 1.5f;
// // //     float jitterAmount = fieldRadius * 0.05f; // 5% радиуса — можно настроить

// // //     for (int i = 0; i < numNodes; i++)
// // //     {
// // //         Vector2 p = VogelPoint(i, scale);

// // //         // добавим небольшой случайный дрейф (джиттер), чтобы каждый запуск был другой
// // //         p += Random.insideUnitCircle * jitterAmount;

// // //         int id = nextNodeId++;
// // //         nodePositions[id] = p;
// // //         nodeScores[id] = 0;
// // //         nodeOwners[id] = 0;
// // //         adjacency[id] = new List<int>();
// // //     }

// // //     // --- 2) edges — MST (Prim) + дополнительные ближайшие ребра
// // //     List<(int a, int b, float dist)> allPairs = new List<(int, int, float)>();
// // //     var ids = new List<int>(nodePositions.Keys);
// // //     for (int i = 0; i < ids.Count; i++)
// // //     {
// // //         for (int j = i + 1; j < ids.Count; j++)
// // //         {
// // //             float d = Vector2.Distance(nodePositions[ids[i]], nodePositions[ids[j]]);
// // //             allPairs.Add((ids[i], ids[j], d));
// // //         }
// // //     }

// // //     // Prim's MST
// // //     HashSet<int> inTree = new HashSet<int>();
// // //     List<(int a, int b)> edges = new List<(int a, int b)>();
// // //     inTree.Add(ids[0]);
// // //     while (inTree.Count < ids.Count)
// // //     {
// // //         float best = float.MaxValue;
// // //         int ba = -1, bb = -1;
// // //         foreach (var e in allPairs)
// // //         {
// // //             if (inTree.Contains(e.a) && !inTree.Contains(e.b))
// // //             {
// // //                 if (e.dist < best) { best = e.dist; ba = e.a; bb = e.b; }
// // //             }
// // //             else if (inTree.Contains(e.b) && !inTree.Contains(e.a))
// // //             {
// // //                 if (e.dist < best) { best = e.dist; ba = e.b; bb = e.a; }
// // //             }
// // //         }
// // //         if (ba != -1 && bb != -1)
// // //         {
// // //             edges.Add((ba, bb));
// // //             inTree.Add(bb);
// // //         }
// // //         else break;
// // //     }

// // //     // дополнительные ребра: для каждой вершины добавляем up to extraEdgesPerNode ближайших соседей
// // //     foreach (int id in ids)
// // //     {
// // //         var neighbors = new List<(int id, float dist)>();
// // //         foreach (int j in ids)
// // //         {
// // //             if (j == id) continue;
// // //             float d = Vector2.Distance(nodePositions[id], nodePositions[j]);
// // //             neighbors.Add((j, d));
// // //         }
// // //         neighbors.Sort((x, y) => x.dist.CompareTo(y.dist));
// // //         int added = 0;
// // //         foreach (var n in neighbors)
// // //         {
// // //             if (added >= extraEdgesPerNode) break;
// // //             if (!EdgeExists(edges, id, n.id))
// // //             {
// // //                 edges.Add((id, n.id));
// // //                 added++;
// // //             }
// // //         }
// // //     }

// // //     // заполняем adjacency
// // //     foreach (var e in edges)
// // //     {
// // //         if (!adjacency[e.a].Contains(e.b)) adjacency[e.a].Add(e.b);
// // //         if (!adjacency[e.b].Contains(e.a)) adjacency[e.b].Add(e.a);
// // //     }

// // //     // --- 3) выбираем случайные стартовые владельцы (seedA и seedB), но требуем достаточной дистанции
// // //     // Выберем одну случайную точку, вторую — случайную, но отдалённую не менее minSeedDist
// // //     int seedA = ids[Random.Range(0, ids.Count)];
// // //     int seedB = seedA;
// // //     float minSeedDist = fieldRadius * 0.6f; // порог расстояния — настраиваем

// // //     // попробуем выбрать seedB случайно с требованием расстояния; ограничим число попыток
// // //     int attempts = 0;
// // //     while (attempts < 200)
// // //     {
// // //         int candidate = ids[Random.Range(0, ids.Count)];
// // //         if (Vector2.Distance(nodePositions[seedA], nodePositions[candidate]) >= minSeedDist && candidate != seedA)
// // //         {
// // //             seedB = candidate;
// // //             break;
// // //         }
// // //         attempts++;
// // //     }
// // //     // если не нашлось подходящего по рандому — выберем максимально удалённую
// // //     if (seedB == seedA)
// // //     {
// // //         float maxd = -1f;
// // //         foreach (int j in ids)
// // //         {
// // //             float d = Vector2.Distance(nodePositions[seedA], nodePositions[j]);
// // //             if (d > maxd) { maxd = d; seedB = j; }
// // //         }
// // //     }

// // //     // задаём начальные значения (больше magnitude, чтобы чётко были владельцы)
// // //     nodeScores[seedA] = 10;
// // //     nodeOwners[seedA] = 1;
// // //     nodeScores[seedB] = -10;
// // //     nodeOwners[seedB] = 2;

// // //     Debug.Log($"[Server] Graph generated: seeds {seedA} (player1) and {seedB} (player2). Nodes: {nodePositions.Count}, edges: {edges.Count}");
// // // }

// // // private int GenerateRandomSeed()
// // // {
// // //     // используем криптографический RNG для генерации seed'а
// // //     byte[] data = new byte[4];
// // //     using (var rng = RandomNumberGenerator.Create())
// // //     {
// // //         rng.GetBytes(data);
// // //     }
// // //     int seed = System.BitConverter.ToInt32(data, 0);
// // //     // делаем seed положительным
// // //     if (seed == int.MinValue) seed = -seed;
// // //     if (seed < 0) seed = -seed;
// // //     return seed;
// // // }


// // //     // -------------------- обработка клика --------------------
// // //     private void OnClickMessageReceived(NetworkConnectionToClient conn, ClickMessage msg)
// // //     {
// // //         int nodeId = msg.nodeId;
// // //         if (!nodePositions.ContainsKey(nodeId))
// // //         {
// // //             Debug.LogWarning($"[Server] Click: unknown nodeId {nodeId} from conn {conn.connectionId}");
// // //             return;
// // //         }

// // //         // проверяем доступность клика по adjacency и владениям
// // //         if (!connIndex.TryGetValue(conn.connectionId, out int pIndex))
// // //         {
// // //             Debug.LogWarning($"[Server] Click: unknown connection {conn.connectionId}");
// // //             return;
// // //         }
// // //         byte playerOwnerId = (byte)((pIndex == 0) ? 1 : 2);

// // //         // если node нейтрален — можно захватить только если соседствует с вершиной игрока (или если игрок уже владеет соседней)
// // //         bool allowed = false;
// // //         // если игрок владеет хотя бы одну соседнюю вершину — разрешаем
// // //         foreach (int nbr in adjacency[nodeId])
// // //         {
// // //             if (nodeOwners.ContainsKey(nbr) && nodeOwners[nbr] == playerOwnerId)
// // //             {
// // //                 allowed = true;
// // //                 break;
// // //             }
// // //         }
// // //         // также разрешим если игрок уже владеет сам этой вершиной
// // //         if (nodeOwners.ContainsKey(nodeId) && nodeOwners[nodeId] == playerOwnerId)
// // //             allowed = true;

// // //         if (!allowed)
// // //         {
// // //             Debug.Log($"[Server] Click rejected: conn {conn.connectionId} (player {playerOwnerId}) cannot click node {nodeId} (not adjacent to player's nodes)");
// // //             return;
// // //         }

// // //         // применяем изменение score: player1 -> +1, player2 -> -1
// // //         if (playerOwnerId == 1) nodeScores[nodeId] += 1;
// // //         else nodeScores[nodeId] -= 1;

// // //         // пересчитать owner:
// // //         int sc = nodeScores[nodeId];
// // //         byte newOwner = 0;
// // //         if (sc > 0) newOwner = 1;
// // //         else if (sc < 0) newOwner = 2;
// // //         else newOwner = 0;

// // //         nodeOwners[nodeId] = newOwner;

// // //         // рассылаем обновление (NodeUpdateMessage) всем
// // //         NodeUpdateMessage um = new NodeUpdateMessage
// // //         {
// // //             nodeId = nodeId,
// // //             score = nodeScores[nodeId],
// // //             owner = newOwner
// // //         };
// // //         NetworkServer.SendToAll(um);

// // //         Debug.Log($"[Server] Node {nodeId} updated by conn {conn.connectionId} -> score={nodeScores[nodeId]} owner={newOwner}");
// // //     }

// // //     // -------------------- утилиты --------------------
// // //     private Vector2 VogelPoint(int i, float scale)
// // //     {
// // //         float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f)); // ≈2.39996
// // //         float theta = i * goldenAngle;
// // //         float r = scale * Mathf.Sqrt(i + 0.5f);
// // //         return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
// // //     }

// // //     private bool EdgeExists(List<(int a, int b)> list, int a, int b)
// // //     {
// // //         foreach (var e in list)
// // //         {
// // //             if ((e.a == a && e.b == b) || (e.a == b && e.b == a)) return true;
// // //         }
// // //         return false;
// // //     }
// // // }

// // using System.Collections;
// // using System.Collections.Generic;
// // using UnityEngine;
// // using Mirror;
// // using System.Security.Cryptography;

// // public class ServerGameController : MonoBehaviour
// // {
// //     [Header("Graph settings")]
// //     public int numNodes = 30;
// //     public float fieldRadius = 6f;
// //     public float nodeHitRadius = 0.6f;
// //     public int extraEdgesPerNode = 1;

// //     [Header("Players")]
// //     public int maxPlayers = 2;

// //     [Header("Economy")]
// //     public int goldPerNodePerTick = 1; // сколько даёт одна вершина за тик
// //     public float tickIntervalSeconds = 1f;
// //     public int maxGold = 10000;

// //     // graph data
// //     private bool graphGenerated = false;
// //     private int nextNodeId = 0;
// //     private readonly Dictionary<int, Vector2> nodePositions = new Dictionary<int, Vector2>();
// //     private readonly Dictionary<int, int> nodeScores = new Dictionary<int, int>();
// //     private readonly Dictionary<int, byte> nodeOwners = new Dictionary<int, byte>();
// //     private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();

// //     // players: key = conn.connectionId
// //     class PlayerState
// //     {
// //         public NetworkConnectionToClient conn;
// //         public byte ownerId; // 1 or 2
// //         public int gold;
// //         public int ownedNodes;
// //     }
// //     private readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>();

// //     // temporary mapping connId -> index (0,1...) (keeps assignment order)
// //     private readonly Dictionary<int, int> connIndex = new Dictionary<int, int>();

// //     void Awake()
// //     {
// //         // nothing to send here
// //     }

// //     // called from CustomNetworkManagerServer.OnStartServer
// //     public void OnServerStarted()
// //     {
// //         NetworkServer.RegisterHandler<ClickMessage>(OnClickMessageReceived, false);
// //         Debug.Log("[ServerGameController] ClickMessage handler registered");
// //         // start tick coroutine
        
// //         //StartCoroutine(GoldTickCoroutine());
// //     }

// //     // called from CustomNetworkManagerServer.OnServerConnect
// //     public void HandleClientConnected(NetworkConnectionToClient conn)
// //     {
// //         Debug.Log($"[Server] Client connected: connId={conn.connectionId}");

// //         if (!connIndex.ContainsKey(conn.connectionId))
// //         {
// //             int idx = connIndex.Count; // 0..n-1
// //             connIndex[conn.connectionId] = idx;
// //         }

// //         // assign PlayerState
// //         if (!players.ContainsKey(conn.connectionId))
// //         {
// //             PlayerState ps = new PlayerState();
// //             ps.conn = conn;
// //             // ownerId depends on index: index 0 -> ownerId 1 (red), index 1 -> ownerId 2 (blue)
// //             int idx = connIndex[conn.connectionId];
// //             ps.ownerId = (byte)((idx == 0) ? 1 : 2);
// //             ps.gold = 0;
// //             ps.ownedNodes = 0;
// //             players[conn.connectionId] = ps;
// //         }

// //         // generate graph once
// //         if (!graphGenerated)
// //         {
// //             GenerateGraph();
// //             graphGenerated = true;
// //             Debug.Log("[Server] Graph generated once and locked.");
// //         }

// //         // send graph to this client
// //         SendGraphToClient(conn);

// //         // recompute ownedNodes counts and send initial stats to this client (and optionally to others)
// //         RecalculateOwnedNodes();
// //         SendAllPlayerStats();
// //     }

// //     public void HandleClientDisconnected(NetworkConnectionToClient conn)
// //     {
// //         Debug.Log($"[Server] Client disconnected: connId={conn.connectionId}");
// //         if (players.ContainsKey(conn.connectionId))
// //             players.Remove(conn.connectionId);
// //         if (connIndex.ContainsKey(conn.connectionId))
// //             connIndex.Remove(conn.connectionId);

// //         // after disconnect, recompute owned counts (optional)
// //         RecalculateOwnedNodes();
// //         SendAllPlayerStats();
// //     }

// //     // ---------------- Graph generation (kept from your version) ----------------
// //     private void GenerateGraph()
// //     {
// //         nodePositions.Clear();
// //         nodeScores.Clear();
// //         nodeOwners.Clear();
// //         adjacency.Clear();
// //         nextNodeId = 0;

// //         int seed = GenerateRandomSeed();
// //         Random.InitState(seed);
// //         Debug.Log($"[Server] GenerateGraph seed = {seed}");

// //         float scale = fieldRadius / Mathf.Sqrt(numNodes) * 1.5f;
// //         float jitterAmount = fieldRadius * 0.05f;

// //         for (int i = 0; i < numNodes; i++)
// //         {
// //             Vector2 p = VogelPoint(i, scale);
// //             p += Random.insideUnitCircle * jitterAmount;
// //             int id = nextNodeId++;
// //             nodePositions[id] = p;
// //             nodeScores[id] = 0;
// //             nodeOwners[id] = 0;
// //             adjacency[id] = new List<int>();
// //         }

// //         List<(int a, int b, float dist)> allPairs = new List<(int, int, float)>();
// //         var ids = new List<int>(nodePositions.Keys);
// //         for (int i = 0; i < ids.Count; i++)
// //             for (int j = i + 1; j < ids.Count; j++)
// //                 allPairs.Add((ids[i], ids[j], Vector2.Distance(nodePositions[ids[i]], nodePositions[ids[j]])));

// //         HashSet<int> inTree = new HashSet<int>();
// //         List<(int a, int b)> edges = new List<(int a, int b)>();
// //         inTree.Add(ids[0]);
// //         while (inTree.Count < ids.Count)
// //         {
// //             float best = float.MaxValue;
// //             int ba = -1, bb = -1;
// //             foreach (var e in allPairs)
// //             {
// //                 if (inTree.Contains(e.a) && !inTree.Contains(e.b))
// //                 {
// //                     if (e.dist < best) { best = e.dist; ba = e.a; bb = e.b; }
// //                 }
// //                 else if (inTree.Contains(e.b) && !inTree.Contains(e.a))
// //                 {
// //                     if (e.dist < best) { best = e.dist; ba = e.b; bb = e.a; }
// //                 }
// //             }
// //             if (ba != -1 && bb != -1)
// //             {
// //                 edges.Add((ba, bb));
// //                 inTree.Add(bb);
// //             }
// //             else break;
// //         }

// //         foreach (int id in ids)
// //         {
// //             var neighbors = new List<(int id, float dist)>();
// //             foreach (int j in ids)
// //             {
// //                 if (j == id) continue;
// //                 float d = Vector2.Distance(nodePositions[id], nodePositions[j]);
// //                 neighbors.Add((j, d));
// //             }
// //             neighbors.Sort((x, y) => x.dist.CompareTo(y.dist));
// //             int added = 0;
// //             foreach (var n in neighbors)
// //             {
// //                 if (added >= extraEdgesPerNode) break;
// //                 if (!EdgeExists(edges, id, n.id))
// //                 {
// //                     edges.Add((id, n.id));
// //                     added++;
// //                 }
// //             }
// //         }

// //         foreach (var e in edges)
// //         {
// //             if (!adjacency[e.a].Contains(e.b)) adjacency[e.a].Add(e.b);
// //             if (!adjacency[e.b].Contains(e.a)) adjacency[e.b].Add(e.a);
// //         }

// //         // choose random seeds far apart
// //         int seedA = ids[Random.Range(0, ids.Count)];
// //         int seedB = seedA;
// //         float minSeedDist = fieldRadius * 0.6f;
// //         int attempts = 0;
// //         while (attempts < 200)
// //         {
// //             int candidate = ids[Random.Range(0, ids.Count)];
// //             if (Vector2.Distance(nodePositions[seedA], nodePositions[candidate]) >= minSeedDist && candidate != seedA)
// //             {
// //                 seedB = candidate;
// //                 break;
// //             }
// //             attempts++;
// //         }
// //         if (seedB == seedA)
// //         {
// //             float maxd = -1f;
// //             foreach (int j in ids)
// //             {
// //                 float d = Vector2.Distance(nodePositions[seedA], nodePositions[j]);
// //                 if (d > maxd) { maxd = d; seedB = j; }
// //             }
// //         }

// //         nodeScores[seedA] = 10;
// //         nodeOwners[seedA] = 1;
// //         nodeScores[seedB] = -10;
// //         nodeOwners[seedB] = 2;

// //         Debug.Log($"[Server] Graph generated: seeds {seedA} (player1) and {seedB} (player2). Nodes: {nodePositions.Count}, edges: {edges.Count}");
// //     }

// //     private int GenerateRandomSeed()
// //     {
// //         byte[] data = new byte[4];
// //         using (var rng = RandomNumberGenerator.Create())
// //         {
// //             rng.GetBytes(data);
// //         }
// //         int seed = System.BitConverter.ToInt32(data, 0);
// //         if (seed == int.MinValue) seed = -seed;
// //         if (seed < 0) seed = -seed;
// //         return seed;
// //     }

// //     // ---------------- sending graph ----------------
// //     private void SendGraphToClient(NetworkConnectionToClient conn)
// //     {
// //         int n = nodePositions.Count;
// //         int[] nodeIds = new int[n];
// //         Vector2[] positions = new Vector2[n];
// //         int[] scores = new int[n];
// //         byte[] owners = new byte[n];

// //         var ids = new List<int>(nodePositions.Keys);
// //         for (int i = 0; i < ids.Count; i++)
// //         {
// //             int id = ids[i];
// //             nodeIds[i] = id;
// //             positions[i] = nodePositions[id];
// //             scores[i] = nodeScores[id];
// //             owners[i] = nodeOwners[id];
// //         }

// //         List<int> edgeFrom = new List<int>();
// //         List<int> edgeTo = new List<int>();
// //         var seenPairs = new HashSet<(int, int)>();
// //         foreach (var kvp in adjacency)
// //         {
// //             int a = kvp.Key;
// //             foreach (int b in kvp.Value)
// //             {
// //                 var pair = (Mathf.Min(a, b), Mathf.Max(a, b));
// //                 if (!seenPairs.Contains(pair))
// //                 {
// //                     seenPairs.Add(pair);
// //                     edgeFrom.Add(pair.Item1);
// //                     edgeTo.Add(pair.Item2);
// //                 }
// //             }
// //         }

// //         GraphMessage gm = new GraphMessage
// //         {
// //             nodeIds = nodeIds,
// //             positions = positions,
// //             scores = scores,
// //             owners = owners,
// //             edgeFrom = edgeFrom.ToArray(),
// //             edgeTo = edgeTo.ToArray()
// //         };

// //         conn.Send(gm);
// //         Debug.Log($"[Server] Sent GraphMessage to connId={conn.connectionId} nodes={n} edges={edgeFrom.Count}");
// //     }

// //     // ---------------- handle clicks ----------------
// //     private void OnClickMessageReceived(NetworkConnectionToClient conn, ClickMessage msg)
// //     {
// //         int nodeId = msg.nodeId;
// //         if (!nodePositions.ContainsKey(nodeId))
// //         {
// //             Debug.LogWarning($"[Server] Click: unknown nodeId {nodeId} from conn {conn.connectionId}");
// //             return;
// //         }

// //         if (!connIndex.TryGetValue(conn.connectionId, out int pIndex))
// //         {
// //             Debug.LogWarning($"[Server] Click: unknown connection {conn.connectionId}");
// //             return;
// //         }
// //         byte playerOwnerId = (byte)((pIndex == 0) ? 1 : 2);

// //         // adjacency rule: must be adjacent to some node owned by player, or already owned
// //         bool allowed = false;
// //         foreach (int nbr in adjacency[nodeId])
// //         {
// //             if (nodeOwners.ContainsKey(nbr) && nodeOwners[nbr] == playerOwnerId)
// //             {
// //                 allowed = true;
// //                 break;
// //             }
// //         }
// //         if (nodeOwners.ContainsKey(nodeId) && nodeOwners[nodeId] == playerOwnerId)
// //             allowed = true;

// //         if (!allowed)
// //         {
// //             Debug.Log($"[Server] Click rejected: conn {conn.connectionId} (player {playerOwnerId}) cannot click node {nodeId}");
// //             return;
// //         }

// //         // apply score: player1 -> +1, player2 -> -1
// //         if (playerOwnerId == 1) nodeScores[nodeId] += 1;
// //         else nodeScores[nodeId] -= 1;

// //         // recalc owner
// //         int sc = nodeScores[nodeId];
// //         byte newOwner = 0;
// //         if (sc > 0) newOwner = 1;
// //         else if (sc < 0) newOwner = 2;
// //         else newOwner = 0;

// //         nodeOwners[nodeId] = newOwner;

// //         // broadcast node update to all clients
// //         NodeUpdateMessage um = new NodeUpdateMessage { nodeId = nodeId, score = nodeScores[nodeId], owner = newOwner };
// //         NetworkServer.SendToAll(um);

// //         // recalc players ownedNodes and send stats to each player
// //         RecalculateOwnedNodes();
// //         SendAllPlayerStats();

// //         Debug.Log($"[Server] Node {nodeId} updated by conn {conn.connectionId} -> score={nodeScores[nodeId]} owner={newOwner}");
// //     }

// //     // ---------------- recalc owned nodes and player stats ----------------
// //     private void RecalculateOwnedNodes()
// //     {
// //         // reset counts
// //         foreach (var kv in players) kv.Value.ownedNodes = 0;

// //         foreach (var kv in nodeOwners)
// //         {
// //             int nodeId = kv.Key;
// //             byte owner = kv.Value;
// //             if (owner == 0) continue;
// //             // find player's conn by ownerId
// //             foreach (var p in players.Values)
// //             {
// //                 if (p.ownerId == owner)
// //                 {
// //                     p.ownedNodes++;
// //                 }
// //             }
// //         }
// //     }

// //     private void SendPlayerStatsToConn(PlayerState ps)
// //     {
// //         if (ps == null || ps.conn == null) return;
// //         PlayerStatsMessage m = new PlayerStatsMessage { ownerId = ps.ownerId, gold = ps.gold, ownedNodes = ps.ownedNodes };
// //         ps.conn.Send(m);
// //     }

// //     private void SendAllPlayerStats()
// //     {
// //         foreach (var kv in players)
// //         {
// //             SendPlayerStatsToConn(kv.Value);
// //         }
// //     }

// //     // ---------------- gold tick ----------------
// //     IEnumerator GoldTickCoroutine()
// //     {
// //         while (true)
// //         {
// //             yield return new WaitForSeconds(tickIntervalSeconds);

// //             // add gold to every player
// //             bool anyChanged = false;
// //             foreach (var kv in players)
// //             {
// //                 var ps = kv.Value;
// //                 int add = ps.ownedNodes * goldPerNodePerTick;
// //                 if (add != 0)
// //                 {
// //                     ps.gold += add;
// //                     if (ps.gold > maxGold) ps.gold = maxGold;
// //                     anyChanged = true;
// //                 }
// //             }

// //             if (anyChanged)
// //             {
// //                 // send updated stats to clients
// //                 SendAllPlayerStats();
// //             }
// //         }
// //     }

// //     // ---------------- utils ----------------
// //     private Vector2 VogelPoint(int i, float scale)
// //     {
// //         float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
// //         float theta = i * goldenAngle;
// //         float r = scale * Mathf.Sqrt(i + 0.5f);
// //         return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
// //     }

// //     private bool EdgeExists(List<(int a, int b)> list, int a, int b)
// //     {
// //         foreach (var e in list)
// //         {
// //             if ((e.a == a && e.b == b) || (e.a == b && e.b == a)) return true;
// //         }
// //         return false;
// //     }
// // }


// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using Mirror;
// using System.Security.Cryptography;

// /// <summary>
// /// Server-side game controller:
// /// - генерирует граф (узлы + ребра)
// /// - хранит scores/owners для узлов
// /// - принимает ClickMessage от клиентов
// /// - рассылает GraphMessage / NodeUpdateMessage / PlayerStatsMessage
// /// - начисляет золото игрокам по таймеру
// /// 
// /// Важно: этот класс НЕ запускает корутину сам — корутина запускается из CustomNetworkManagerServer (на активном объекте).
// /// </summary>
// public class ServerGameController : MonoBehaviour
// {
//     [Header("Graph settings")]
//     public int numNodes = 30;
//     public float fieldRadius = 6f;
//     public int extraEdgesPerNode = 1;

//     [Header("Gold / tick")]
//     public float tickIntervalSeconds = 1.0f;
//     public int goldPerNodePerTick = 1;
//     public int maxGold = 99999;

//     [Header("Gameplay")]
//     public int maxPlayers = 2;

//     // internal graph state
//     private bool graphGenerated = false;
//     private int nextNodeId = 0;
//     private readonly Dictionary<int, Vector2> nodePositions = new Dictionary<int, Vector2>();
//     private readonly Dictionary<int, int> nodeScores = new Dictionary<int, int>();
//     private readonly Dictionary<int, byte> nodeOwners = new Dictionary<int, byte>(); // 0 neutral, 1 player1, 2 player2
//     private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();

//     // players
//     class PlayerState
//     {
//         public NetworkConnectionToClient conn;
//         public int ownedNodes;
//         public int gold;
//         public byte ownerId; // 1 or 2
//     }
//     private readonly Dictionary<int, PlayerState> players = new Dictionary<int, PlayerState>(); // key = conn.connectionId
//     private readonly Dictionary<int, int> connIndex = new Dictionary<int, int>(); // connId -> idx 0/1

//     // --- сообщения (если у тебя уже определены в общем NetworkMessages.cs — убери дублирование там) ---
//     public struct PlayerStatsMessage : NetworkMessage
//     {
//         public byte ownerId;
//         public int gold;
//         public int ownedNodes;
//     }

//     // ----------------- lifecycle / init -----------------
//     // вызовется из CustomNetworkManagerServer.OnStartServer()
//     public void OnServerStarted()
//     {
//         // регистрируем хендлер кликов
//         NetworkServer.RegisterHandler<ClickMessage>(OnClickMessageReceived, false);
//         Debug.Log("[ServerGameController] ClickMessage handler registered");
//         // Мы НЕ стартуем корутину здесь, т.к. этот объект мог быть неактивен в момент вызова. Корутину запускает NetworkManager.
//     }

//     // вызывается, когда клиент подключился
//     public void HandleClientConnected(NetworkConnectionToClient conn)
//     {
//         Debug.Log($"[Server] Client connected connId={conn.connectionId}");

//         if (!connIndex.ContainsKey(conn.connectionId))
//         {
//             int idx = connIndex.Count; // 0,1...
//             connIndex[conn.connectionId] = idx;
//         }

//         // создаём PlayerState
//         var ps = new PlayerState
//         {
//             conn = conn,
//             gold = 0,
//             ownedNodes = 0,
//             ownerId = (byte)((connIndex[conn.connectionId] == 0) ? 1 : 2)
//         };
//         players[conn.connectionId] = ps;

//         // генерация графа только один раз
//         if (!graphGenerated)
//         {
//             GenerateGraph();
//             graphGenerated = true;
//             Debug.Log("[Server] Graph generated once and locked.");
//         }

//         // пересчитаем ownedNodes для всех игроков
//         RecalculateAllOwnedNodes();

//         // отправим граф новому клиенту
//         SendGraphToClient(conn);

//         // и пришлём текущую статистику игроку (его собственной)
//         SendPlayerStatsToConn(conn);
//     }

//     public void HandleClientDisconnected(NetworkConnectionToClient conn)
//     {
//         Debug.Log($"[Server] Client disconnected connId={conn.connectionId}");
//         if (connIndex.ContainsKey(conn.connectionId))
//             connIndex.Remove(conn.connectionId);
//         if (players.ContainsKey(conn.connectionId))
//             players.Remove(conn.connectionId);
//         RecalculateAllOwnedNodes();
//     }

//     // ----------------- генерация графа -----------------
//     private void GenerateGraph()
//     {
//         nodePositions.Clear();
//         nodeScores.Clear();
//         nodeOwners.Clear();
//         adjacency.Clear();
//         nextNodeId = 0;

//         // случайный seed (криптографический)
//         int seed = GenerateRandomSeed();
//         Random.InitState(seed);
//         Debug.Log($"[Server] GenerateGraph seed = {seed}");

//         // позиции — Vogel-like spiral + jitter
//         float scale = fieldRadius / Mathf.Sqrt(Mathf.Max(1, numNodes)) * 1.5f;
//         float jitter = fieldRadius * 0.06f;

//         for (int i = 0; i < numNodes; i++)
//         {
//             Vector2 p = VogelPoint(i, scale) + Random.insideUnitCircle * jitter;
//             int id = nextNodeId++;
//             nodePositions[id] = p;
//             nodeScores[id] = 0;
//             nodeOwners[id] = 0;
//             adjacency[id] = new List<int>();
//         }

//         // edges: простая Prim MST
//         var ids = new List<int>(nodePositions.Keys);
//         List<(int a, int b, float dist)> allPairs = new List<(int, int, float)>();
//         for (int i = 0; i < ids.Count; i++)
//             for (int j = i + 1; j < ids.Count; j++)
//                 allPairs.Add((ids[i], ids[j], Vector2.Distance(nodePositions[ids[i]], nodePositions[ids[j]])));

//         HashSet<int> inTree = new HashSet<int>();
//         List<(int a, int b)> edges = new List<(int a, int b)>();
//         inTree.Add(ids[0]);
//         while (inTree.Count < ids.Count)
//         {
//             float best = float.MaxValue;
//             int ba = -1, bb = -1;
//             foreach (var e in allPairs)
//             {
//                 if (inTree.Contains(e.a) && !inTree.Contains(e.b))
//                 {
//                     if (e.dist < best) { best = e.dist; ba = e.a; bb = e.b; }
//                 }
//                 else if (inTree.Contains(e.b) && !inTree.Contains(e.a))
//                 {
//                     if (e.dist < best) { best = e.dist; ba = e.b; bb = e.a; }
//                 }
//             }
//             if (ba != -1 && bb != -1)
//             {
//                 edges.Add((ba, bb));
//                 inTree.Add(bb);
//             }
//             else break;
//         }

//         // добавим по ближайшим соседям
//         foreach (int id in ids)
//         {
//             var neigh = new List<(int id, float dist)>();
//             foreach (int j in ids) if (j != id) neigh.Add((j, Vector2.Distance(nodePositions[id], nodePositions[j])));
//             neigh.Sort((x, y) => x.dist.CompareTo(y.dist));
//             int added = 0;
//             for (int k = 0; k < neigh.Count && added < extraEdgesPerNode; k++)
//             {
//                 int other = neigh[k].id;
//                 if (!EdgeExists(edges, id, other))
//                 {
//                     edges.Add((id, other));
//                     added++;
//                 }
//             }
//         }

//         // fill adjacency
//         foreach (var e in edges)
//         {
//             if (!adjacency[e.a].Contains(e.b)) adjacency[e.a].Add(e.b);
//             if (!adjacency[e.b].Contains(e.a)) adjacency[e.b].Add(e.a);
//         }

//         // стартовые владельцы: выбираем две удалённые вершины
//         int seedA = ids[Random.Range(0, ids.Count)];
//         int seedB = seedA;
//         float minSeedDist = fieldRadius * 0.5f;
//         int tries = 0;
//         while (tries < 200)
//         {
//             int cand = ids[Random.Range(0, ids.Count)];
//             if (cand != seedA && Vector2.Distance(nodePositions[seedA], nodePositions[cand]) >= minSeedDist)
//             {
//                 seedB = cand; break;
//             }
//             tries++;
//         }
//         if (seedB == seedA)
//         {
//             float maxd = -1f;
//             foreach (int j in ids)
//             {
//                 float d = Vector2.Distance(nodePositions[seedA], nodePositions[j]);
//                 if (d > maxd) { maxd = d; seedB = j; }
//             }
//         }

//         nodeScores[seedA] = 10; nodeOwners[seedA] = 1;
//         nodeScores[seedB] = -10; nodeOwners[seedB] = 2;

//         Debug.Log($"[Server] Graph generated: nodes={nodePositions.Count}, edges={edges.Count}, seeds={seedA},{seedB}");
//     }

//     private int GenerateRandomSeed()
//     {
//         byte[] b = new byte[4];
//         using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
//         int s = System.BitConverter.ToInt32(b, 0);
//         if (s == int.MinValue) s = -s;
//         if (s < 0) s = -s;
//         return s;
//     }

//     // ----------------- отправка графа -----------------
//     private void SendGraphToClient(NetworkConnectionToClient conn)
//     {
//         if (!NetworkServer.active)
//         {
//             Debug.LogWarning("[Server] NetworkServer not active, skip SendGraphToClient");
//             return;
//         }

//         var ids = new List<int>(nodePositions.Keys);
//         int n = ids.Count;
//         int[] nodeIds = new int[n];
//         Vector2[] positions = new Vector2[n];
//         int[] scores = new int[n];
//         byte[] owners = new byte[n];

//         for (int i = 0; i < ids.Count; i++)
//         {
//             int id = ids[i];
//             nodeIds[i] = id;
//             positions[i] = nodePositions[id];
//             scores[i] = nodeScores[id];
//             owners[i] = nodeOwners[id];
//         }

//         List<int> edgeFrom = new List<int>();
//         List<int> edgeTo = new List<int>();
//         var seen = new HashSet<(int,int)>();
//         foreach (var kv in adjacency)
//         {
//             int a = kv.Key;
//             foreach (int b in kv.Value)
//             {
//                 var p = (Mathf.Min(a,b), Mathf.Max(a,b));
//                 if (!seen.Contains(p))
//                 {
//                     seen.Add(p);
//                     edgeFrom.Add(p.Item1);
//                     edgeTo.Add(p.Item2);
//                 }
//             }
//         }

//         GraphMessage gm = new GraphMessage
//         {
//             nodeIds = nodeIds,
//             positions = positions,
//             scores = scores,
//             owners = owners,
//             edgeFrom = edgeFrom.ToArray(),
//             edgeTo = edgeTo.ToArray()
//         };

//         conn.Send(gm);
//         Debug.Log($"[Server] Sent GraphMessage to connId={conn.connectionId} nodes={n} edges={edgeFrom.Count}");
//     }

//     // ----------------- клики от клиента -----------------
//     private void OnClickMessageReceived(NetworkConnectionToClient conn, ClickMessage msg)
//     {
//         int nodeId = msg.nodeId;
//         if (!nodePositions.ContainsKey(nodeId))
//         {
//             Debug.LogWarning($"[Server] Click: unknown nodeId {nodeId} from conn {conn.connectionId}");
//             return;
//         }

//         if (!connIndex.TryGetValue(conn.connectionId, out int idx))
//         {
//             Debug.LogWarning($"[Server] Click from unknown conn {conn.connectionId}");
//             return;
//         }
//         byte playerOwnerId = (byte)((idx == 0) ? 1 : 2);

//         // проверяем adjacency: можно кликать только если владеешь соседней вершиной или сам владеешь
//         bool allowed = false;
//         if (nodeOwners.ContainsKey(nodeId) && nodeOwners[nodeId] == playerOwnerId) allowed = true;
//         else
//         {
//             foreach (int nbr in adjacency[nodeId])
//             {
//                 if (nodeOwners.ContainsKey(nbr) && nodeOwners[nbr] == playerOwnerId) { allowed = true; break; }
//             }
//         }

//         if (!allowed)
//         {
//             Debug.Log($"[Server] Click rejected: conn {conn.connectionId} (player {playerOwnerId}) cannot click node {nodeId}");
//             return;
//         }

//         // применяем изменение
//         if (playerOwnerId == 1) nodeScores[nodeId] += 1;
//         else nodeScores[nodeId] -= 1;

//         int sc = nodeScores[nodeId];
//         byte newOwner = 0;
//         if (sc > 0) newOwner = 1;
//         else if (sc < 0) newOwner = 2;
//         nodeOwners[nodeId] = newOwner;

//         // пересчёт ownedNodes
//         RecalculateAllOwnedNodes();

//         // рассылаем update всем
//         NodeUpdateMessage um = new NodeUpdateMessage
//         {
//             nodeId = nodeId,
//             score = nodeScores[nodeId],
//             owner = newOwner
//         };
//         NetworkServer.SendToAll(um);

//         Debug.Log($"[Server] Node {nodeId} updated by conn {conn.connectionId} -> score={nodeScores[nodeId]} owner={newOwner}");
//     }

//     // ----------------- корутина начисления золота -----------------
//     // Делает Send индивидуально каждому клиенту (чтобы игрок видел только свой счет)
//     public IEnumerator GoldTickCoroutine()
//     {
//         Debug.Log("[ServerGameController] GoldTickCoroutine started. tickInterval=" + tickIntervalSeconds + " goldPerNodePerTick=" + goldPerNodePerTick);
//         while (true)
//         {
//             yield return new WaitForSeconds(tickIntervalSeconds);

//             // для каждого игрока начисляем
//             foreach (var kv in players)
//             {
//                 var ps = kv.Value;
//                 int add = ps.ownedNodes * goldPerNodePerTick;
//                 Debug.Log($"[Server][Tick] connId={ps.conn.connectionId} owner={ps.ownerId} ownedNodes={ps.ownedNodes} add={add} beforeGold={ps.gold}");

//                 if (add != 0)
//                 {
//                     ps.gold += add;
//                     if (ps.gold > maxGold) ps.gold = maxGold;

//                     // отправляем только этому клиенту актуальную статистику
//                     SendPlayerStatsToConn(ps.conn);
//                     Debug.Log($"[Server][Tick] connId={ps.conn.connectionId} newGold={ps.gold}");
//                 }
//                 else
//                 {
//                     // даже если add==0 — можно периодически шлать статистику; пока не шлём
//                 }
//             }
//         }
//     }

//     // отправка игроку его статистики
//     private void SendPlayerStatsToConn(NetworkConnectionToClient conn)
//     {
//         if (!players.TryGetValue(conn.connectionId, out PlayerState ps)) return;

//         PlayerStatsMessage pm = new PlayerStatsMessage
//         {
//             ownerId = ps.ownerId,
//             gold = ps.gold,
//             ownedNodes = ps.ownedNodes
//         };

//         conn.Send(pm);
//     }

//     // пересчитать ownedNodes для всех игроков
//     private void RecalculateAllOwnedNodes()
//     {
//         // zero
//         foreach (var kv in players) kv.Value.ownedNodes = 0;

//         foreach (var kv in nodeOwners)
//         {
//             int nodeId = kv.Key;
//             byte owner = kv.Value;
//             if (owner == 0) continue;
//             // найти игрок с ownerId
//             foreach (var pkv in players)
//             {
//                 var ps = pkv.Value;
//                 if (ps.ownerId == owner)
//                 {
//                     ps.ownedNodes++;
//                 }
//             }
//         }
//     }

//     // ----------------- утилиты -----------------
//     private Vector2 VogelPoint(int i, float scale)
//     {
//         float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
//         float theta = i * goldenAngle;
//         float r = scale * Mathf.Sqrt(i + 0.5f);
//         return new Vector2(r * Mathf.Cos(theta), r * Mathf.Sin(theta));
//     }

//     private bool EdgeExists(List<(int a, int b)> list, int a, int b)
//     {
//         foreach (var e in list)
//             if ((e.a == a && e.b == b) || (e.a == b && e.b == a)) return true;
//         return false;
//     }
// }


// ServerGameController.cs
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
    public float nodeHitRadius = 0.6f;
    public int extraEdgesPerNode = 1;

    [Header("Players")]
    public int maxPlayers = 2;

    [Header("Economy")]
    public float goldTickInterval = 1.0f; // seconds
    public int goldPerNodeBase = 1;        // base gold per owned node per tick
    public int maxGoldPerNodePerTick = 5;  // cap

    // internal
    private bool graphGenerated = false;
    private int nextNodeId = 0;
    private bool goldStarted = false;

    private readonly Dictionary<int, Vector2> nodePositions = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, int> nodeScores = new Dictionary<int, int>();
    private readonly Dictionary<int, byte> nodeOwners = new Dictionary<int, byte>(); // 0 neutral,1 player1,2 player2
    private readonly Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();

    // connection mapping
    private readonly Dictionary<int, int> connIndex = new Dictionary<int, int>(); // connId -> index (0..)
    private readonly Dictionary<int, NetworkConnectionToClient> indexToConn = new Dictionary<int, NetworkConnectionToClient>(); // index -> conn

    // player resources (by index 0..maxPlayers-1)
    private int[] playerGold;
    private int[] playerOwnedCount;
    private int[] playerClickPower;      // сила клика каждого игрока
    private float[] playerGoldMultiplier; // множитель генерации золота
    private bool[] playerEliminated;      // исключен ли игрок из игры

    // game state
    private byte gameState = 0;  // 0=Playing, 1=Player1Won, 2=Player2Won
    private byte winnerOwnerId = 0;

    Coroutine goldCoroutine = null;

    void Awake()
    {
        // prepare arrays
        playerGold = new int[maxPlayers];
        playerOwnedCount = new int[maxPlayers];
        playerClickPower = new int[maxPlayers];
        playerGoldMultiplier = new float[maxPlayers];
        playerEliminated = new bool[maxPlayers];
        
        // initialize default values
        for (int i = 0; i < maxPlayers; i++)
        {
            playerClickPower[i] = 1;
            playerGoldMultiplier[i] = 1.0f;
            playerEliminated[i] = false;
        }
    }

    // Called from CustomNetworkManagerServer.OnStartServer()
    public void OnServerStarted()
    {
        // register click handler
        NetworkServer.RegisterHandler<ClickMessage>(OnClickMessageReceived, false);
        NetworkServer.RegisterHandler<BuyBonusMessage>(OnBuyBonusReceived, false);
        Debug.Log("[ServerGameController] ClickMessage and BuyBonusMessage handlers registered");
    }

    // Called from CustomNetworkManagerServer.OnServerConnect
    public void HandleClientConnected(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Server] Client connected: connId={conn.connectionId}");


         if (!goldStarted)
        {
            goldStarted = true;
            goldCoroutine = StartCoroutine(GoldTickRoutine());
            Debug.Log("[ServerGameController] Gold coroutine started");
        }


        if (connIndex.Count >= maxPlayers)
        {
            Debug.Log("[Server] Room full, disconnecting new client");
            conn.Disconnect();
            return;
        }

        int idx = connIndex.Count; // next index 0..
        connIndex[conn.connectionId] = idx;
        indexToConn[idx] = conn;

        // if graph not generated - create it once
        if (!graphGenerated)
        {
            GenerateGraph();
            graphGenerated = true;
            Debug.Log("[Server] Graph generated once and locked.");
        }

        // send full graph to the new client
        SendGraphToClient(conn);

        // send initial PlayerStatsMessage telling the client who they are (ownerId)
        byte ownerId = (byte)(idx == 0 ? 1 : 2);
        SendPlayerStatsToConn(conn, ownerId, playerGold[idx], GetOwnedCountByIndex(idx), playerClickPower[idx]);

        Debug.Log($"[Server] Assigned idx {idx} to conn {conn.connectionId} ownerId={ownerId}");
        
        // send current game state
        SendGameStateToConn(conn);

        // optionally broadcast current stats to all players
        BroadcastAllPlayerStats();
    }

    public void HandleClientDisconnected(NetworkConnectionToClient conn)
    {
        Debug.Log($"[Server] Client disconnected: connId={conn.connectionId}");
        if (connIndex.TryGetValue(conn.connectionId, out int idx))
        {
            connIndex.Remove(conn.connectionId);
            indexToConn.Remove(idx);
            // keep playerGold but free slot; future reconnects will get new index
        }
    }

    // ---------------- generation ----------------
    private void GenerateGraph()
    {
        nodePositions.Clear();
        nodeScores.Clear();
        nodeOwners.Clear();
        adjacency.Clear();
        nextNodeId = 0;

        int seed = GenerateRandomSeed();
        Random.InitState(seed);
        Debug.Log($"[Server] GenerateGraph seed = {seed}");

        float scale = fieldRadius / Mathf.Sqrt(numNodes) * 1.5f;
        float jitterAmount = fieldRadius * 0.05f;

        for (int i = 0; i < numNodes; i++)
        {
            Vector2 p = VogelPoint(i, scale);
            p += Random.insideUnitCircle * jitterAmount;
            int id = nextNodeId++;
            nodePositions[id] = p;
            nodeScores[id] = 0;
            nodeOwners[id] = 0;
            adjacency[id] = new List<int>();
        }

        // build MST (Prim)
        List<(int a, int b, float dist)> allPairs = new List<(int, int, float)>();
        var ids = new List<int>(nodePositions.Keys);
        for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
                allPairs.Add((ids[i], ids[j], Vector2.Distance(nodePositions[ids[i]], nodePositions[ids[j]])));

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

        // extra edges
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

        // fill adjacency
        foreach (var e in edges)
        {
            if (!adjacency[e.a].Contains(e.b)) adjacency[e.a].Add(e.b);
            if (!adjacency[e.b].Contains(e.a)) adjacency[e.b].Add(e.a);
        }

        // choose seeds randomly (require distance)
        int seedA = ids[Random.Range(0, ids.Count)];
        int seedB = seedA;
        float minSeedDist = fieldRadius * 0.6f;
        int attempts = 0;
        while (attempts < 200)
        {
            int candidate = ids[Random.Range(0, ids.Count)];
            if (Vector2.Distance(nodePositions[seedA], nodePositions[candidate]) >= minSeedDist && candidate != seedA)
            {
                seedB = candidate;
                break;
            }
            attempts++;
        }
        if (seedB == seedA)
        {
            float maxd = -1f;
            foreach (int j in ids)
            {
                float d = Vector2.Distance(nodePositions[seedA], nodePositions[j]);
                if (d > maxd) { maxd = d; seedB = j; }
            }
        }

        nodeScores[seedA] = 10; nodeOwners[seedA] = 1;
        nodeScores[seedB] = -10; nodeOwners[seedB] = 2;

        Debug.Log($"[Server] Graph generated: seeds {seedA} (p1) {seedB} (p2). Nodes: {nodePositions.Count}, edges: {edges.Count}");
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

    // ---------------- networking: send graph / updates ----------------
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
    }

    private void BroadcastAllPlayerStats()
    {
        for (int idx = 0; idx < maxPlayers; idx++)
        {
            if (indexToConn.TryGetValue(idx, out var conn))
            {
                byte ownerId = (byte)(idx == 0 ? 1 : 2);
                SendPlayerStatsToConn(conn, ownerId, playerGold[idx], GetOwnedCountByIndex(idx), playerClickPower[idx]);
            }
        }
    }

    private void SendPlayerStatsToConn(NetworkConnectionToClient conn, byte ownerId, int gold, int ownedNodes, int clickPower)
    {
        PlayerStatsMessage m = new PlayerStatsMessage { ownerId = ownerId, gold = gold, ownedNodes = ownedNodes, clickPower = clickPower };
        conn.Send(m);
    }
    
    private void SendGameStateToConn(NetworkConnectionToClient conn)
    {
        GameStateMessage msg = new GameStateMessage { gameState = gameState, winnerOwnerId = winnerOwnerId };
        conn.Send(msg);
    }
    
    private void BroadcastGameState()
    {
        GameStateMessage msg = new GameStateMessage { gameState = gameState, winnerOwnerId = winnerOwnerId };
        NetworkServer.SendToAll(msg);
    }

    // ---------------- clicks ----------------
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
            Debug.LogWarning($"[Server] Click: unknown connection {conn.connectionId}");
            return;
        }
        byte playerOwnerId = (byte)((pIndex == 0) ? 1 : 2);

        // check adjacency / ownership rule
        bool allowed = false;
        // allowed if node owner is same as player (they can click their own nodes)
        if (nodeOwners.TryGetValue(nodeId, out byte curOwner) && curOwner == playerOwnerId) allowed = true;

        // or allowed if any neighbor is owned by player
        foreach (int nbr in adjacency[nodeId])
        {
            if (nodeOwners.TryGetValue(nbr, out byte o) && o == playerOwnerId)
            {
                allowed = true;
                break;
            }
        }

        if (!allowed)
        {
            Debug.Log($"[Server] Click rejected: conn {conn.connectionId} (player {playerOwnerId}) cannot click node {nodeId}");
            return;
        }
        
        // check if game is over
        if (gameState != 0)
        {
            Debug.Log($"[Server] Click rejected: game is over");
            return;
        }

        // apply change with click power
        int clickPower = playerClickPower[pIndex];
        if (playerOwnerId == 1) nodeScores[nodeId] += clickPower;
        else nodeScores[nodeId] -= clickPower;

        // recalc owner
        int sc = nodeScores[nodeId];
        byte newOwner = 0;
        if (sc > 0) newOwner = 1;
        else if (sc < 0) newOwner = 2;
        else newOwner = 0;
        nodeOwners[nodeId] = newOwner;

        NodeUpdateMessage um = new NodeUpdateMessage { nodeId = nodeId, score = nodeScores[nodeId], owner = newOwner };
        NetworkServer.SendToAll(um);
        Debug.Log($"[Server] Node {nodeId} updated by conn {conn.connectionId} -> score={nodeScores[nodeId]} owner={newOwner}");

        // after update maybe update owned counts and send stats
        UpdateOwnedCountsAndNotify();
        
        // check for victory condition
        CheckVictoryCondition();
    }

    // ---------------- bonus system ----------------
    private void OnBuyBonusReceived(NetworkConnectionToClient conn, BuyBonusMessage msg)
    {
        if (!connIndex.TryGetValue(conn.connectionId, out int pIndex))
        {
            SendBonusResponse(conn, false, "Игрок не найден");
            return;
        }
        
        if (gameState != 0)
        {
            SendBonusResponse(conn, false, "Игра окончена");
            return;
        }

        byte playerOwnerId = (byte)(pIndex == 0 ? 1 : 2);
        
        // Bonus types: 0=IncreaseClickPower, 1=BoostNodeScore, 2=BoostGoldGen, 3=AttackEnemy
        switch (msg.bonusType)
        {
            case 0: // Increase Click Power (cost: 50)
                if (playerGold[pIndex] >= 50)
                {
                    playerGold[pIndex] -= 50;
                    playerClickPower[pIndex] += 1;
                    SendBonusResponse(conn, true, $"Сила клика увеличена до {playerClickPower[pIndex]}!");
                    UpdateOwnedCountsAndNotify();
                }
                else
                {
                    SendBonusResponse(conn, false, "Недостаточно золота (требуется 50)");
                }
                break;
                
            case 1: // Boost Own Node Score (cost: 30)
                if (msg.targetNodeId < 0 || !nodePositions.ContainsKey(msg.targetNodeId))
                {
                    SendBonusResponse(conn, false, "Неверный узел");
                    return;
                }
                if (nodeOwners[msg.targetNodeId] != playerOwnerId)
                {
                    SendBonusResponse(conn, false, "Это не ваш узел");
                    return;
                }
                if (playerGold[pIndex] >= 30)
                {
                    playerGold[pIndex] -= 30;
                    int boost = 5;
                    if (playerOwnerId == 1) nodeScores[msg.targetNodeId] += boost;
                    else nodeScores[msg.targetNodeId] -= boost;
                    
                    NodeUpdateMessage um = new NodeUpdateMessage { 
                        nodeId = msg.targetNodeId, 
                        score = nodeScores[msg.targetNodeId], 
                        owner = nodeOwners[msg.targetNodeId] 
                    };
                    NetworkServer.SendToAll(um);
                    SendBonusResponse(conn, true, $"Узел усилен на {boost}!");
                    UpdateOwnedCountsAndNotify();
                }
                else
                {
                    SendBonusResponse(conn, false, "Недостаточно золота (требуется 30)");
                }
                break;
                
            case 2: // Boost Gold Generation (cost: 100)
                if (playerGold[pIndex] >= 100)
                {
                    playerGold[pIndex] -= 100;
                    playerGoldMultiplier[pIndex] += 0.5f;
                    SendBonusResponse(conn, true, $"Генерация золота увеличена до x{playerGoldMultiplier[pIndex]:F1}!");
                    UpdateOwnedCountsAndNotify();
                }
                else
                {
                    SendBonusResponse(conn, false, "Недостаточно золота (требуется 100)");
                }
                break;
                
            case 3: // Attack Enemy Node (cost: 40)
                if (msg.targetNodeId < 0 || !nodePositions.ContainsKey(msg.targetNodeId))
                {
                    SendBonusResponse(conn, false, "Неверный узел");
                    return;
                }
                byte targetOwner = nodeOwners[msg.targetNodeId];
                if (targetOwner == 0 || targetOwner == playerOwnerId)
                {
                    SendBonusResponse(conn, false, "Выберите вражеский узел");
                    return;
                }
                if (playerGold[pIndex] >= 40)
                {
                    playerGold[pIndex] -= 40;
                    int damage = 7;
                    if (playerOwnerId == 1) nodeScores[msg.targetNodeId] += damage;
                    else nodeScores[msg.targetNodeId] -= damage;
                    
                    // recalc owner
                    int sc = nodeScores[msg.targetNodeId];
                    byte newOwner = 0;
                    if (sc > 0) newOwner = 1;
                    else if (sc < 0) newOwner = 2;
                    nodeOwners[msg.targetNodeId] = newOwner;
                    
                    NodeUpdateMessage um = new NodeUpdateMessage { 
                        nodeId = msg.targetNodeId, 
                        score = nodeScores[msg.targetNodeId], 
                        owner = newOwner 
                    };
                    NetworkServer.SendToAll(um);
                    SendBonusResponse(conn, true, $"Атака нанесла урон {damage}!");
                    UpdateOwnedCountsAndNotify();
                    CheckVictoryCondition();
                }
                else
                {
                    SendBonusResponse(conn, false, "Недостаточно золота (требуется 40)");
                }
                break;
                
            default:
                SendBonusResponse(conn, false, "Неизвестный тип бонуса");
                break;
        }
    }
    
    private void SendBonusResponse(NetworkConnectionToClient conn, bool success, string message)
    {
        BonusResponseMessage resp = new BonusResponseMessage { success = success, message = message };
        conn.Send(resp);
    }
    
    // ---------------- victory condition ----------------
    private void CheckVictoryCondition()
    {
        if (gameState != 0) return; // game already over
        
        // count owned nodes for each player
        int player1Nodes = 0;
        int player2Nodes = 0;
        
        foreach (var kv in nodeOwners)
        {
            if (kv.Value == 1) player1Nodes++;
            else if (kv.Value == 2) player2Nodes++;
        }
        
        // check if any player lost all nodes
        if (player1Nodes == 0 && player2Nodes > 0)
        {
            gameState = 2; // Player 2 won
            winnerOwnerId = 2;
            playerEliminated[0] = true;
            Debug.Log("[Server] Player 2 WON! Player 1 eliminated.");
            BroadcastGameState();
        }
        else if (player2Nodes == 0 && player1Nodes > 0)
        {
            gameState = 1; // Player 1 won
            winnerOwnerId = 1;
            playerEliminated[1] = true;
            Debug.Log("[Server] Player 1 WON! Player 2 eliminated.");
            BroadcastGameState();
        }
    }

    // ---------------- gold tick ----------------
    IEnumerator GoldTickRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(goldTickInterval);

            // only run if server active
            if (!NetworkServer.active) continue;

            // compute gold per player index
            for (int i = 0; i < maxPlayers; i++) playerGold[i] = playerGold[i]; // ensure exists

            // accumulate
            foreach (var kv in nodeOwners)
            {
                int nodeId = kv.Key;
                byte owner = kv.Value;
                if (owner == 0) continue;
                int ownerIdx = (owner == 1) ? 0 : 1;
                
                // skip if player eliminated
                if (playerEliminated[ownerIdx]) continue;
                
                int sc = nodeScores.ContainsKey(nodeId) ? nodeScores[nodeId] : 0;
                int gain = goldPerNodeBase + Mathf.Abs(sc) / 5; // example formula
                gain = Mathf.Clamp(gain, 0, maxGoldPerNodePerTick);
                
                // apply gold multiplier bonus
                gain = Mathf.RoundToInt(gain * playerGoldMultiplier[ownerIdx]);
                
                playerGold[ownerIdx] += gain;
            }

            // send PlayerStatsMessage to each connected player
            for (int idx = 0; idx < maxPlayers; idx++)
            {
                if (indexToConn.TryGetValue(idx, out var conn))
                {
                    byte ownerId = (byte)(idx == 0 ? 1 : 2);
                    SendPlayerStatsToConn(conn, ownerId, playerGold[idx], GetOwnedCountByIndex(idx), playerClickPower[idx]);
                }
            }
        }
    }

    private void UpdateOwnedCountsAndNotify()
    {
        for (int i = 0; i < maxPlayers; i++) playerOwnedCount[i] = 0;
        foreach (var kv in nodeOwners)
        {
            int nodeId = kv.Key;
            byte owner = kv.Value;
            if (owner == 0) continue;
            int idx = (owner == 1) ? 0 : 1;
            playerOwnedCount[idx]++;
        }

        // notify connected players
        for (int idx = 0; idx < maxPlayers; idx++)
        {
            if (indexToConn.TryGetValue(idx, out var conn))
            {
                byte ownerId = (byte)(idx == 0 ? 1 : 2);
                SendPlayerStatsToConn(conn, ownerId, playerGold[idx], playerOwnedCount[idx], playerClickPower[idx]);
            }
        }
    }

    private int GetOwnedCountByIndex(int idx)
    {
        int count = 0;
        foreach (var kv in nodeOwners)
        {
            if (kv.Value == (byte)(idx == 0 ? 1 : 2)) count++;
        }
        return count;
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
