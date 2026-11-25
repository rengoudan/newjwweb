using JwShapeCommon.Compat; // 旧版 using Sunny.UI 带来的 First()/Last() 语义，见 SunnyCollectionsCompat
using JwCore;
using JwwHelper;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JwShapeCommon.Model
{
    public class JwVpl:IDrawToJww
    {
        // 移植：所属图纸（缩尺等），旧版读静态 JwFileConsts
        public JwDrawing Drawing { get; }


        public JwVpl(JwDrawing drawing, JWPoint p,BeamEndPosition bep)
        {
            Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            Location = p;
            Position = bep;
            //createOther();
            createOther();
        }

        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 链接点 即三个圆孔中的基准孔中心
        /// </summary>
        public JWPoint Location { get; set; }

        /// <summary>
        /// 相对于Location的第二个孔位置
        /// </summary>
        public JWPoint SecondLoaction { get; set; }

        /// <summary>
        /// 第三个孔位置一般为败方G 打孔位置
        /// </summary>
        public JWPoint ThirdLocation { get; set; }

        public BeamEndPosition Position { get; set; }

        /// <summary>
        /// 部件侧边
        /// </summary>
        public JwXian SideLine { get; set; }

        /// <summary>
        /// 部件顶边
        /// </summary>
        public JwXian TopLine { get; set; }

        /// <summary>
        /// 部件底边
        /// </summary>
        public JwXian BottomLine { get; set; }

        /// <summary>
        /// 部件倾斜轮廓线
        /// </summary>
        public JwXian Slash { get; set; }

        public JwArc Arc { get; set; }

        /// <summary>
        /// 
        /// </summary>
        private const double bottomSpacing= 40;

        private const double secondCenterSpacing = 68;

        private const double sideLength = 131;

        private const double longestsideLength = 95;

        private const double shortsideLength = 50;

        private const double topSpacing = 35;
        private const double jpSpacing = 45;

        private void createOther()
        {
            switch (Position)
            {
                case BeamEndPosition.上右:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy;
                        var slx=cx-(secondCenterSpacing/Drawing.Scale); 
                        SecondLoaction=new JWPoint(slx,sly);
                        var by = cy - (bottomSpacing / Drawing.Scale);
                        var bx = cx - (longestsideLength / Drawing.Scale);
                        var sidebottom =new JWPoint(bx, by);
                        var ul = new JWPoint(cx, by);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsy = cy + ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(bx, tpsy);
                        SideLine = new JwXian(sidebottom, topside);
                        var trx=cx-(jpSpacing/Drawing.Scale);
                        var topother = new JWPoint(trx, tpsy);
                        TopLine = new JwXian(topother, topside);
                        var tlx=cy+JwConstants.EllipseSpacing/Drawing.Scale;
                        ThirdLocation=new JWPoint(slx, tlx);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, 1.5707963267948966, -2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;  
                        Slash=new JwXian(topother, l);
                        break;
                    }
                case BeamEndPosition.上左:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy;
                        var slx = cx + (secondCenterSpacing / Drawing.Scale);
                        SecondLoaction = new JWPoint(slx, sly);
                        var by = cy - (bottomSpacing / Drawing.Scale);
                        var bx = cx + (longestsideLength / Drawing.Scale);
                        var sidebottom = new JWPoint(bx, by);
                        var ul = new JWPoint(cx, by);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsy = cy + ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(bx, tpsy);
                        SideLine = new JwXian(sidebottom, topside);
                        var trx = cx + (jpSpacing / Drawing.Scale);
                        var topother = new JWPoint(trx, tpsy);
                        TopLine = new JwXian(topother, topside);
                        var tlx = cy + JwConstants.EllipseSpacing / Drawing.Scale;
                        ThirdLocation = new JWPoint(slx, tlx);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, 1.5707963267948966, 2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;
                        Slash = new JwXian(topother, l);
                        break;
                    }
                case BeamEndPosition.下右:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy;
                        var slx = cx - (secondCenterSpacing / Drawing.Scale);
                        SecondLoaction = new JWPoint(slx, sly);
                        var by = cy + (bottomSpacing / Drawing.Scale);
                        var bx = cx - (longestsideLength / Drawing.Scale);
                        var sidebottom = new JWPoint(bx, by);
                        var ul = new JWPoint(cx, by);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsy = cy - ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(bx, tpsy);
                        SideLine = new JwXian(sidebottom, topside);
                        var trx = cx - (jpSpacing / Drawing.Scale);
                        var topother = new JWPoint(trx, tpsy);
                        TopLine = new JwXian(topother, topside);
                        var tlx = cy - JwConstants.EllipseSpacing / Drawing.Scale;
                        ThirdLocation = new JWPoint(slx, tlx);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, -1.5707963267948966, 2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;
                        Slash = new JwXian(topother, l);
                        break;
                    }

                case BeamEndPosition.下左:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy;
                        var slx = cx + (secondCenterSpacing / Drawing.Scale);
                        SecondLoaction = new JWPoint(slx, sly);
                        var by = cy + (bottomSpacing / Drawing.Scale);
                        var bx = cx + (longestsideLength / Drawing.Scale);
                        var sidebottom = new JWPoint(bx, by);
                        var ul = new JWPoint(cx, by);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsy = cy - ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(bx, tpsy);
                        SideLine = new JwXian(sidebottom, topside);
                        var trx = cx + (jpSpacing / Drawing.Scale);
                        var topother = new JWPoint(trx, tpsy);
                        TopLine = new JwXian(topother, topside);
                        var tlx = cy - JwConstants.EllipseSpacing / Drawing.Scale;
                        ThirdLocation = new JWPoint(slx, tlx);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, -1.5707963267948966, -2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;
                        Slash = new JwXian(topother, l);
                        break;
                    }
                case BeamEndPosition.左上:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy - (secondCenterSpacing / Drawing.Scale);
                        var slx = cx;
                        SecondLoaction = new JWPoint(slx, sly);
                        var by = cy - (longestsideLength / Drawing.Scale);
                        var bx = cx+ (bottomSpacing / Drawing.Scale);  
                        var sidebottom = new JWPoint(bx, by);
                        var ul = new JWPoint(bx, cy);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsx = cx - ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(tpsx, by);
                        SideLine = new JwXian(sidebottom, topside);
                        var trxy = cy - (jpSpacing / Drawing.Scale);
                        var topother = new JWPoint(tpsx, trxy);
                        TopLine = new JwXian(topother, topside);
                        var tlx = cx - JwConstants.EllipseSpacing / Drawing.Scale;
                        ThirdLocation = new JWPoint(tlx,sly);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, 0, -2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;
                        Slash = new JwXian(topother, l);
                        break;
                    }
                case BeamEndPosition.左下:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy + (secondCenterSpacing / Drawing.Scale);
                        var slx = cx;
                        SecondLoaction = new JWPoint(slx, sly);
                        var by = cy + (longestsideLength / Drawing.Scale);
                        var bx = cx + (bottomSpacing / Drawing.Scale);
                        var sidebottom = new JWPoint(bx, by);
                        var ul = new JWPoint(bx, cy);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsx = cx - ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(tpsx, by);
                        SideLine = new JwXian(sidebottom, topside);
                        var trxy = cy + (jpSpacing / Drawing.Scale);
                        var topother = new JWPoint(tpsx, trxy);
                        TopLine = new JwXian(topother, topside);
                        var tlx = cx - JwConstants.EllipseSpacing / Drawing.Scale;
                        ThirdLocation = new JWPoint(tlx, sly);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, 0, 2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;
                        Slash = new JwXian(topother, l);
                        break;
                    }

                case BeamEndPosition.右上:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy - (secondCenterSpacing / Drawing.Scale);
                        var slx = cx;
                        SecondLoaction = new JWPoint(slx, sly);
                        var by = cy - (longestsideLength / Drawing.Scale);
                        var bx = cx - (bottomSpacing / Drawing.Scale);
                        var sidebottom = new JWPoint(bx, by);
                        var ul = new JWPoint(bx, cy);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsx = cx + ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(tpsx, by);
                        SideLine = new JwXian(sidebottom, topside);
                        var trxy = cy - (jpSpacing / Drawing.Scale);
                        var topother = new JWPoint(tpsx, trxy);
                        TopLine = new JwXian(topother, topside);
                        var tlx = cx + JwConstants.EllipseSpacing / Drawing.Scale;
                        ThirdLocation = new JWPoint(tlx, sly);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, 3.1415926535897931, 2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;
                        Slash = new JwXian(topother, l);
                        break;
                    }
                case BeamEndPosition.右下:
                    {
                        var cx = Location.X;
                        var cy = Location.Y;
                        var sly = cy + (secondCenterSpacing / Drawing.Scale);
                        var slx = cx;
                        SecondLoaction = new JWPoint(slx, sly);
                        var by = cy + (longestsideLength / Drawing.Scale);
                        var bx = cx - (bottomSpacing / Drawing.Scale);
                        var sidebottom = new JWPoint(bx, by);
                        var ul = new JWPoint(bx, cy);
                        BottomLine = new JwXian(sidebottom, ul);
                        var tpsx = cx + ((sideLength - bottomSpacing) / Drawing.Scale);
                        var topside = new JWPoint(tpsx, by);
                        SideLine = new JwXian(sidebottom, topside);
                        var trxy = cy + (jpSpacing / Drawing.Scale);
                        var topother = new JWPoint(tpsx, trxy);
                        TopLine = new JwXian(topother, topside);
                        var tlx = cx + JwConstants.EllipseSpacing / Drawing.Scale;
                        ThirdLocation = new JWPoint(tlx, sly);
                        JwArc ja = new JwArc(Location, bottomSpacing / Drawing.Scale, 3.1415926535897931, -2.4035303736600242);
                        var l = ja.ArcFinish;
                        this.Arc = ja;
                        Slash = new JwXian(topother, l);
                        break;
                    }

            }
        }

        //enum TemplateType { Horizontal, Vertical }

        //struct DirConfig
        //{
        //    public TemplateType Type;
        //    public bool MirrorX;
        //    public bool MirrorY;
        //    public double ArcStart;
        //    public double ArcSweep;
        //}

        //private static readonly Dictionary<BeamEndPosition, DirConfig> DirMap =
        //    new Dictionary<BeamEndPosition, DirConfig>
        //    {
        //        // 水平模板（上/下）
        //        [BeamEndPosition.上右] = new DirConfig { Type = TemplateType.Horizontal, MirrorX = false, MirrorY = false, ArcStart = Math.PI / 2, ArcSweep = -2.4035303736600242 },
        //        [BeamEndPosition.上左] = new DirConfig { Type = TemplateType.Horizontal, MirrorX = true, MirrorY = false, ArcStart = Math.PI / 2, ArcSweep = 2.4035303736600242 },

        //        [BeamEndPosition.下右] = new DirConfig { Type = TemplateType.Horizontal, MirrorX = false, MirrorY = true, ArcStart = -Math.PI / 2, ArcSweep = 2.4035303736600242 },
        //        [BeamEndPosition.下左] = new DirConfig { Type = TemplateType.Horizontal, MirrorX = true, MirrorY = true, ArcStart = -Math.PI / 2, ArcSweep = -2.4035303736600242 },

        //        // 垂直模板（左/右）
        //        [BeamEndPosition.左上] = new DirConfig { Type = TemplateType.Vertical, MirrorX = false, MirrorY = false, ArcStart = 0, ArcSweep = -2.4035303736600242 },
        //        [BeamEndPosition.左下] = new DirConfig { Type = TemplateType.Vertical, MirrorX = false, MirrorY = true, ArcStart = 0, ArcSweep = 2.4035303736600242 },

        //        [BeamEndPosition.右上] = new DirConfig { Type = TemplateType.Vertical, MirrorX = true, MirrorY = false, ArcStart = Math.PI, ArcSweep = 2.4035303736600242 },
        //        [BeamEndPosition.右下] = new DirConfig { Type = TemplateType.Vertical, MirrorX = true, MirrorY = true, ArcStart = Math.PI, ArcSweep = -2.4035303736600242 },
        //    };
        //private JWPoint ApplyOffset(double dx, double dy, DirConfig cfg)
        //{
        //    if (cfg.MirrorX) dx = -dx;
        //    if (cfg.MirrorY) dy = -dy;

        //    return new JWPoint(
        //        Location.X + dx / Drawing.Scale,
        //        Location.Y + dy / Drawing.Scale
        //    );
        //}
        //private void createOther1()
        //{
        //    var cfg = DirMap[Position];

        //    // -------------------------
        //    // 水平模板（上/下）
        //    // -------------------------
        //    if (cfg.Type == TemplateType.Horizontal)
        //    {
        //        // 第二孔
        //        SecondLoaction = ApplyOffset(-secondCenterSpacing, 0, cfg);

        //        // 底边
        //        var sidebottom = ApplyOffset(-longestsideLength, -bottomSpacing, cfg);
        //        var ul = ApplyOffset(0, -bottomSpacing, cfg);
        //        BottomLine = new JwXian(sidebottom, ul);

        //        // 侧边
        //        var topside = ApplyOffset(-longestsideLength, sideLength - bottomSpacing, cfg);
        //        SideLine = new JwXian(sidebottom, topside);

        //        // 顶边
        //        var topother = ApplyOffset(-jpSpacing, sideLength - bottomSpacing, cfg);
        //        TopLine = new JwXian(topother, topside);

        //        // 第三孔
        //        ThirdLocation = ApplyOffset(0, JwConstants.EllipseSpacing, cfg);

        //        // 圆弧
        //        Arc = new JwArc(Location, bottomSpacing / Drawing.Scale, cfg.ArcStart, cfg.ArcSweep);
        //        Slash = new JwXian(topother, Arc.ArcFinish);
        //        return;
        //    }

        //    // -------------------------
        //    // 垂直模板（左/右）
        //    // -------------------------
        //    {
        //        // 第二孔
        //        SecondLoaction = ApplyOffset(0, -secondCenterSpacing, cfg);

        //        // 底边
        //        var sidebottom = ApplyOffset(bottomSpacing, -longestsideLength, cfg);
        //        var ul = ApplyOffset(bottomSpacing, 0, cfg);
        //        BottomLine = new JwXian(sidebottom, ul);

        //        // 侧边
        //        var topside = ApplyOffset(-(sideLength - bottomSpacing), -longestsideLength, cfg);
        //        SideLine = new JwXian(sidebottom, topside);

        //        // 顶边
        //        var topother = ApplyOffset(-(sideLength - bottomSpacing), -jpSpacing, cfg);
        //        TopLine = new JwXian(topother, topside);

        //        // 第三孔
        //        ThirdLocation = ApplyOffset(-JwConstants.EllipseSpacing, -secondCenterSpacing, cfg);

        //        // 圆弧
        //        Arc = new JwArc(Location, bottomSpacing / Drawing.Scale, cfg.ArcStart, cfg.ArcSweep);
        //        Slash = new JwXian(topother, Arc.ArcFinish);
        //    }
        //}


        public List<JwwData> DrawToJww()
        {
            var banjing = Drawing.Export.HoleDiameter / (2 * Drawing.Scale);
            List<JwwData> jwws = new List<JwwData>();
            jwws.Add(SideLine.ToJwwData(DrawShapeType.LinkPart));
            jwws.Add(TopLine.ToJwwData(DrawShapeType.LinkPart));
            jwws.Add(BottomLine.ToJwwData(DrawShapeType.LinkPart));
            jwws.Add(Slash.ToJwwData(DrawShapeType.LinkPart));
            jwws.Add(Arc.ToJwwData(DrawShapeType.LinkPart));
            JwwEnko enko = new JwwEnko();
            enko.m_nPenColor = 2;
            enko.m_dHankei = banjing;
            enko.m_radKaishiKaku = 0;
            enko.m_radEnkoKaku = 6.2831853;
            enko.m_radKatamukiKaku = 0;
            enko.m_dHenpeiRitsu = 1;
            enko.m_bZenEnFlg = 1;
            enko.m_start_x = Location.X;
            enko.m_start_y = Location.Y;
            enko.m_nLayer = (int)DrawShapeType.LinkPart + 1;
            //tentwo.m_nLayer = (int)DrawShapeType.LinkPart + 1;
            jwws.Add(enko);
            return jwws;
        }
        // 移植：屏幕绘制方法（Draw / BuildPath / DrawCircles，依赖 WinForms 的 Graphics）已移除，Web 端在前端渲染。
    }


    /// <summary>
    /// jw圆弧
    /// </summary>
    public class JwArc
    {
        public string Id { get; set; }
        public JWPoint Center { get; }
        public double Radius { get; }


        /// <summary>
        /// radkatamukikaku 起始度数 0 与x轴正方向相同
        /// -1.5707963267948966 
        /// </summary>
        public double StartAngle { get; }

        /// <summary>
        /// radenkokaku 旋转角度 2.4035303736600242 固定正为逆时针
        /// </summary>
        public double SweepAngle { get; }

        public JWPoint ArcFinish { get; set; }

        public JwArc(JWPoint center, double radius, double startAngle, double endAngle)
        {
            Id=Guid.NewGuid().ToString();
            Center = center;
            Radius = radius;
            StartAngle = startAngle;
            SweepAngle = endAngle;
            ArcFinish=JwExtend.GetArcEndPoint(center,radius, -startAngle, -endAngle);
        }


        public JwwData ToJwwData(DrawShapeType shapeType)
        {
            var sen = new JwwEnko();
            //sen.m_nPenWidth=1/
            sen.m_nPenColor = (short)shapeType;

            sen.m_start_x = Center.X;
            sen.m_start_y = Center.Y;
            sen.m_dHankei=Radius;
            sen.m_nLayer = (short)((int)shapeType + 1);
            sen.m_nPenColor = (short)((int)shapeType);
            sen.m_nPenStyle = 1;
            sen.m_nPenWidth = 0;
            sen.m_radEnkoKaku = -SweepAngle;
            sen.m_radKatamukiKaku= -StartAngle;
            sen.m_bZenEnFlg = 0;
            sen.m_dHenpeiRitsu = 1;
            return sen;
        }
    }

}
