using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NodeGraph : MonoBehaviour
{
	public List<Transform> StartingPositions;
	public float JumpingDistance;
	public Dictionary<(int,int,int),Node> Nodes;
	public float NodeRaycastHeight;
	public float PlatformCheckRaycastHeight;
	public LayerMask TerrainLayerMask;
	public LayerMask ObstaclesLayerMask;

	[Header("Grid Generation Parameters")]
	public float GridIncrement;
	public float SearchRadiusFactor;
	public int NAngles;
	public float SamePlatformThreshold;

	[Header("Jump Detection Parameters")]
	public float MaxJumpHeight;
	public float MinJumpHeight = 0.5f;

	void Start()
	{
		GenerateGrid();	
	}

	public NodeGraph()
	{
		Nodes = new();
	}

	public void GenerateGrid()
	{
		Nodes = new();
		StartingPositions = GameObject.FindGameObjectsWithTag("PlatformPosition").Select(go => go.transform).ToList();

		for (int i = 0; i < StartingPositions.Count; i++) FloodFillGrid(StartingPositions[i].position, i);
		GenerateThenCleanAdjacencies();
		FindJumpConnections();
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
		Debug.Log($"FloodFillGrid called for {root}");
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
            	if (!OnSamePlatform(pos, newPos, out Vector3 groundPos, out float heightDiff)) continue;

                (int, int) newIndex = (xIdx + indexOffsets[i].Item1, zIdx + indexOffsets[i].Item2);

                if (!visited.Contains(newIndex))
                {
                    visited.Add(newIndex);
                    queue.Enqueue((groundPos, platform, newIndex.Item1, newIndex.Item2));
                }
            }
        }

        foreach (var kvp in Nodes)
        {
            if (kvp.Key.Item1 != platform) continue;
            Node node = kvp.Value;
            foreach (var off in indexOffsets)
            {
                if (Nodes.TryGetValue((platform, kvp.Key.Item2 + off.Item1, kvp.Key.Item3 + off.Item2), out Node adj))
                {
                    if (Mathf.Abs(node.loc.y - adj.loc.y) >= MinJumpHeight)
                    {
                        node.NodeType = NodeTypeEnum.Jumping;
                        adj.NodeType = NodeTypeEnum.Jumping;
                    }
                }
            }
        }
	}

	bool OnSamePlatform(Vector3 pos, Vector3 newPos, out Vector3 groundPos, out float heightDiff)
	{
		groundPos = newPos;
		heightDiff = 0f;
		RaycastHit hit;
		Vector3 rayOrigin = newPos + Vector3.up * PlatformCheckRaycastHeight;
		if (Physics.Raycast(rayOrigin, Vector3.down, out hit, float.MaxValue, TerrainLayerMask))
		{
			if (hit.collider.CompareTag("Wall")) return false;

			// Check if an obstacle sits between the ray origin and the terrain hit
			if (Physics.Raycast(rayOrigin, Vector3.down, hit.distance, ObstaclesLayerMask))
				return false;

			heightDiff = Mathf.Abs(hit.point.y - pos.y);
			if (heightDiff < SamePlatformThreshold)
			{
				groundPos = new Vector3(newPos.x, hit.point.y, newPos.z);
				return true;
			}
		}
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

	List<Node> FindEdgeNodes()
	{
		(int, int)[] indexOffsets = { (1, 0), (-1, 0), (0, 1), (0, -1) };
		List<Node> edgeNodes = new();

		foreach (var kvp in Nodes)
		{
			(int platform, int x, int z) = kvp.Key;
			foreach (var offset in indexOffsets)
			{
				if (!Nodes.ContainsKey((platform, x + offset.Item1, z + offset.Item2)))
				{
					edgeNodes.Add(kvp.Value);
					break;
				}
			}
		}
		return edgeNodes;
	}

	// call this when a door is destroyed with the location of the door
	public void UpdateGridAroundPoint(Vector3 position, float radius)
	{
		(int, int)[] indexOffsets = { (1, 0), (-1, 0), (0, 1), (0, -1) };

		List<Node> affectedNodes = Nodes.Values
			.Where(n => Vector3.Distance(n.loc, position) <= radius)
			.ToList();

		foreach (var node in affectedNodes)
		{
			(int platform, int x, int z) = node.Index;
			foreach (var offset in indexOffsets)
			{
				if (!Nodes.TryGetValue((platform, x + offset.Item1, z + offset.Item2), out Node neighbor))
					continue;
				if (node.AdjList.Contains(neighbor))
					continue;
				if (!IsPathBlocked(node.loc, neighbor.loc))
				{
					node.AdjList.Add(neighbor);
					if (!neighbor.AdjList.Contains(node))
						neighbor.AdjList.Add(node);
				}
			}
		}
	}

	void FindJumpConnections()
	{
		List<Node> edgeNodes = FindEdgeNodes();

		foreach (var nodeA in edgeNodes)
		{
			foreach (var nodeB in Nodes.Values)
			{
				if (nodeB.Index.platform == nodeA.Index.platform) continue;

				float horizDist = new Vector2(nodeB.loc.x - nodeA.loc.x, nodeB.loc.z - nodeA.loc.z).magnitude;
				if (horizDist > JumpingDistance) continue;

				float vertDist = Mathf.Abs(nodeA.loc.y - nodeB.loc.y);
				if (vertDist > MaxJumpHeight) continue;

				nodeA.NodeType = NodeTypeEnum.Jumping;
				nodeB.NodeType = NodeTypeEnum.Jumping;

				if (!nodeA.AdjList.Contains(nodeB)) nodeA.AdjList.Add(nodeB);
				if (!nodeB.AdjList.Contains(nodeA)) nodeB.AdjList.Add(nodeA);
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
		Vector3 offset = new Vector3(0, 0.1f, 0);
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