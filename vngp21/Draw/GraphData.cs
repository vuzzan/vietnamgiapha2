using AutoUpdaterDotNET;
using ControlzEx.Standard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace WpfDraw.Class
{
    public class GraphData : INotifyPropertyChanged
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("m0");

        public Dictionary<int, List<Node>> dicNode = new Dictionary<int, List<Node>>();
        private int _maxWidthCount = 0;
        public int maxWidthCount
        {
            set
            {
                _maxWidthCount = value;
            }
            get
            {
                return _maxWidthCount;
            }
        }
        private int _maxAtLevel = 0;
        public int maxAtLevel
        {
            set
            {
                _maxAtLevel = value;
            }
            get
            {
                return _maxAtLevel;
            }
        }
        private double _maxWidth = 0;
        public double maxWidth
        {
            set
            {
                _maxWidth = value;
            }
            get
            {
                return _maxWidth;
            }
        }

        public int NodeHeight
        {
            get
            {
                return dicNode.Count;
            }
        }

        public void CalculateDicNode()
        {
            // Tim vị trí nhiều nhất, level + maxwidth của bản đồ
            //int maxWidthCount = 0;
            //int maxAtLevel = 0;
            //double maxWidth = 0;
            maxWidthCount = 0;
            foreach (var key in dicNode.Keys)
            {
                if (dicNode[key].Count > maxWidthCount)
                {
                    maxWidthCount = dicNode[key].Count;
                    maxAtLevel = key;
                }
            }
            // Tim vị trí nhiều nhất, level + maxwidth của bản đồ
            foreach (var node in dicNode[maxAtLevel])
            {
                if (maxWidth < node.width + node.p.X)
                {
                    maxWidth = node.width + node.p.X;
                }
            }
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("maxWidth"));
                this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("maxAtLevel"));
                this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("maxWidthCount"));
                this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs("NodeHeight"));
            }
        }

        private double _HEIGHT_LENGTH = 300;
        public double HEIGHT_LENGTH
        {
            get
            {
                return _HEIGHT_LENGTH;
            }
            set
            {
                if (Convert.ToDouble(value) < 100)
                {
                    _HEIGHT_LENGTH = 100;
                }
                else
                {
                    _HEIGHT_LENGTH = Convert.ToDouble(value);
                }
            }
        }
        private double _MARGIN_WIDTH = 20;
        public double MARGIN_WIDTH
        {
            get
            {
                return _MARGIN_WIDTH;
            }
            set
            {
                if (Convert.ToDouble(value) < 10)
                {
                    _MARGIN_WIDTH = 10;
                }
                else
                {
                    _MARGIN_WIDTH = Convert.ToDouble(value);
                }
            }
        }

        private bool _AUTO_SIZE = false;
        public bool AUTO_SIZE
        {
            get
            {
                return _AUTO_SIZE;
            }
            set
            {
                _AUTO_SIZE = value;
            }
        }

        public ObservableCollection<Sharp0> listSharp;

        public ObservableCollection<Sharp0> listLayer;

        public event PropertyChangedEventHandler PropertyChanged;

        public GraphData()
        {
            HEIGHT_LENGTH = 300;
            MARGIN_WIDTH = 20;
            listSharp = new ObservableCollection<Sharp0>();
            listLayer = new ObservableCollection<Sharp0>();
        }
        public void AddLayer(Sharp0 sharp0)
        {
            sharp0.graphData = this;
            listLayer.Add(sharp0);
        }
        public void AddSharp(Sharp0 sharp0)
        {
            if (sharp0.GetType() == typeof(Node))
            {
                Node node = (Node)sharp0;
                node.rect.MouseEnter += Rect_MouseEnter; ;
                node.rect.MouseDown += Rect_MouseDown;
                node.rect.MouseUp += Rect_MouseUp;
                node.rect.MouseMove += Rect_MouseMove;
            }
            else if (sharp0.GetType() == typeof(Link))
            {
            }
            sharp0.graphData = this;
            listSharp.Add(sharp0);
        }

        private void Rect_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //foreach (Sharp0 node1 in listSharp)
            //{
            //    if (node1.IsSelected == true)
            //    {
            //        node1.IsSelected = false;
            //        node1.Draw(null);
            //    }
            //}
            //Node node = (Node)((Rectangle)sender).Tag;
            //node.IsSelected = true;
            //node.Draw(null);
        }

        private void Rect_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Point mousePoint = e.GetPosition((Rectangle)sender);
            Node node = (Node)((Rectangle)sender).Tag;
            if (node.ReadyMoveSelected == true)
            {
                //Console.WriteLine("     node.p : " + node.p.X + ", " + node.p.Y);
                node.p.X += mousePoint.X - node.width / 2;
                //node.p.Y += mousePoint.Y - node.height / 2;
                //Console.WriteLine("     --> node.p : " + node.p.X + ", " + node.p.Y);
                node.Draw(null);
            }
        }

        private void Rect_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //Node node = (Node)((Rectangle)sender).Tag;
            //node.ReadyMoveSelected = false;
            //node.Draw(null);
            //Console.WriteLine("  -- Sharp: " + node.ToString() + " Stop move");
        }

        private void Rect_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Node node = (Node)((Rectangle)sender).Tag;
            node.ReadyMoveSelected = !node.ReadyMoveSelected;
            node.Draw(null);
            if (node.ReadyMoveSelected == true)
            {
                //node.selectMe();
            }
            //Console.WriteLine("  -- Sharp: " + node.ToString() +"  Ready to move");
        }

        public void Unlink(Canvas theCanvas)
        {
            foreach (Sharp0 sharp0 in listLayer)
            {
                sharp0.Unlink();
            }
            foreach (Sharp0 sharp0 in listSharp)
            {
                if (sharp0.GetType() == typeof(Node))
                {
                    Node node = (Node)sharp0;
                    node.rect.MouseEnter -= Rect_MouseEnter; ;
                    node.rect.MouseDown -= Rect_MouseDown;
                    node.rect.MouseUp -= Rect_MouseUp;
                    node.rect.MouseMove -= Rect_MouseMove;
                    //
                    node.links.Clear();
                }

                sharp0.Unlink();
            }
        }
        public void Draw(Canvas theCanvas)
        {
            // Ve background va thong tin so doi
            foreach (Sharp0 node in listLayer)
            {
                node.Draw(theCanvas);
            }
            // Ve Gia Pha
            foreach (Sharp0 node in listSharp)
            {
                node.Draw(theCanvas);
            }
        }
        private string GetMxTool()
        {
            string mxData = "<mxfile host=\"app.diagrams.net\" agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\" version=\"24.8.6\">";
            mxData += Environment.NewLine;
            mxData += "<diagram name=\"Page-1\" id=\"Z0l2aBFmFVraHKxeqVqh\">";
            mxData += Environment.NewLine;
            mxData += "<mxGraphModel dx=\"1189\" dy=\"605\" grid=\"1\" gridSize=\"10\" guides=\"1\" tooltips=\"1\" connect=\"1\" arrows=\"1\" fold=\"1\" page=\"1\" pageScale=\"1\" pageWidth=\"850\" pageHeight=\"1100\" math=\"0\" shadow=\"0\">";
            mxData += Environment.NewLine;
            mxData += "<root>";
            mxData += Environment.NewLine;
            mxData += "<mxCell id=\"0\" />";
            mxData += Environment.NewLine;
            mxData += "<mxCell id=\"1\" parent=\"0\" />";
            mxData += Environment.NewLine;
            // Ve Gia Pha
            foreach (Sharp0 sharp0 in listSharp)
            {
                if (sharp0.GetType() == typeof(Node))
                {
                    Node node = (Node)sharp0;
                    mxData += node.ExportMx();
                }
                else if (sharp0.GetType() == typeof(Link))
                {
                    Link link = (Link)sharp0;
                    mxData += link.ExportMx();
                }
            }
            mxData += "</root>";
            mxData += Environment.NewLine;
            mxData += "</mxGraphModel>";
            mxData += Environment.NewLine;
            mxData += "</diagram>";
            mxData += Environment.NewLine;
            mxData += "</mxfile>";
            mxData += Environment.NewLine;
            return mxData;
        }
        public void ExportMxFile(string fileName)
        {
            System.IO.File.WriteAllText(fileName, GetMxTool());
            MessageBox.Show("Export mxfile " + fileName);
        }

        public void ExportHtmlFile(string gpname, string fileName)
        {
            string mxData = "<!--[if IE]><meta http-equiv=\"X-UA-Compatible\" content=\"IE=5,IE=9\" ><![endif]-->";
            mxData += Environment.NewLine;
            mxData += "<!DOCTYPE html>" + Environment.NewLine;
            mxData += "<html>" + Environment.NewLine;
            mxData += "<head>" + Environment.NewLine;
            mxData += "<title>"+ gpname +"</title>" + Environment.NewLine;
            mxData += "<meta charset=\"utf-8\"/>" + Environment.NewLine;
            mxData += "</head>" + Environment.NewLine;
            mxData += "<body>" + Environment.NewLine;
            mxData += "<div class=\"mxgraph\" style=\"max-width:100%;border:1px solid transparent;\" data-mxgraph=\"";
            //================================
            string xmldata = GetMxTool();
            xmldata = xmldata.Replace(Environment.NewLine, "");
            xmldata = xmldata.Replace("\"", "\\&quot;");
            xmldata = xmldata.Replace(">", "&gt;");
            xmldata = xmldata.Replace("<", "&lt;");
            //
            mxData += "{&quot;highlight&quot;:&quot;#0000ff&quot;,&quot;nav&quot;:true,&quot;resize&quot;:true,&quot;xml&quot;:&quot;";
            mxData += xmldata;

            mxData += "&quot;,&quot;toolbar&quot;:&quot;pages zoom layers lightbox&quot;,&quot;page&quot;:0}";
            //
            //================================
            mxData += "\"></div>" + Environment.NewLine;
            mxData += "<script type=\"text/javascript\" src=\"https://app.diagrams.net/js/viewer-static.min.js\"></script>\r\n" + Environment.NewLine;
            mxData += "</body>" + Environment.NewLine;
            mxData += "</html>" + Environment.NewLine;
            System.IO.File.WriteAllText(fileName, mxData);
            //MessageBox.Show("Export html: " + fileName);
        }

        public void ExportDrawioFile(string gpname, string fileName)
        {
            //================================
            string xmldata = GetMxTool();
            System.IO.File.WriteAllText(fileName, xmldata);
            //MessageBox.Show("Export html: " + fileName);
        }
    }
}