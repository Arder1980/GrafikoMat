using System;
using System.Collections.Generic;

namespace GrafikoMat.Core.Scheduling.Engines.Algorithms
{
    /// <summary>
    /// Implementacja algorytmu Dinica do znajdowania maksymalnego przepływu w sieci.
    /// Używany do obliczania górnych ograniczeń (upper bounds) w solverach.
    /// Wersja zaadaptowana z GrafikWPF.
    /// </summary>
    internal sealed class MaxFlowDinic
    {
        private readonly int _nodeCount;
        private readonly List<Edge>[] _graph;
        private int[] _level;
        private int[] _iterator;

        private sealed class Edge
        {
            public int To, ReverseEdgeIndex, Capacity;
            public Edge(int to, int rev, int cap) { To = to; ReverseEdgeIndex = rev; Capacity = cap; }
        }

        public MaxFlowDinic(int nodeCount)
        {
            _nodeCount = nodeCount;
            _graph = new List<Edge>[nodeCount];
            for (int i = 0; i < nodeCount; i++) _graph[i] = new List<Edge>(8);
            _level = new int[nodeCount];
            _iterator = new int[nodeCount];
        }

        public void AddEdge(int u, int v, int capacity)
        {
            var forwardEdge = new Edge(v, _graph[v].Count, capacity);
            var backwardEdge = new Edge(u, _graph[u].Count, 0); // Krawędź zwrotna o zerowej pojemności
            _graph[u].Add(forwardEdge);
            _graph[v].Add(backwardEdge);
        }

        private bool Bfs(int s, int t)
        {
            Array.Fill(_level, -1);
            var q = new Queue<int>();
            _level[s] = 0;
            q.Enqueue(s);
            while (q.Count > 0)
            {
                int v = q.Dequeue();
                foreach (var e in _graph[v])
                {
                    if (e.Capacity <= 0 || _level[e.To] >= 0) continue;

                    _level[e.To] = _level[v] + 1;
                    if (e.To == t) return true;
                    q.Enqueue(e.To);
                }
            }
            return _level[t] >= 0;
        }

        private int Dfs(int v, int t, int f)
        {
            if (v == t) return f;

            for (int i = _iterator[v]; i < _graph[v].Count; i++, _iterator[v] = i)
            {
                var e = _graph[v][i];
                if (e.Capacity <= 0 || _level[v] + 1 != _level[e.To]) continue;

                int d = Dfs(e.To, t, Math.Min(f, e.Capacity));
                if (d <= 0) continue;

                e.Capacity -= d;
                _graph[e.To][e.ReverseEdgeIndex].Capacity += d;
                return d;
            }
            return 0;
        }

        public int GetMaxFlow(int s, int t, int requiredFlow = int.MaxValue)
        {
            int flow = 0;
            while (flow < requiredFlow && Bfs(s, t))
            {
                Array.Fill(_iterator, 0);
                int f;
                while (flow < requiredFlow && (f = Dfs(s, t, requiredFlow - flow)) > 0)
                {
                    flow += f;
                }
            }
            return flow;
        }
    }
}