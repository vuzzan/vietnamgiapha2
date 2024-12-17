using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WpfDraw.Class;

namespace vngp21.Draw
{
    public class LayerLevel : Sharp0
    {
        private SolidColorBrush bgColor1 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(146, 160, 161));
        private SolidColorBrush bgColor2 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(193, 214, 220));
        private SolidColorBrush bgColor3 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 224, 174));
        private SolidColorBrush bgColor4 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 189, 139));
        private SolidColorBrush bgColor5 = new SolidColorBrush(System.Windows.Media.Color.FromRgb(202, 134, 113));

        public TextBlock textBlock;
        public System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle();
        public int level;
        public double height;
        public GraphData _objGraphData;
        public LayerLevel(double x, double y) : base(x, y)
        {
            System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle();
            textBlock = new TextBlock();
            level = 0;
        }

        public override void Draw(Canvas theCanvas)
        {
            if ((this.myCanvas == null) || (this.myCanvas != theCanvas && theCanvas != null))
                this.myCanvas = theCanvas;
            if (this.myCanvas != null)
            {
                myCanvas.Children.Remove(rect);
                Canvas.SetLeft(rect, 0 );
                //Canvas.SetTop(rect, level * _objGraphData.HEIGHT_LENGTH - _objGraphData.MARGIN_WIDTH);
                Canvas.SetTop(rect, p.Y - _objGraphData.HEIGHT_LENGTH / 2);
                rect.Height = p.Y + height + _objGraphData.HEIGHT_LENGTH/2;
                rect.Width = _objGraphData.maxWidth;
                rect.StrokeThickness = 0;
                //rect.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 0));
                rect.Fill = level % 2 == 0 ? bgColor2 : bgColor3;
                myCanvas.Children.Add(rect);
                // TExt block

                myCanvas.Children.Remove(textBlock);
                textBlock.Text = "ĐỜI THỨ #" + level + "\nCó " + _objGraphData.dicNode[level].Count + " gia đình.\n";

                //Canvas.SetLeft(textBlock, p.X + _objGraphData.MARGIN_WIDTH);
                //Canvas.SetTop(textBlock, p.Y + (level-1) * _objGraphData.HEIGHT_LENGTH);
                Canvas.SetLeft(textBlock, 10);
                Canvas.SetTop(textBlock, p.Y + height);
                myCanvas.Children.Add(textBlock);
            }
        }

        public override void Unlink()
        {
            // Nothing
        }

        public override string ExportMx()
        {
            return "";
        }
    }
}
