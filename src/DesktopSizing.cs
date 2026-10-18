using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
namespace GoogleNas {
    // Suspend our responsive layout for the entire recursive WinForms scale pass.
    // Layout callbacks must never resize a sibling that Windows has yet to scale.
    public partial class MainForm {
        private float desktopScale=1;
        private bool desktopScaleApplied;
        private static void CollectLayoutControls(Control parent,List<Control> controls) {
            controls.Add(parent); foreach(Control child in parent.Controls) CollectLayoutControls(child,controls);
        }
        internal void ApplyDesktopScale(float scale,bool simulateFontPixels) {
            if(scale<0.75f || scale>4) throw new ArgumentOutOfRangeException("scale");
            float ratio=scale/desktopScale; desktopScaleApplied=true;
            if(Math.Abs(ratio-1)<0.001f) { if(resizeSidebar!=null) resizeSidebar(); return; }
            var controls=new List<Control>(); CollectLayoutControls(this,controls);
            var fonts=new Dictionary<Control,Font>();
            if(simulateFontPixels) foreach(var control in controls) fonts[control]=control.Font;
            foreach(var control in controls) control.SuspendLayout();
            Theme.LayoutScaling=true;
            try {
                desktopScale=scale;
                Scale(new SizeF(ratio,ratio));
                // Real Windows DPI already renders point fonts at the correct pixel size.
                // Tests at 96 DPI use this option to reproduce the same font geometry.
                if(simulateFontPixels) foreach(var pair in fonts) pair.Key.Font=new Font(pair.Value.FontFamily,pair.Value.Size*ratio,pair.Value.Style);
            } finally {
                Theme.LayoutScaling=false;
                for(int i=controls.Count-1;i>=0;i--) controls[i].ResumeLayout(false);
            }
            PerformLayout();
            if(resizeSidebar!=null) resizeSidebar();
            foreach(var page in pages) { FitCards(page); page.PerformLayout(); }
            RoundWindow();
        }
    }
}
