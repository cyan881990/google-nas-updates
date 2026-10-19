using System;
using System.Drawing;
using System.Windows.Forms;
namespace GoogleNas {
    // Explicit action layout: equal cells, fixed height, no caption-driven wrapping.
    public sealed class ActionBar : FlowLayoutPanel {
        private readonly Func<float> factor;
        private bool arranging;
        public bool StackWhenNarrow { get; set; }
        public ActionBar(Func<float> scale) { factor=scale; WrapContents=false; AutoSize=false; Margin=Padding.Empty; Padding=Padding.Empty; }
        public int RequiredHeight(int width) {
            float s=factor==null?1:factor(); int count=Controls.Count;
            bool stack=StackWhenNarrow && count>1 && width<(128*count+10*(count-1))*s;
            return (int)Math.Round((stack?40*count+10*(count-1):40)*s);
        }
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e); if(arranging || factor==null || Theme.LayoutScaling || Controls.Count==0) return; arranging=true;
            try {
                float s=factor(); int count=Controls.Count,gap=(int)Math.Round(10*s),height=(int)Math.Round(40*s);
                bool stack=StackWhenNarrow && RequiredHeight(ClientSize.Width)>height;
                int width=Math.Max(1,Math.Min((int)Math.Round(184*s),stack?ClientSize.Width:(ClientSize.Width-gap*(count-1))/count));
                for(int i=0;i<count;i++) {
                    var button=Controls[i]; button.AutoSize=false; button.MinimumSize=Size.Empty; button.Margin=Padding.Empty;
                    button.Padding=new Padding((int)Math.Round(6*s),0,(int)Math.Round(6*s),0);
                    button.SetBounds(stack?0:i*(width+gap),stack?i*(height+gap):0,width,height);
                }
            } finally { arranging=false; }
        }
    }
}
