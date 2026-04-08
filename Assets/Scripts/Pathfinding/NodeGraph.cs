using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[ExecuteAlways]
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

	[Header("Editor")]
	[Tooltip("If enabled, generates/refreshes the grid in Edit Mode (no Play required). Can be slow on big scenes.")]
	public bool GenerateInEditMode = true;

	void Start()
	{
		// In play mode we always generate at startup.
		if (Application.isPlaying)
			GenerateGrid();
	}

#if UNITY_EDITOR
	private void OnEnable()
	{
		// In Edit Mode: optionally auto-generate so gizmos can be seen without pressing Play.
		if (Application.isPlaying)
			return;
		if (!GenerateInEditMode)
			return;

		// Delay to let Unity finish loading scene objects/colliders before raycasts.
		UnityEditor.EditorApplication.delayCall += () =>
		{
			if (this == null) return;
			if (Application.isPlaying) return;
			if (!GenerateInEditMode) return;
			GenerateGrid();
		};
	}

	private void OnValidate()
	{
		if (Application.isPlaying)
			return;
		if (!GenerateInEditMode)
			return;

		// Debounced refresh when parameters change.
		UnityEditor.EditorApplication.delayCall += () =>
		{
			if (this == null) return;
			if (Application.isPlaying) return;
			if (!GenerateInEditMode) return;
			GenerateGrid();
		};
	}
#endif

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
#if UNITY_EDITOR
		InvalidateGizmoCache();
#endif
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
            	if (!OnSamePlatform(pos, newPos, out Vector3 groundPos, out float heightDiff)) continue;
				if (Physics.CheckSphere(groundPos + Vector3.up * NodeRaycastHeight, GridIncrement * 0.4f, ObstaclesLayerMask, QueryTriggerInteraction.Ignore)) continue;

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
                    float yDiff = Mathf.Abs(node.loc.y - adj.loc.y);
                    if (yDiff >= MinJumpHeight && yDiff <= MaxJumpHeight)
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

				// Reject if a wall stands between the two positions horizontally.
				Vector3 horizontal = newPos - pos;
				horizontal.y = 0f;
				float dist = horizontal.magnitude;
				Vector3 rayStart = new Vector3(pos.x, groundPos.y + NodeRaycastHeight, pos.z);
				if (dist > 0.001f
				    && Physics.Raycast(rayStart, horizontal.normalized, out RaycastHit wallHit, dist, TerrainLayerMask)
				    && wallHit.collider.CompareTag("Wall"))
					return false;

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
                if (Mathf.Abs(node.loc.y - adjNode.loc.y) <= MaxJumpHeight && !IsPathBlocked(node.loc, adjNode.loc))
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
        
        return Physics.Raycast(fromRay, direction.normalized, distance, ObstaclesLayerMask, QueryTriggerInteraction.Ignore);
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
		List<Node> affectedNodes = Nodes.Values
			.Where(n => Vector3.Distance(n.loc, position) <= radius)
			.ToList();

		float connectDist = GridIncrement * 2.0f;
		for (int i = 0; i < affectedNodes.Count; i++)
		{
			for (int j = i + 1; j < affectedNodes.Count; j++)
			{
				Node a = affectedNodes[i], b = affectedNodes[j];
				if (Vector3.Distance(a.loc, b.loc) > connectDist) continue;
				if (a.AdjList.Contains(b)) continue;
				if (!IsPathBlocked(a.loc, b.loc))
				{
					a.AdjList.Add(b);
					b.AdjList.Add(a);
				}
			}
		}
#if UNITY_EDITOR
		InvalidateGizmoCache();
#endif
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

				if (IsPathBlocked(nodeA.loc, nodeB.loc)) continue;

				nodeA.NodeType = NodeTypeEnum.Jumping;
				nodeB.NodeType = NodeTypeEnum.Jumping;

				if (!nodeA.AdjList.Contains(nodeB)) nodeA.AdjList.Add(nodeB);
				if (!nodeB.AdjList.Contains(nodeA)) nodeB.AdjList.Add(nodeA);
			}
		}
	}


