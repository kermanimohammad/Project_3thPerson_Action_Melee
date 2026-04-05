using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class Pathfinding
{
    private class NodePriorityElement
    {
        public Node Node;
        public float Priority;

        public NodePriorityElement(Node node, float priority)
        {
            Node = node;
            Priority = priority;
        }
    }

    public static List<Node> FindPath(Node start, Node goal, Vector3 heuristicTarget)
    {
        if (start == null || goal == null)
            return null;

        var openSet = new PriorityQueue<Node, float>();
        var openSetLookup = new HashSet<Node>();
        var closedSet = new HashSet<Node>();
        var cameFrom = new Dictionary<Node, Node>();
        var gScore = new Dictionary<Node, float>();
        var fScore = new Dictionary<Node, float>();

        gScore[start] = 0;
        fScore[start] = Heuristic(start.loc, heuristicTarget);
        openSet.Enqueue(start, fScore[start]);
        openSetLookup.Add(start);

        while (openSet.Count > 0)
        {
            Node current = openSet.Dequeue();
            openSetLookup.Remove(current);

            if (current == goal)
                return ReconstructPath(cameFrom, current);

            closedSet.Add(current);

            foreach (Node neighbor in current.AdjList)
            {
                if (neighbor.Removed || closedSet.Contains(neighbor))
                    continue;

                float tentativeGScore = gScore[current] + Vector3.Distance(current.loc, neighbor.loc);

                if (tentativeGScore < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + Heuristic(neighbor.loc, heuristicTarget);

                    if (!openSetLookup.Contains(neighbor))
                    {
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                        openSetLookup.Add(neighbor);
                    }
                }
            }
        }

        return null;
    }

    private static float Heuristic(Vector3 from, Vector3 to)
    {
        return Vector3.Distance(from, to);
    }

    private static List<Node> ReconstructPath(Dictionary<Node, Node> cameFrom, Node current)
    {
        var path = new List<Node> { current };
        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }
        return path;
    }

    public static Node FindNearestNode(Vector3 position, Dictionary<(int, int, int), Node> nodes, bool filter = false, NodeTypeEnum typeFilter = NodeTypeEnum.Normal)
    {
        return nodes.Values
            .Where(n => !n.Removed && (!filter || n.NodeType == typeFilter))
            .OrderBy(n => Vector3.Distance(position, n.loc))
            .FirstOrDefault();
    }

    public static void DebugPrintPath(List<Node> path)
    {
        string s = "Path: ";
        foreach(Node n in path)
            s += $"{n.loc} --> ";
        Debug.Log(s);
    }
}