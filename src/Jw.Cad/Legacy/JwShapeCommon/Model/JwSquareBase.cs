using JwCore;
using JwwHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace JwShapeCommon
{
    public abstract class JwSquareBase
    {
        // 移植：所属图纸（缩尺、解析设定）。旧版从静态变量 JwFileConsts 读取，现在由构造函数显式传入，
        // 漏传会编译失败，避免以缩尺 0 静默计算。
        protected JwSquareBase(JwDrawing drawing)
        {
            Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        }

        public JwDrawing Drawing { get; }

        public string Id { get; set; }

        #region 矩形才有
        public JwXian? TopLine { get; set; }

        public JwXian? BottomLine { get; set; }

        public JwXian? LeftLine { get; set; }

        public JwXian? RightLine { get; set; }

        /// <summary>
        /// 左上
        /// </summary>
        public JWPoint? TopLeft { get; set; }

        /// <summary>
        /// 右上
        /// </summary>
        public JWPoint? TopRight { get; set; }

        /// <summary>
        /// 左下
        /// </summary>
        public JWPoint? BottomLeft { get; set; }

        /// <summary>
        /// 右下
        /// </summary>
        public JWPoint? BottomRight { get; set; }

        public List<JwXian> BlockLines { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        /// <summary>
        /// jw图纸坐标差 乘 jw设计绘制里标注的 比例 默认100
        /// 
        /// </summary>
        public double WidthScale { get; set; }

        public double HeightScale { get; set; }

        /// <summary>
        /// 水平的为Y 垂直的为X
        /// </summary>
        public double Center { get; set; }

        

        public JWPoint CenterPoint { get; set; }

        /// <summary>
        /// 约定小于零右上，大于零右下
        /// </summary>
        public double Jiaodu { get; set; }

        /// <summary>
        /// 实现 style 1  虚线2
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <returns></returns>
        public JwwSen CreateSenByTwoPoint(JWPoint p1, JWPoint p2)
        {
            var sen = new JwwSen();
            //sen.m_nPenWidth=1/
            //sen.m_nPenColor = 2;
            sen.m_start_x = p1.X;
            sen.m_start_y = p1.Y;
            sen.m_end_x = Math.Round(p2.X, 6);
            sen.m_end_y = Math.Round(p2.Y, 6);
            sen.m_nLayer = (int)DrawShapeType.Beam + 1;
            sen.m_nPenColor = (int)DrawShapeType.Beam;
            sen.m_nPenStyle = 1;
            sen.m_nPenWidth = 0;
            return sen;
        }

        public JwwSen DrawSen(double x,double y,bool isHorizontal,double distance,int ps=1)
        {
            var sen = new JwwSen();
            //sen.m_nPenWidth=1/
            //sen.m_nPenColor = 2;
            if (isHorizontal)
            {
                sen.m_start_x = x;
                sen.m_start_y = y;
                sen.m_end_x = Math.Round(x+distance, 6);
                sen.m_end_y = Math.Round(y, 6);
            }
            else
            {
                sen.m_start_x = x;
                sen.m_start_y = y;
                sen.m_end_x = Math.Round(x, 6);
                sen.m_end_y = Math.Round(y-distance, 6);
            }
            
            sen.m_nLayer = (int)DrawShapeType.Beam + 1;
            sen.m_nPenColor = (int)DrawShapeType.Beam;
            sen.m_nPenStyle = (byte)ps;
            sen.m_nPenWidth = 0;
            return sen;
        }



        public BeamDirectionType DirectionType { get; set; }

        public void JisuanWidthHeight()
        {
            Width = TopRight.X - TopLeft.X;
            Height = TopLeft.Y - BottomLeft.Y;

            if (Width >= Height)
            {
                DirectionType = BeamDirectionType.Horizontal;
                Center = (TopLeft.Y + BottomLeft.Y) / 2;
            }
            else
            {
                DirectionType = BeamDirectionType.Vertical;
                Center = (TopLeft.X + TopRight.X) / 2;
            }
            CenterPoint = new JWPoint();
            CenterPoint.X = Math.Round((TopLeft.X + TopRight.X) / 2.0, 4);
            CenterPoint.Y = Math.Round((TopLeft.Y + BottomLeft.Y) / 2.0, 4);
        }

        // 计算两点之间的距离
        private double Distance(JWPoint p1, JWPoint p2)
        {
            return Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
        }

        public bool Contains(double x, double y) => TopLeft.X <= x && x <= TopLeft.X + Width && TopLeft.Y >= y && y >= TopLeft.Y - Height;

        public bool Contains(JWPoint pt) {
            if(object.Equals(pt, null))
            {
                return false;
            }
            return Contains(pt.X, pt.Y);
        } 

        /// <summary>
        /// 可以扩大范围
        /// 2025年6月21日
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public bool ContainShenglue(JWPoint pt)
        {
            //return Contains(Math.Round(pt.X, 2), Math.Round(pt.Y, 2));

            var slx = Math.Round(pt.X, 2);
            var sly = Math.Round(pt.Y, 2);
            double wc = 0.05;
            return TopLeft.X-wc <= slx && slx <= TopLeft.X + Width+wc && TopLeft.Y+wc >= sly && sly >= TopLeft.Y - Height-wc;


        }

        public bool WuchaContains(double x, double y) => TopLeft.X- Drawing.Settings.NearSpliteMax / Drawing.Scale <= x && x <= TopLeft.X + Width+ Drawing.Settings.NearSpliteMax / Drawing.Scale && TopLeft.Y+ Drawing.Settings.NearSpliteMax / Drawing.Scale >= y && y >= TopLeft.Y - Height- Drawing.Settings.NearSpliteMax / Drawing.Scale;

        public bool WuchaContains(JWPoint pt) => WuchaContains(pt.X, pt.Y);

        public bool NearContains(JWPoint pt) => NearContains(pt.X,pt.Y);

        public bool NearContains(double x, double y)
        {
            //if (DirectionType == BeamDirectionType.Horizontal)
            //{
            //    return (TopLeft.Y + Drawing.Settings.NearSpliteMax / Drawing.Scale >= y && y >= TopLeft.Y - Drawing.Settings.NearSpliteMax / Drawing.Scale) || (BottomLeft.Y + Drawing.Settings.NearSpliteMax / Drawing.Scale >= y && y >= BottomLeft.Y - Drawing.Settings.NearSpliteMax / Drawing.Scale);
            //}
            //else
            //{
            //    return (TopLeft.X + Drawing.Settings.NearSpliteMax / Drawing.Scale >= x && x >= TopLeft.X - Drawing.Settings.NearSpliteMax / Drawing.Scale) || (TopRight.X + Drawing.Settings.NearSpliteMax / Drawing.Scale >= x && x >= TopRight.X - Drawing.Settings.NearSpliteMax / Drawing.Scale);
            //}
            return TopLeft.X - Drawing.Settings.NearSpliteMax / Drawing.Scale <= x && x <= TopRight.X + Drawing.Settings.NearSpliteMax / Drawing.Scale && TopLeft.Y + Drawing.Settings.NearSpliteMax / Drawing.Scale >= y && y >= BottomLeft.Y - Drawing.Settings.NearSpliteMax / Drawing.Scale;
            //return true;
        }


        public bool ContainsDirected(JwDirected directed)
        {
            bool re = true;

            foreach (var p in directed.Points)
            {
                re = re && Contains(p);
            }

            return re;
        }

        public bool JieChuDirected(JwDirected directed) 
        {
            if (directed.IsDirected)
            {
                if (directed.TaggDirect == TaggDirect.Left)
                {
                    return (TopLeft.Y>directed.QieGeZhi&& directed.QieGeZhi > BottomLeft.Y)&&(TopRight.X - Drawing.Settings.NearSpliteMax / Drawing.Scale <= directed.DirectPoint.X && directed.DirectPoint.X <= TopRight.X + Drawing.Settings.NearSpliteMax / Drawing.Scale);
                }
                if (directed.TaggDirect == TaggDirect.Right)
                {
                    return (TopLeft.Y > directed.QieGeZhi && directed.QieGeZhi > BottomLeft.Y)&&(TopLeft.X - Drawing.Settings.NearSpliteMax / Drawing.Scale <= directed.DirectPoint.X && directed.DirectPoint.X <= TopLeft.X + Drawing.Settings.NearSpliteMax / Drawing.Scale);
                }
                if (directed.TaggDirect == TaggDirect.Up)
                {
                    return (TopLeft.X < directed.QieGeZhi && directed.QieGeZhi < TopRight.X) &&( BottomLeft.Y - Drawing.Settings.NearSpliteMax / Drawing.Scale <= directed.DirectPoint.Y && directed.DirectPoint.Y <= BottomLeft.Y + Drawing.Settings.NearSpliteMax / Drawing.Scale);
                }
                if (directed.TaggDirect == TaggDirect.Down)
                {
                    return (TopLeft.X < directed.QieGeZhi && directed.QieGeZhi < TopRight.X) &&(TopLeft.Y - Drawing.Settings.NearSpliteMax / Drawing.Scale <= directed.DirectPoint.Y && directed.DirectPoint.Y <= TopLeft.Y + Drawing.Settings.NearSpliteMax / Drawing.Scale);
                }
                return false;
            }
            else
            {
                return false;
            }
        }


        public bool ContainsAnyPoint(List<JWPoint> points)
        {
            bool r = false;
            foreach(JWPoint point in points)
            {
                if (Contains(point))
                {
                    r=true; 
                    break;
                }
            }
            return r;
        }

        #endregion
    }
}
