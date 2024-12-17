using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using vietnamgiapha;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace WpfDraw.Class
{
    public class Link: Sharp0
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");

        public Node node1;
        public Node node2;
        System.Windows.Point p1 = new System.Windows.Point();
        System.Windows.Point p4 = new System.Windows.Point();
        public Polyline polyline = new Polyline();
        //public Line lineLink = new Line();
        Rectangle point1Rect = new Rectangle();
        Ellipse point2Eclipse = new Ellipse();
        public Link(Node node1, Node node2, double x, double y) : base(x, y)
        {
            this.node1 = node1;
            this.node2 = node2;

            p1.X = node1.p.X + node1.width / 2;
            p1.Y = node1.p.Y + node1.height;
            p4.X = node2.p.X + node2.width / 2;
            p4.Y = node2.p.Y;

            bool checkExist = false;
            foreach (var l in this.node1.links)
            {
                if (this.ToString() == l.ToString())
                {
                    checkExist = true;
                    break;
                }
            }
            if (checkExist == false)
            {
                this.node1.links.Add(this);
            }


            checkExist = false;
            foreach (var l in this.node2.links)
            {
                if (this.ToString() == l.ToString())
                {
                    checkExist = true;
                    break;
                }
            }
            if (checkExist == false)
            {
                this.node2.links.Add(this);
            }
            polyline.Tag = this;
        }
        public override string ToString()
        {
            return "Link " + node1==null?"":node1.ToString() + " -> " + node2==null?"":node2.ToString();
        }
        public override void Draw(Canvas theCanvas)
        {
            if ((this.myCanvas == null) || (this.myCanvas != theCanvas && theCanvas != null))
                this.myCanvas = theCanvas;
            if (this.myCanvas != null)
            {
                this.myCanvas.Children.Remove(polyline);
                this.myCanvas.Children.Remove(point1Rect);
                this.myCanvas.Children.Remove(point2Eclipse);
                // Draw from point 1 to point 2
                polyline.Stroke = colorbrush1;

                polyline.Points.Clear();
                //                   P1 (tren)
                //                    |
                //        P3 --------P2
                //         |
                //        P4 (duoi)
                int px = (int)((p1.X + p4.X) / 2);
                int py = (int)((p1.Y + p4.Y) / 2);
                if (node1.orderInSameLevel > 0)
                {
                    if (p1.X < p4.X)
                    {
                        //         P1 (tren)
                        //         |
                        //         P2 ---------- P3
                        //                       |
                        //                       P4 (duoi)
                        //Cha nằm bên trái - con bên phải - link lấy từ dưới lên trên
                        int py_tmp = (int)((p4.Y - 
                            (((graphData.HEIGHT_LENGTH - 20) / (node1.maxOrderInsameLelel == 1 ? 2 : node1.maxOrderInsameLelel)) 
                            * node1.orderInSameLevel)));
                        //log.Info(" LINK 111 p1.X =" + p1.X + "< p4.X" + p4.X + " p1.Y=" + p1.Y + " p4.Y=" + p4.Y + " " + node1.name + " " + py_tmp + " " + node1.orderInSameLevel + " / " + node1.maxOrderInsameLelel);
                        if (py_tmp < p1.Y + 15 )
                        {
                            py_tmp = (int)p1.Y +  15;
                            
                            log.Info("          LINK 666666666 py_tmp="+ py_tmp+" p1.X =" + p1.X + "< p4.X" + p4.X + " p1.Y=" + p1.Y + " p4.Y=" + p4.Y + " " + node1.name + " " + py_tmp + " " + node1.orderInSameLevel + " / " + node1.maxOrderInsameLelel);
                        }
                        py = py_tmp;
                    }
                    else
                    {
                        //                   P1 (tren)
                        //                    |
                        //        P3 --------P2
                        //         |
                        //        P4 (duoi)
                        // Cha nằm bên phải - con bên trai - link lấy từ trên xuống dưới 
                        int py_tmp = (int)((p1.Y + (((graphData.HEIGHT_LENGTH ) / 
                            (node1.maxOrderInsameLelel == 1 ? 2 : node1.maxOrderInsameLelel)) 
                            * node1.orderInSameLevel)));
                        log.Info(" LINK 222 p1.X ="+ p1.X+"< p4.X"+ p4.X  + " p1.Y=" + p1.Y + " p4.Y="+ p4.Y +" "+ node1.name + " " + py_tmp + " " + node1.orderInSameLevel + " / " + node1.maxOrderInsameLelel);
                        if (py_tmp > p4.Y - 15)
                        {
                            py_tmp = (int)p4.Y - 15;
                            //log.Info("          LINK 666666666 py_tmp=" + py_tmp + " p1.X =" + p1.X + "< p4.X" + p4.X + " p1.Y=" + p1.Y + " p4.Y=" + p4.Y + " " + node1.name + " " + py_tmp + " " + node1.orderInSameLevel + " / " + node1.maxOrderInsameLelel);
                        }
                        py = py_tmp;
                    }
                }
                p1.X = node1.p.X + node1.width / 2;
                p1.Y = node1.p.Y + node1.height;
                p4.X = node2.p.X + node2.width / 2;
                p4.Y = node2.p.Y;

                System.Windows.Point p2 = new System.Windows.Point(p1.X, py);
                System.Windows.Point p3 = new System.Windows.Point(p4.X, py);
                polyline.Points.Add(p1);
                polyline.Points.Add(p2);
                polyline.Points.Add(p3);
                polyline.Points.Add(p4);
                //
                //lineLink.X1 = node1.p.X + node1.width/2;
                //lineLink.Y1 = node1.p.Y + node1.height;
                ////
                //lineLink.X2 = node2.p.X + node2.width / 2;
                //lineLink.Y2 = node2.p.Y;

                Canvas.SetLeft(point1Rect, p1.X- 2.5);
                Canvas.SetTop(point1Rect, p1.Y - 2.5);
                point1Rect.Height = 5;
                point1Rect.Width = 5;
                point1Rect.StrokeThickness = IsSelected ? 3 : 1;
                point1Rect.Stroke = colorbrushMove;
                point1Rect.Fill = colorbrush1;
                
                Canvas.SetLeft(point2Eclipse, p4.X - 2.5);
                Canvas.SetTop(point2Eclipse, p4.Y - 2.5);
                point2Eclipse.Height = 5;
                point2Eclipse.Width = 5;
                point2Eclipse.StrokeThickness = IsSelected ? 3 : 1;
                point2Eclipse.Stroke = colorbrushMove;
                point2Eclipse.Fill = colorbrush2;
                this.myCanvas.Children.Add(point1Rect);
                this.myCanvas.Children.Add(point2Eclipse);
                this.myCanvas.Children.Add(polyline);
            }
        }
        public override void Unlink()
        {
        }

        public override string ExportMx()
        {
            /*
             <mxCell id="Y6tu8hUhq5FS6PETqi4e-432" value="" style="endArrow=classic;html=1;rounded=0;entryX=0.5;entryY=0;entryDx=0;entryDy=0;exitX=0.5;exitY=1;exitDx=0;exitDy=0;" edge="1" parent="1" source="Y6tu8hUhq5FS6PETqi4e-429" target="Y6tu8hUhq5FS6PETqi4e-430">
          <mxGeometry width="50" height="50" relative="1" as="geometry">
            <mxPoint x="490" y="190" as="sourcePoint" />
            <mxPoint x="540" y="140" as="targetPoint" />
          </mxGeometry>
        </mxCell>
             */
            //lineLink.X1 = node1.p.X + node1.width / 2;
            //lineLink.Y1 = node1.p.Y + node1.height;
            //lineLink.X2 = node2.p.X + node2.width / 2;
            //lineLink.Y2 = node2.p.Y;

            //long FamilyId = node1.familyViewModel.familyInfo.FamilyId * node2.familyViewModel.familyInfo.FamilyId;
            string data = "<mxCell id=\"link_" + node1.familyViewModel.familyInfo.FamilyId +"_" + node2.familyViewModel.familyInfo.FamilyId + "\" value=\"\" ";
            // Direct line
            //data += " style=\"endArrow=classic;html=1;rounded=0;entryX=0.5;entryY=0;entryDx=0;entryDy=0;exitX=0.5;exitY=1;exitDx=0;exitDy=0;\"";
            // Curve line
            data += " style=\"edgeStyle=elbowEdgeStyle;elbow=vertical;endArrow=classic;html=1;exitX=0.5;exitY=1;exitDx=0;exitDy=0;\"";
            //
            data += " edge=\"1\" parent=\"1\" ";
            data += " source=\"node_"+ node1.familyViewModel.familyInfo.FamilyId + "\" target=\"node_"+ node2.familyViewModel.familyInfo.FamilyId+"\">";
            data += Environment.NewLine;
            data += "<mxGeometry width=\"50\" height=\"50\" relative=\"1\" as=\"geometry\">";
            data += Environment.NewLine;
            data += "<mxPoint x=\""+ p1.X+"\" y=\""+ p1.Y+"\" as=\"sourcePoint\" />";
            data += Environment.NewLine;
            data += "<mxPoint x=\""+ p4.X+"\" y=\""+ p4.Y+"\" as=\"targetPoint\" />";
            // Point data - link from parent -> child
            int px = (int)((p1.X + p4.X) / 2);
            int py = (int)((p1.Y + p4.Y) / 2);
            
            if (node1.orderInSameLevel > 0)
            {
                //log.Info(" LINK " + node1.familyViewModel.familyInfo.FamilyLevel + " -- Node 1:" + node1.name + " orderInSameLevel=" + node1.orderInSameLevel + " node1.maxOrderInsameLelel=" + node1.maxOrderInsameLelel);
                //py = (int)((lineLink.Y2 - (((graphData.HEIGHT_LENGTH - 20) / (node1.maxOrderInsameLelel == 1 ? 2 : node1.maxOrderInsameLelel)) * node1.orderInSameLevel)));
                if (p1.X < p4.X)
                {
                    //Cha nằm bên trái - con bên phải - link lấy từ dưới lên trên
                    py = (int)((p4.Y - (((graphData.HEIGHT_LENGTH - 20) / (node1.maxOrderInsameLelel == 1 ? 2 : node1.maxOrderInsameLelel)) * node1.orderInSameLevel)));
                }
                else
                {
                    // Cha nằm bên phải - con bên trai - link lấy từ trên xuống dưới 
                     py = (int)((p1.Y + (((graphData.HEIGHT_LENGTH - 20) / (node1.maxOrderInsameLelel == 1 ? 2 : node1.maxOrderInsameLelel)) * node1.orderInSameLevel)));
                }
            }
            data += "<Array as=\"points\"><mxPoint x=\"" + px + "\" y=\"" + py + "\" /></Array>";
            //
            data += Environment.NewLine;
            data += "</mxGeometry>";
            data += Environment.NewLine;
            data += "</mxCell>";
            return data;

        }
    }
}
