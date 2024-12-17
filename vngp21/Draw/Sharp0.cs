using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WpfDraw.Class
{
    public abstract class Sharp0
    {
        public SolidColorBrush colorbrush1 = new SolidColorBrush();
        public SolidColorBrush colorbrush2 = new SolidColorBrush();
        public SolidColorBrush colorbrushMove = new SolidColorBrush();

        public Point p;
        public Canvas myCanvas;

        public bool IsSelected = false;

        public bool ReadyMoveSelected = false;
        public GraphData graphData = null;
        public Sharp0(double x, double y)
        {
            this.p = new Point((int)x, (int)y);
            colorbrush1.Color = Colors.BlueViolet;
            colorbrush2.Color = Colors.AliceBlue;

            colorbrushMove.Color = Colors.Blue;
        }
        public override string ToString()
        {
            return "sharp0";
        }
        public abstract void Draw(Canvas theCanvas);
        public abstract void Unlink();
        public abstract string ExportMx();
    }
}
