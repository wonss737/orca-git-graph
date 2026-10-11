using System;
using System.Collections.Generic;

namespace GitGraphLite
{
    public struct Edge
    {
        public int From, To;
        public Edge(int from, int to) { From = from; To = to; }
    }

    // One row per commit: the lane of its dot, the lines passing by, entering from above and leaving below.
    public sealed class Row
    {
        public int Lane;
        public readonly List<int> Through = new List<int>();
        public readonly List<Edge> Top = new List<Edge>();
        public readonly List<Edge> Bottom = new List<Edge>();
    }

    public sealed class GraphLayout
    {
        public List<Row> Rows;
        public int Width;

        public static GraphLayout Build(IList<Commit> commits)
        {
            var lanes = new List<string>();
            var layout = new GraphLayout { Rows = new List<Row>(commits.Count) };
            foreach (var c in commits) layout.Rows.Add(BuildRow(lanes, c));
            int width = 0;
            foreach (var r in layout.Rows) width = Math.Max(width, RowWidth(r));
            layout.Width = width + 1;
            return layout;
        }

        static Row BuildRow(List<string> lanes, Commit c)
        {
            var row = new Row();
            var matches = new List<int>();
            for (int i = 0; i < lanes.Count; i++)
            {
                if (lanes[i] == c.Hash) matches.Add(i);
                else if (lanes[i] != null) row.Through.Add(i);
            }
            row.Lane = matches.Count > 0 ? matches[0] : FreeSlot(lanes);
            foreach (int i in matches) { lanes[i] = null; row.Top.Add(new Edge(i, row.Lane)); }
            Set(lanes, row.Lane, null);
            for (int n = 0; n < c.Parents.Length; n++)
                row.Bottom.Add(new Edge(row.Lane, ParentLane(lanes, c.Parents[n], n == 0 ? row.Lane : -1)));
            while (lanes.Count > 0 && lanes[lanes.Count - 1] == null) lanes.RemoveAt(lanes.Count - 1);
            return row;
        }

        static int RowWidth(Row r)
        {
            int w = r.Lane;
            foreach (int i in r.Through) w = Math.Max(w, i);
            foreach (var e in r.Top) w = Math.Max(w, e.From);
            foreach (var e in r.Bottom) w = Math.Max(w, e.To);
            return w;
        }

        static int FreeSlot(List<string> lanes)
        {
            int i = lanes.IndexOf(null);
            return i >= 0 ? i : lanes.Count;
        }

        // A parent's line goes to the lane already waiting for it, else the preferred lane (first parent), else a free lane.
        static int ParentLane(List<string> lanes, string hash, int preferred)
        {
            int waiting = lanes.IndexOf(hash);
            if (waiting >= 0) return waiting;
            int slot = preferred >= 0 ? preferred : FreeSlot(lanes);
            Set(lanes, slot, hash);
            return slot;
        }

        static void Set(List<string> lanes, int i, string value)
        {
            while (lanes.Count <= i) lanes.Add(null);
            lanes[i] = value;
        }
    }
}
