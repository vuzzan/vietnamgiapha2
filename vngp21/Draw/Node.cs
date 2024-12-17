using ControlzEx.Standard;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using vietnamgiapha;

namespace WpfDraw.Class
{
    public class Node: Sharp0
    {
        public delegate void UpdateNodeSizeHandler(double x, double y, double w, double h);
        public event UpdateNodeSizeHandler UpdateNodeSize;

        public delegate void SelectedNodeHandler(Node node);
        public event SelectedNodeHandler SelectedNodeEvent;

        SolidColorBrush blackBrush = new SolidColorBrush();
        SolidColorBrush textBrush = new SolidColorBrush();
        public int color = 1;
        public double margin = 10;
        public double width = 10;
        public double height = 10;
        // Same level - node have order --- user for draw tree
        public int orderInSameLevel = 1;
        public int maxOrderInsameLelel = 1;
        public string name;
        public ObservableCollection<Link> links = new ObservableCollection<Link>();
        public TextBlock textBlock;
        public System.Windows.Shapes.Rectangle rect = new System.Windows.Shapes.Rectangle();
        public FamilyViewModel familyViewModel;

        public override string ToString()
        {
            return "Node: " + name;
        }
        
        public Node(FamilyViewModel familyViewModel, double x, double y) : base(x, y)
        {
            this.familyViewModel = familyViewModel;
            this.name = familyViewModel.Name0;
            rect = new System.Windows.Shapes.Rectangle();
            rect.RadiusX = 10;
            rect.Tag = this;

            textBrush.Color = Colors.Black;
            colorbrush1.Color = Colors.BlueViolet;
            colorbrush1.Opacity = .3;
            blackBrush.Color = Colors.Black;

            textBlock = new TextBlock();
            textBlock.Text = name;
            textBlock.HorizontalAlignment = HorizontalAlignment.Center;
            textBlock.VerticalAlignment = VerticalAlignment.Top;
            textBlock.RenderTransform = new RotateTransform(0, 0, 0);
            textBlock.Foreground = textBrush;
            

            textBlock.MouseEnter += TextBlock_MouseEnter;
            textBlock.MouseDown += TextBlock_MouseDown;
            textBlock.MouseMove += TextBlock_MouseMove;

            textBlock.Measure(new System.Windows.Size(Double.PositiveInfinity, Double.PositiveInfinity));
            textBlock.Arrange(new Rect(textBlock.DesiredSize));
            // AUTO SET WIDTH + HEIGHT
            width = textBlock.DesiredSize.Width + margin * 2;
            height = textBlock.DesiredSize.Height + margin * 2;
        }

        public override void Unlink()
        {
            textBlock.MouseEnter -= TextBlock_MouseEnter;
            textBlock.MouseDown -= TextBlock_MouseDown;
            textBlock.MouseMove -= TextBlock_MouseMove;
        }

        private void TextBlock_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rect.RaiseEvent(e);
        }

        private void TextBlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            rect.RaiseEvent(e);
        }

        private void TextBlock_DragLeave(object sender, DragEventArgs e)
        {
            rect.RaiseEvent(e);
        }

        private void TextBlock_DragEnter(object sender, DragEventArgs e)
        {
            rect.RaiseEvent(e);
        }

        private void TextBlock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            rect.RaiseEvent(e);
        }

        public override void Draw(Canvas theCanvas)
        {
            if( (this.myCanvas==null) || (this.myCanvas != theCanvas && theCanvas!=null) )
                this.myCanvas = theCanvas;
            if(this.myCanvas != null) {
                this.myCanvas.Children.Remove(rect);
                Canvas.SetLeft(this.rect, p.X );
                Canvas.SetTop(this.rect, p.Y);
                rect.Height = height;
                rect.Width = width;
                rect.StrokeThickness = IsSelected?3:1;
                rect.Stroke = ReadyMoveSelected? colorbrushMove:blackBrush;
                rect.Fill = color%2==1?colorbrush1: colorbrush2;
                rect.Tag = this;

                if (UpdateNodeSize != null)
                {
                    UpdateNodeSize(p.X, p.Y, width, height);
                }
                textBlock.Text = familyViewModel.Name0;
                textBlock.Measure(new System.Windows.Size(Double.PositiveInfinity, Double.PositiveInfinity));
                textBlock.Arrange(new Rect(textBlock.DesiredSize));
                // AUTO SET WIDTH + HEIGHT
                width = textBlock.DesiredSize.Width + margin * 2;
                height = textBlock.DesiredSize.Height + margin * 2;


                this.myCanvas.Children.Add(rect);
                this.myCanvas.Children.Remove(textBlock);
                Canvas.SetLeft(textBlock, p.X + margin);
                Canvas.SetTop(textBlock, p.Y);
                this.myCanvas.Children.Add(textBlock);
                // Check links exist
                foreach(var link in links)
                {
                    link.Draw(theCanvas);
                }
            }
        }

        internal void selectMe()
        {
            if( SelectedNodeEvent!= null)
            {
                SelectedNodeEvent(this);
            }
        }

        public override string ExportMx()
        {
            /*
             <mxCell id="Y6tu8hUhq5FS6PETqi4e-429" value="Main1" style="rounded=1;whiteSpace=wrap;html=1;" vertex="1" parent="1">
          <mxGeometry x="40" y="10" width="120" height="60" as="geometry" />
        </mxCell>
             */
            string data = "<mxCell id=\"node_" + familyViewModel.familyInfo.FamilyId + "\" value=\""
                + familyViewModel.Name0 + "\" ";
            //data += " style=\"rounded=1;whiteSpace=wrap;html=1;\"";
            //data += " style=\"strokeWidth=2;shadow=0;dashed=0;align=center;html=1;shape=mxgraph.mockup.buttons.multiButton;fillColor=default;strokeColor=#000000;mainText=;subText=;gradientColor=#3333FF;gradientDirection=west;\" ";
            // #fa6800 #fa3500
            data += " style=\"rounded=1;fillColor=" + (this.color%2==1?"#fa6800": "#fa3500") + ";strokeColor=#C73500;arcSize=20;textDirection=rtl;verticalAlign=top;horizontal=1;textShadow=0;fontStyle=1;fontSize=13;\"";
            data += " vertex=\"1\" parent=\"1\">";
            data += Environment.NewLine; 
            data += "<mxGeometry x=\""+ this.p.X +"\" y=\""+ this.p.Y +"\" width=\""+ this.width +"\" height=\""+ this.height +"\" as=\"geometry\" />";
            data += Environment.NewLine;
            data += "</mxCell>";
            data += Environment.NewLine;
            return data;

        }
    }
}
