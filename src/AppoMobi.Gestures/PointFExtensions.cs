using System.Drawing;
using PointF = System.Drawing.PointF;

namespace AppoMobi.Gestures;

public static class PointFExtensions
{
    public static float Length(this PointF point)
    {
        return (float)Math.Sqrt(point.X * point.X + point.Y * point.Y);
    }

    public static PointF Add(this PointF lhs, PointF rhs)
    {
        return new PointF(lhs.X + rhs.X, lhs.Y + rhs.Y);
    }

    public static PointF Subtract(this PointF lhs, PointF rhs)
    {
        return new PointF(lhs.X - rhs.X, lhs.Y - rhs.Y);
    }

    public static SizeF SubtractDistance(this PointF pointA, PointF pointB)
    {
        return new SizeF(
            pointA.X - pointB.X,
            pointA.Y - pointB.Y);
    }

    public static SizeF AddDistance(this PointF pointA, PointF pointB)
    {
        return new SizeF(
            pointA.X + pointB.X,
            pointA.Y + pointB.Y);
    }

    public static PointF Multiply(float lhs, PointF rhs)
    {
        return new PointF(lhs * rhs.X, lhs * rhs.Y);
    }

    public static PointF Divide(this PointF lhs, float rhs)
    {
        return new PointF(lhs.X / rhs, lhs.Y / rhs);
    }
}
