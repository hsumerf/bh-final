using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BrailleUrdu
{
    public class PageNumberBox : UserControl
    {
        private const int PAD = 6;

        private string     _displayText         = "#";
        private bool       _dragging;
        private Point      _mouseDownScreen;
        private Point      _startLocation;
        private Dictionary<Control, Point> _groupStartLocations;

        public bool   IsSelected  { get; set; }
        public string DisplayText => _displayText;

        public PageNumberBox()
        {
            SetStyle(
                ControlStyles.UserPaint              |
                ControlStyles.AllPaintingInWmPaint   |
                ControlStyles.Selectable             |
                ControlStyles.SupportsTransparentBackColor, true);
            SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
            BackColor   = Color.Transparent;
            TabStop     = true;
            MinimumSize = new Size(24, 22);
            Size        = new Size(24, 22);
        }

        // Called by CanvasPanel.PageChanged() each time the active page changes.
        // pageIndex < 0 means we are viewing a master page → show placeholder.
        public void UpdateNumber(int pageIndex)
        {
            _displayText = pageIndex < 0 ? "#" : (pageIndex + 1).ToString();
            FitSize();
            Invalidate();
        }

        private void FitSize()
        {
            if (!IsHandleCreated) return;
            try
            {
                using (var font = MakeFont())
                using (var g    = CreateGraphics())
                {
                    var sz = g.MeasureString(_displayText, font);
                    Size = new Size(
                        Math.Max(MinimumSize.Width,  (int)sz.Width  + PAD * 2),
                        Math.Max(MinimumSize.Height, (int)sz.Height + PAD));
                }
            }
            catch { }
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null) FitSize();
        }

        private static Font MakeFont() =>
            new Font("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);

        // ── Transparency ──────────────────────────────────────────────────────
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x20; return cp; }
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x84 && m.Result == (IntPtr)(-1)) m.Result = (IntPtr)1;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.None;

            using (var font  = MakeFont())
            using (var brush = new SolidBrush(Color.Black))
            {
                var fmt = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(_displayText, font, brush,
                    new RectangleF(0, 0, Width, Height), fmt);
            }

            if (Focused)
            {
                using (var pen = new Pen(Color.DodgerBlue, 2f))
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else if (IsSelected)
            {
                using (var pen = new Pen(Color.DodgerBlue, 1.5f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
            else
            {
                using (var pen = new Pen(Color.FromArgb(160, 160, 160), 1f) { DashStyle = DashStyle.Dot })
                    g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            }
        }

        // ── Mouse (drag only — no resize) ─────────────────────────────────────
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _mouseDownScreen = PointToScreen(e.Location);
            _startLocation   = Location;
            _dragging        = true;
            Capture          = true;

            var canvas = Parent as CanvasPanel;
            if (canvas != null && IsSelected && canvas.SelectedControls.Count > 1)
            {
                _groupStartLocations = new Dictionary<Control, Point>();
                foreach (var c in canvas.SelectedControls)
                    _groupStartLocations[c] = c.Location;
            }
            else
                _groupStartLocations = null;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging) return;
            var screen = PointToScreen(e.Location);
            int dx = screen.X - _mouseDownScreen.X;
            int dy = screen.Y - _mouseDownScreen.Y;

            if (_groupStartLocations != null)
            {
                foreach (var kvp in _groupStartLocations)
                    kvp.Key.Location = new Point(
                        Math.Max(0, kvp.Value.X + dx),
                        Math.Max(0, kvp.Value.Y + dy));
            }
            else
            {
                Location = new Point(
                    Math.Max(0, _startLocation.X + dx),
                    Math.Max(0, _startLocation.Y + dy));
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _dragging            = false;
            _groupStartLocations = null;
            Capture              = false;
        }

        // ── Keyboard ──────────────────────────────────────────────────────────
        protected override bool IsInputKey(Keys keyData) =>
            keyData == Keys.Delete || keyData == Keys.Back;

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
            {
                BeginInvoke((Action)(() => { Parent?.Controls.Remove(this); Dispose(); }));
                e.Handled = true;
            }
        }

        // ── Focus ─────────────────────────────────────────────────────────────
        protected override void OnGotFocus(EventArgs e)  { base.OnGotFocus(e);  Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}