#if UNITY_EDITOR
	[Header("Gizmos Parameters")]
	[Tooltip("If true, gizmos (nodes/edges) are only drawn while the editor is in Play Mode.")]
	public bool RequirePlayModeForGizmos = false;
	public bool ShowNodes = true;
	public bool ShowEdges = true;
	public Color NodeColor = Color.green;
	public Color JumpingNodeColor = Color.yellow;
	public Color EdgeColor = Color.cyan;
	public float NodeGizmoSize = 0.3f;
	public float EdgeThickness = 1.0f;
	[Tooltip("Limits how often gizmos are drawn (reduces Scene view slowdown). 0 = no throttling.")]
	public float GizmoDrawHz = 15f;
	[Header("Edge draw performance")]
	[Tooltip("Draw every Nth edge (1 = draw all). Higher values reduce cost a lot.")]
	public int EdgeDrawStride = 1;
	[Tooltip("If > 0, only draw edges whose midpoint is within this distance from the SceneView camera.")]
	public float EdgeMaxCameraDistance = 0f;

	private Vector3[] _gizmoNodePositions;
	private NodeTypeEnum[] _gizmoNodeTypes;
	private (Vector3 a, Vector3 b)[] _gizmoEdges;
	private Vector3[] _gizmoEdgeSegments;
	private bool _gizmoCacheDirty = true;
	private double _nextAllowedGizmoDrawTime;

	public void InvalidateGizmoCache() => _gizmoCacheDirty = true;

	void RebuildGizmoCache()
	{
		var nodeList = new List<Node>(Nodes.Values);
		_gizmoNodePositions = new Vector3[nodeList.Count];
		_gizmoNodeTypes = new NodeTypeEnum[nodeList.Count];

		var edges = new List<(Vector3, Vector3)>();
		var seen = new HashSet<(Node, Node)>();

		for (int i = 0; i < nodeList.Count; i++)
		{
			_gizmoNodePositions[i] = nodeList[i].loc;
			_gizmoNodeTypes[i] = nodeList[i].NodeType;
			foreach (var adj in nodeList[i].AdjList)
			{
				if (seen.Contains((adj, nodeList[i]))) continue;
				seen.Add((nodeList[i], adj));
				edges.Add((nodeList[i].loc, adj.loc));
			}
		}

		_gizmoEdges = edges.ToArray();
		// Prebuild segment array for batch drawing (a0,b0,a1,b1,...)
		_gizmoEdgeSegments = new Vector3[_gizmoEdges.Length * 2];
		for (int i = 0; i < _gizmoEdges.Length; i++)
		{
			_gizmoEdgeSegments[i * 2] = _gizmoEdges[i].a;
			_gizmoEdgeSegments[i * 2 + 1] = _gizmoEdges[i].b;
		}
		_gizmoCacheDirty = false;
	}

	void OnDrawGizmos()
	{
		if (RequirePlayModeForGizmos && !Application.isPlaying)
			return;
		if (Nodes == null || Nodes.Count == 0) return;

		// Throttle gizmo drawing to reduce editor slowdown when many nodes exist.
		// Note: this skips whole-frame drawing (nodes+edges), which is fine for visualization.
		if (GizmoDrawHz > 0f)
		{
			double now = UnityEditor.EditorApplication.timeSinceStartup;
			if (now < _nextAllowedGizmoDrawTime)
				return;
			_nextAllowedGizmoDrawTime = now + (1.0 / Mathf.Max(0.01f, GizmoDrawHz));
		}

		if (_gizmoCacheDirty || _gizmoNodePositions == null) RebuildGizmoCache();

		Vector3 offset = new Vector3(0, 0.1f, 0);

		// Make node/edge visualization respect scene depth (no x-ray through walls).
		var prevZTest = UnityEditor.Handles.zTest;
		UnityEditor.Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

		if (ShowEdges)
		{
			UnityEditor.Handles.color = EdgeColor;
			// Drawing edges one-by-one is very expensive for large graphs.
			// Batch draw via DrawLines, with optional stride and camera-distance culling.
			if (_gizmoEdgeSegments != null && _gizmoEdgeSegments.Length >= 2)
			{
				int stride = Mathf.Max(1, EdgeDrawStride);

				Vector3 camPos = default;
				float maxDist = EdgeMaxCameraDistance;
				float maxDistSqr = maxDist > 0f ? maxDist * maxDist : 0f;

				if (maxDist > 0f && UnityEditor.SceneView.lastActiveSceneView != null)
					camPos = UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;

				// If no culling and stride==1, draw everything in one call.
				if (stride == 1 && maxDist <= 0f)
				{
					// Apply offset by drawing a temporary shifted array (avoid mutating cache).
					var segs = new Vector3[_gizmoEdgeSegments.Length];
					for (int i = 0; i < _gizmoEdgeSegments.Length; i++)
						segs[i] = _gizmoEdgeSegments[i] + offset;
					UnityEditor.Handles.DrawLines(segs);
				}
				else
				{
					// Filtered draw: build a smaller segment list.
					var segs = new List<Vector3>(_gizmoEdgeSegments.Length / (stride * 2));
					for (int e = 0; e < _gizmoEdges.Length; e += stride)
					{
						var (a, b) = _gizmoEdges[e];
						if (maxDist > 0f)
						{
							Vector3 mid = (a + b) * 0.5f;
							if ((mid - camPos).sqrMagnitude > maxDistSqr)
								continue;
						}
						segs.Add(a + offset);
						segs.Add(b + offset);
					}

					if (segs.Count >= 2)
						UnityEditor.Handles.DrawLines(segs.ToArray());
				}
			}
		}

		if (ShowNodes)
		{
			for (int i = 0; i < _gizmoNodePositions.Length; i++)
			{
				UnityEditor.Handles.color = _gizmoNodeTypes[i] == NodeTypeEnum.Jumping ? JumpingNodeColor : NodeColor;
				// Cube is cheaper than sphere for large node counts. Use Handles so depth test applies.
				float s = Mathf.Max(0.001f, NodeGizmoSize * 2f);
				UnityEditor.Handles.CubeHandleCap(
					controlID: 0,
					position: _gizmoNodePositions[i] + offset,
					rotation: Quaternion.identity,
					size: s,
					eventType: UnityEngine.EventType.Repaint);
			}
		}

		UnityEditor.Handles.zTest = prevZTest;
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