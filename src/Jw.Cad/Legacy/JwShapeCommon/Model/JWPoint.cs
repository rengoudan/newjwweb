using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Point = NetTopologySuite.Geometries.Point;

namespace JwShapeCommon
{
    public class JWPoint : IEquatable<JWPoint>, ICanZoom, IChangeAxis
    {

        public double X { get; set; }
        public double Y { get; set; }

        public bool IsValid { get; set; }

        public JWPoint()
        {
            IsValid = false;
        }

        public JWPoint(double x, double y)
        {
            if (JwConstants.PickPrecision == 0)
            {

                X = x;
                Y = y;
            }
            else
            {
                X = Math.Round(x, JwConstants.PickPrecision);
                Y = Math.Round(y, JwConstants.PickPrecision);
            }
            
            IsValid = true;
        }

        public JWPoint(bool isNull)
        {
            IsValid = false;
        }

        public JWPoint(string x, string y)
        {
            X = Convert.ToDouble(x);
            Y = Convert.ToDouble(y);
            IsValid = true;
        }
        public static bool operator ==(JWPoint? left, JWPoint? right)
        {
            if(object.ReferenceEquals(left, null) || object.ReferenceEquals(right, null))
            {
                return false;
            }
            else
            {
                //return Math.Round(left.X, 4) ==  Math.Round(right.X, 4) && Math.Round(left.Y, 4) == Math.Round(right.Y, 4);
                return left?.X == right?.X && left?.Y == right?.Y;
            }
        }
            //=>  left?.X == right?.X && left?.Y == right?.Y

        public static bool operator !=(JWPoint? left, JWPoint? right) => !(left == right);

        public override bool Equals([NotNullWhen(true)] object? obj) => obj is JWPoint && Equals((JWPoint)obj);

       // public bool Equals(JWPoint other) => this == other;

        public string GetPointString()
        {
            return string.Format("{0} {1}", X, Y);
        }

        public bool Equals(JWPoint? other)
        {
            if (object.ReferenceEquals(other,null))
            {
                return false;
            }
            else
            {
                return this == other;
            }
        }

        // 移植：容差按图纸缩尺（10mm），缩尺由调用方显式传入
        public bool IsEqualsWithError(JWPoint? other, double scale)
        {
            if (object.ReferenceEquals(other, null))
            {
                return false;
            }
            else
            {

                 return (Math.Abs( (this.X - other.X)) < (10 / scale)) && (Math.Abs((this.Y - other.Y)) < (10 / scale));

            }
        }

        public void Zoom(double zoom)
        {
            X *= zoom; Y *= zoom;
        }

        public void ChangeAxis(double x, double y)
        {
            Y = y - Y;
            X = X + x;
        }

        /// <summary>
        /// 转换为数据库存储的
        /// </summary>
        /// <returns></returns>
        public Point ToPoint()
        {
            Point p=new Point(this.X,this.Y);
            return p;
        }

        public PointF ToPointF()
        {
            PointF p = new PointF((float)this.X,(float)this.Y);
            return p;
        }

        public bool Equals2D(JWPoint other)
        {
            return X == other.X && Y == other.Y;
        }

        // 移植：按图纸缩尺决定的位数取整，位数由调用方显式传入（JwDrawing.Precision）
        public JWPoint Jiangjingdu(int precision)
        {
            JWPoint reobj = new JWPoint();
            reobj.X=Math.Round(X, precision);
            reobj.Y=Math.Round(Y, precision);
            return reobj;
        }

    }

    public class JwPointComparint : IEqualityComparer<JWPoint>
    {
        public bool Equals(JWPoint? x, JWPoint? y)
        {
            if(object.ReferenceEquals(x, null))
            {
                return false;
            }
            if (object.ReferenceEquals(y,null))
            {
                return false;
            }
            else
            {
               return x.Equals(y);
            }
        }

        public int GetHashCode(JWPoint obj)
        {
            return obj.GetPointString().GetHashCode();
        }

    }
}
