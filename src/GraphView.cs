using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GitGraphLite
{
    // Draws only the visible rows: graph lanes, ref badges, subject, author, date, hash.
    public sealed class GraphView : UserControl
    {
        const int MaxLanes = 40;
        const TextFormatFlags TextFlags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
        static readonly Color[] Palette =
        {
            Color.FromArgb(0x1c, 0x7e, 0xd6), Color.FromArgb(0xe8, 0x59, 0x0c), Color.FromArgb(0x2f, 0x9e, 0x44),
            Color.FromArgb(0xc2, 0x25, 0x5c), Color.FromArgb(0x70, 0x48, 0xe8), Color.FromArgb(0x0c, 0x85, 0x99),
            Color.FromArgb(0xe6, 0x77, 0x00), Color.FromArgb(0x5c, 0x7c, 0xfa),
        };
        static readonly Color Muted = Color.FromArgb(0x6a, 0x73, 0x7d);

        List<Commit> commits = new List<Commit>();
        GraphLayout layout;
        int selected = -1, rowH, laneW;
        Pen[] pens = new Pen[0];
        Font bold;
        public event EventHandler SelectedChanged;

        public GraphView()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = Color.White;
            AutoScroll = true;
            ContextMenuStrip = BuildMenu();
            UpdateMetrics();
        }

        public Commit Selected
        {
            get { return selected >= 0 && selected < commits.Count ? commits[selected] : null; }
        }

        public void SetData(List<Commit> data, GraphLayout lay)
        {
            string keep = Selected == null ? null : Selected.Hash;
            commits = data;
            layout = lay;
            AutoScrollMinSize = new Size(0, commits.Count * rowH);
            int index = keep == null ? -1 : commits.FindIndex(c => c.Hash == keep);
            bool first = index < 0;
            if (first) index = commits.FindIndex(c => c.Refs.Exists(r => r.Current || r.Kind == RefKind.Head));
            if (index < 0 && commits.Count > 0) index = 0;
            selected = index;
            if (first) EnsureVisible(index);
            Invalidate();
            if (first) RaiseSelected();
        }

        public void SelectIndex(int index)
        {
            if (commits.Count == 0) return;
            index = Math.Max(0, Math.Min(commits.Count - 1, index));
            if (index == selected) return;
            selected = index;
            EnsureVisible(index);
            Invalidate();
            RaiseSelected();
        }

        void RaiseSelected()
        {
            var handler = SelectedChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        void EnsureVisible(int index)
        {
            if (index < 0) return;
            int top = -AutoScrollPosition.Y, y = index * rowH;
            if (y < top) AutoScrollPosition = new Point(0, y);
            else if (y + rowH > top + ClientSize.Height) AutoScrollPosition = new Point(0, y + rowH - ClientSize.Height);
        }

        void UpdateMetrics()
        {
            rowH = Font.Height + Font.Height / 2 + 2;
            laneW = Math.Max(10, (int)(rowH * 0.62));
            foreach (var p in pens) p.Dispose();
            pens = new Pen[Palette.Length];
            for (int i = 0; i < Palette.Length; i++) pens[i] = new Pen(Palette[i], Math.Max(1.5f, rowH / 11f));
            if (bold != null) bold.Dispose();
            bold = new Font(Font, FontStyle.Bold);
            AutoScrollMinSize = new Size(0, commits.Count * rowH);
            Invalidate();
        }

        ContextMenuStrip BuildMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("해시 복사 (Ctrl+C)", null, delegate { Copy(c => c.Hash); });
            menu.Items.Add("제목 복사", null, delegate { Copy(c => c.Subject); });
            return menu;
        }

        void Copy(Func<Commit, string> pick)
        {
            if (Selected != null) Clipboard.SetText(pick(Selected));
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            UpdateMetrics();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            if (commits.Count == 0 || layout == null)
            {
                TextRenderer.DrawText(g, "폴더를 열거나 창에 끌어다 놓으세요 (Ctrl+O)", Font, ClientRectangle, Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            int offset = -AutoScrollPosition.Y;
            int first = Math.Max(0, (e.ClipRectangle.Top + offset) / rowH);
            int last = Math.Min(commits.Count - 1, (e.ClipRectangle.Bottom + offset) / rowH);
            for (int i = first; i <= last; i++) PaintRow(g, i, i * rowH - offset);
        }

        void PaintRow(Graphics g, int index, int y)
        {
            var bounds = new Rectangle(0, y, ClientSize.Width, rowH);
            if (index == selected)
                using (var b = new SolidBrush(Focused ? Color.FromArgb(0xcc, 0xe4, 0xff) : Color.FromArgb(0xe6, 0xec, 0xf4)))
                    g.FillRectangle(b, bounds);
            PaintGraph(g, layout.Rows[index], commits[index], y);
            int lanes = Math.Min(layout.Width, MaxLanes);
            PaintText(g, commits[index], bounds, lanes * laneW + 10);
        }

        int LaneX(int lane)
        {
            return 6 + laneW / 2 + Math.Min(lane, MaxLanes - 1) * laneW;
        }

        Pen LanePen(int lane)
        {
            return pens[lane % pens.Length];
        }

        void PaintGraph(Graphics g, Row row, Commit c, int y)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int mid = y + rowH / 2, bottom = y + rowH;
            foreach (int i in row.Through) g.DrawLine(LanePen(i), LaneX(i), y, LaneX(i), bottom);
            foreach (var e in row.Top) g.DrawLine(LanePen(e.From), LaneX(e.From), y, LaneX(row.Lane), mid);
            foreach (var e in row.Bottom) g.DrawLine(LanePen(e.To), LaneX(row.Lane), mid, LaneX(e.To), bottom);
            PaintDot(g, row.Lane, c, mid);
            g.SmoothingMode = SmoothingMode.None;
        }

        void PaintDot(Graphics g, int lane, Commit c, int mid)
        {
            float r = rowH * 0.22f, x = LaneX(lane);
            var rect = new RectangleF(x - r, mid - r, r * 2, r * 2);
            var pen = LanePen(lane);
            bool merge = c.Parents.Length > 1;
            using (var fill = new SolidBrush(merge ? Color.White : pen.Color)) g.FillEllipse(fill, rect);
            g.DrawEllipse(pen, rect);
            if (c.Refs.Exists(x2 => x2.Current || x2.Kind == RefKind.Head))
            {
                rect.Inflate(r * 0.55f, r * 0.55f);
                g.DrawEllipse(pen, rect);
            }
        }

        void PaintText(Graphics g, Commit c, Rectangle row, int x)
        {
            int hashW = TextRenderer.MeasureText("00000000", Font).Width;
            int dateW = TextRenderer.MeasureText("2026-10-11 12:00  ", Font).Width;
            int authorW = TextRenderer.MeasureText("MMMMMMMMMMMM", Font).Width;
            bool wide = row.Width - x > hashW + dateW + authorW + 200;
            int right = wide ? row.Width - hashW - dateW - authorW - 12 : row.Width - 8;
            x = PaintRefs(g, c, x, row.Y, right);
            DrawCell(g, c.Subject, Font, x, right, row, ForeColor);
            if (!wide) return;
            DrawCell(g, c.Author, Font, right + 8, right + 8 + authorW, row, Muted);
            DrawCell(g, c.Date, Font, right + 8 + authorW, right + 8 + authorW + dateW, row, Muted);
            DrawCell(g, c.Hash.Substring(0, Math.Min(7, c.Hash.Length)), Font, row.Width - hashW - 4, row.Width - 4, row, Muted);
        }

        static void DrawCell(Graphics g, string text, Font font, int left, int right, Rectangle row, Color color)
        {
            if (right <= left) return;
            TextRenderer.DrawText(g, text, font, new Rectangle(left, row.Y, right - left, row.Height), color, TextFlags);
        }

        int PaintRefs(Graphics g, Commit c, int x, int y, int right)
        {
            foreach (var r in c.Refs)
            {
                var font = r.Current || r.Kind == RefKind.Head ? bold : Font;
                int w = TextRenderer.MeasureText(g, r.Name, font, new Size(int.MaxValue, rowH), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + 4;
                if (x + w > right) break;
                var rect = new Rectangle(x, y + 3, w, rowH - 6);
                using (var b = new SolidBrush(BadgeColor(r))) g.FillRectangle(b, rect);
                TextRenderer.DrawText(g, r.Name, font, rect, Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                x += w + 4;
            }
            return x;
        }

        static Color BadgeColor(GitRef r)
        {
            switch (r.Kind)
            {
                case RefKind.Head: return Color.FromArgb(0xc9, 0x2a, 0x2a);
                case RefKind.Tag: return Color.FromArgb(0xd9, 0x7a, 0x00);
                case RefKind.Remote: return Color.FromArgb(0x86, 0x8e, 0x96);
                default: return r.Current ? Color.FromArgb(0x18, 0x64, 0xab) : Color.FromArgb(0x2f, 0x9e, 0x44);
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            int index = (e.Y - AutoScrollPosition.Y) / rowH;
            if (index < commits.Count) SelectIndex(index);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.PageUp: case Keys.PageDown: case Keys.Home: case Keys.End: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int page = Math.Max(1, ClientSize.Height / rowH - 1);
            switch (e.KeyCode)
            {
                case Keys.Up: SelectIndex(selected - 1); break;
                case Keys.Down: SelectIndex(selected + 1); break;
                case Keys.PageUp: SelectIndex(selected - page); break;
                case Keys.PageDown: SelectIndex(selected + page); break;
                case Keys.Home: SelectIndex(0); break;
                case Keys.End: SelectIndex(commits.Count - 1); break;
                case Keys.C: if (e.Control) Copy(c => c.Hash); break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
        }

        protected override void OnScroll(ScrollEventArgs se) { base.OnScroll(se); Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var p in pens) p.Dispose();
                if (bold != null) bold.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
