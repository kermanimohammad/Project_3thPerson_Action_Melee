using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NodeGraph : MonoBehaviour
{
	public List<Transform> StartingPositions;
	public List<Transform> JumpingPositions;
	public float JumpingDistance;
	public Dictionary<(int,int,int),Node> Nodes;
	public float NodeRaycastHeight = 0.5f;
	public float PlatformCheckRaycastHeight = 50.0f;
	public LayerMask TerrainLayerMask;
	public LayerMask ObstaclesLayerMask;

	[Header("Grid Generation Parameters")]
	public float GridIncrement = 1.0f;
	public float SearchRadiusFactor;
	public int NAngles;
	public float SamePlatformThreshold = 0.1f;

	public NodeGraph()
	{
		Nodes = new();
	}

	public void GenerateGrid()
	{
		Nodes = new();
		StartingPositions = GameObject.FindGameObjectsWithTag("PlatformPosition").Select(go => go.transform).ToList();
		JumpingPositions = GameObject.FindGameObjectsWithTag("JumpingPosition").Select(go => go.transform).ToList();

		for (int i = 0; i < StartingPositions.Count; i++) FloodFillGrid(StartingPositions[i].position, i);
		GenerateThenCleanAdjacencies();
		ConnectJumpingSpots();
	}

	void GenerateThenCleanAdjacencies()
	{
		FindAdjacencyLists();
		CleanAdjacencyLists();
		RemoveOrphanNodes();
	}

	// BFS flood fill
	void FloodFillGrid(Vector3 root, int platform)
	{
		Queue<(Vector3 position, int rootIndex, int xIndex, int zIndex)> queue = new();
        HashSet<(int, int)> visited = new();
        
        queue.Enqueue((root, platform, 0, 0));
        visited.Add((0, 0));

        Vector3[] directions = new Vector3[]
        {
            new Vector3(GridIncrement, 0, 0),   // +X
            new Vector3(-GridIncrement, 0, 0),  // -X
            new Vector3(0, 0, GridIncrement),   // +Z
            new Vector3(0, 0, -GridIncrement)   // -Z
        };
            
        (int, int)[] indexOffsets = new (int, int)[]
        {
            (1, 0),   // +X
            (-1, 0),  // -X
            (0, 1),   // +Z
            (0, -1)   // -Z
        }; 

        while (queue.Count > 0)
        {
            var (pos, _, xIdx, zIdx) = queue.Dequeue();

			Node node = new Node(pos, (platform, xIdx, zIdx));

			Nodes[(platform, xIdx, zIdx)] = node;
            
            for (int i = 0; i < directions.Length; i++)
            {
                Vector3 newPos = pos + directions[i];
            	if (!OnSamePlatform(pos, newPos)) continue;

                (int, int) newIndex = (xIdx + indexOffsets[i].Item1, zIdx + indexOffsets[i].Item2);
                
                if (!visited.Contains(newIndex))
                {
                    visited.Add(newIndex);
                    queue.Enqueue((newPos, platform, newIndex.Item1, newIndex.Item2));
                }
            }
        }
	}

	bool OnSamePlatform(Vector3 pos, Vector3 newPos)
	{
		RaycastHit hit;
		if (Physics.Raycast(newPos + Vector3.up * PlatformCheckRaycastHeight,
							Vector3.down,
							out hit,
							float.MaxValue,
							TerrainLayerMask))
			return Mathf.Abs(hit.point.y - pos.y) < SamePlatformThreshold;
		return false;
	}

	void FindAdjacencyLists()
	{
		(int, int)[] indexOffsets = new (int, int)[]
        {
            (1, 0),   // +X
            (-1, 0),  // -X
            (0, 1),   // +Z
            (0, -1)   // -Z
        }; 

        foreach (var kvp in Nodes)
        {
            (int platform, int x, int z) = kvp.Key;
            Node node = kvp.Value;
            
            foreach (var adjIndex in indexOffsets)
            {
                if (Nodes.TryGetValue((platform, x + adjIndex.Item1, z + adjIndex.Item2), out Node adjNode))
					node.AdjList.Add(adjNode);
            }
        }
	}

	void CleanAdjacencyLists()
	{
		foreach (var node in Nodes.Values)
        {
            List<Node> validAdjacents = new();
            
            foreach (var adjNode in node.AdjList)
            {
                if (!IsPathBlocked(node.loc, adjNode.loc))
                    validAdjacents.Add(adjNode);
            }
            
            node.AdjList = validAdjacents;
        }
	}

	bool IsPathBlocked(Vector3 from, Vector3 to)
	{
        Vector3 fromRay = from + Vector3.up * NodeRaycastHeight;
        Vector3 toRay = to + Vector3.up * NodeRaycastHeight;
        
        Vector3 direction = toRay - fromRay;
        float distance = direction.magnitude;
        
        return Physics.Raycast(fromRay, direction.normalized, distance, ObstaclesLayerMask);
	}

	void RemoveOrphanNodes()
	{
		List<(int, int,int)> toRemove = new();
		foreach(var kvp in Nodes)
		{
			if (kvp.Value.AdjList.Count == 0)
			{
				toRemove.Add(kvp.Key);
				kvp.Value.Removed = true;
				Destroy(kvp.Value.NodeObject);
			}
		}

		foreach (var index in toRemove) Nodes.Remove(index);
	}

	// adds nodes around obstacles, need to test in-game
	// requires ObstacleInformation component with "Offset" value and Obstacle tag
	void AddObstacleNodes()
	{
    	GameObject[] obstacles = GameObject.FindGameObjectsWithTag("Obstacle");
		
    
    	int safetyNodeIndex = 10_000;
    	int angleSamples = NAngles;
    	float samplingHeight = 1f;
    
    	foreach (var obstacle in obstacles)
    	{
			Collider[] colliders = obstacle.GetComponents<Collider>();
			ObstacleInformation obstacleInformation = obstacle.GetComponent<ObstacleInformation>();
				
        	if (colliders.Length == 0)
        	{
            	Debug.LogWarning($"Barrier {obstacle.name} has no collider, skipping adding obstacle nodes");
            	continue;
        	}

			// choose tightest collider
			Collider obstacleCollider = colliders.OrderBy(col => col.bounds.size.x * col.bounds.size.z).First();
        
        	Bounds bounds = obstacleCollider.bounds;
        	Vector3 center = bounds.center;
        	float searchRadius = Mathf.Max(bounds.size.x, bounds.size.z) * SearchRadiusFactor;

        	for (int i = 0; i < angleSamples; i++)
        	{
            	float angle = (360f / angleSamples) * i;
            	float radians = angle * Mathf.Deg2Rad;
            	Vector3 direction = new Vector3(Mathf.Cos(radians), 0, Mathf.Sin(radians));
            
            	Vector3 rayStart = center + direction * searchRadius;
            	rayStart.y = center.y + samplingHeight;
            
            	Vector3 rayDirection = -direction;
            
				RaycastHit[] hits = Physics.RaycastAll(rayStart, rayDirection, searchRadius * 2f, ObstaclesLayerMask);
				RaycastHit? targetHit = hits.First(h => h.collider == obstacleCollider);

				if (targetHit.HasValue)
				{
					RaycastHit hit = targetHit.Value;

                    Vector3 safetyPos = hit.point - rayDirection * obstacleInformation.Offset;
                    
                    RaycastHit groundHit;
                    if (!Physics.Raycast(safetyPos + Vector3.up * 10f, Vector3.down, out groundHit, 20f, TerrainLayerMask)) continue;
                    
					int safetyNodePlatform = Pathfinding.GetPlatform(safetyPos, Nodes, false);
                    Node safetyNode = new Node(safetyPos, (safetyNodePlatform, safetyNodeIndex, 0), NodeTypeEnum.Obstacle);
                    Nodes[(safetyNodePlatform, safetyNodeIndex, 0)] = safetyNode;
                    safetyNodeIndex++;
                }
        	}
    	}
	}

	void ConnectJumpingSpots()
	{
		int parity = 50_000;
		for (int i = 0; i < JumpingPositions.Count; i++)
		{
			for (int j = i+1; j < JumpingPositions.Count; j++)
			{
				if (Vector3.Distance(JumpingPositions[i].position, JumpingPositions[j].position) > JumpingDistance) continue;
				else
				{
					// find nearest "Normal" node and make sure the two jumping nodes are on different platforms
					Node nearestNodeI = Pathfinding.FindNearestNode(JumpingPositions[i].position, Nodes, filter : true);
					Node nearestNodeJ = Pathfinding.FindNearestNode(JumpingPositions[j].position, Nodes, filter : true);
					if (nearestNodeI.Index.platform == nearestNodeJ.Index.platform) continue;
				
					// add new nodes to Nodes dictionary
					Node nodeI = new Node(JumpingPositions[i].position, (nearestNodeI.Index.platform, parity, 0));
					Node nodeJ = new Node(JumpingPositions[j].position, (nearestNodeJ.Index.platform, parity, 0));
					Nodes.Add((nearestNodeI.Index.platform, parity, 0), nodeI);
					Nodes.Add((nearestNodeJ.Index.platform, parity, 0), nodeJ);
					parity++;
					
					// add edge between 2 jump nodes
					nodeI.AdjList.Add(nodeJ);
					nodeJ.AdjList.Add(nodeI);

					// add edges to normal nodes on the same platform
					nodeI.AdjList.AddRange(Nodes.Values.Where(n => n.Index.platform == nodeI.Index.platform && Vector3.Distance(n.loc, nodeI.loc) < GridIncrement));
					nodeJ.AdjList.AddRange(Nodes.Values.Where(n => n.Index.platform == nodeJ.Index.platform && Vector3.Distance(n.loc, nodeJ.loc) < GridIncrement));
				}
			}
		}
	}


#if UNITY_EDITOR
	[Header("Gizmos Parameters")]
	public bool ShowNodes = true;
	public bool ShowEdges = true;
	public Color NodeColor = Color.green;
	public Color JumpingNodeColor = Color.yellow;
	public Color EdgeColor = Color.cyan;
	public float NodeGizmoSize = 0.3f;
	public float EdgeThickness = 1.0f;

	void OnDrawGizmos()
	{
		Vector3 offset = new Vector3(0, 3.0f, 0);
    	if (Nodes == null || Nodes.Count == 0)
        	return;

    	if (ShowEdges)
    	{
			UnityEditor.Handles.color = EdgeColor;
        	Gizmos.color = EdgeColor;
        	foreach (var node in Nodes.Values)
        	{
            	foreach (var adjacentNode in node.AdjList)
                	UnityEditor.Handles.DrawLine(node.loc + offset, adjacentNode.loc + offset, EdgeThickness);
        	}
    	}

    	if (ShowNodes)
    	{
        	foreach (var node in Nodes.Values)
        	{
            	Gizmos.color = node.NodeType == NodeTypeEnum.Jumping ? JumpingNodeColor : NodeColor;
            	Gizmos.DrawSphere(node.loc + offset, NodeGizmoSize);
        	}
    	}
	}
#endif
}

public enum NodeTypeEnum
{
	Normal,
	Jumping,
	Obstacle
}

public class Node
{
	public GameObject NodeObject;
	public Vector3 loc;
	public (int platform, int x, int z) Index;
	public NodeTypeEnum NodeType;
	public List<Node> AdjList;
	public bool Removed = false;

	public Node() => throw new MethodAccessException("Use Node(Vector3, (int,int), NodeTypeEnum)");
	public Node(Vector3 loc, (int platform, int x, int z) index,  NodeTypeEnum nodeType = NodeTypeEnum.Normal)
	{
		this.loc = loc;
		Index = index;
		NodeType = nodeType;	
		AdjList = new();
	}
}