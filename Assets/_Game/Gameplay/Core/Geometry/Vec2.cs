using System;

namespace Game.Gameplay.Geometry
{
    /// <summary>A point or direction on the logical XZ plane. Doubles keep generation deterministic and tolerant.</summary>
    public readonly struct Vec2 : IEquatable<Vec2>
    {
        private const int HashPrime = 397;

        public readonly double X;
        public readonly double Z;

        public Vec2(double x, double z)
        {
            X = x;
            Z = z;
        }

        public static readonly Vec2 Zero = new Vec2(0, 0);
        public static readonly Vec2 Forward = new Vec2(0, 1);

        public double LengthSquared => X * X + Z * Z;
        public double Length => Math.Sqrt(LengthSquared);
        public Vec2 Normalized
        {
            get
            {
                var length = Length;

                return length > 0
                    ? new Vec2(X / length, Z / length)
                    : Zero;
            }
        }
        public Vec2 Perp => new Vec2(-Z, X);

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Z + b.Z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Z - b.Z);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Z);
        public static Vec2 operator *(Vec2 a, double k) => new Vec2(a.X * k, a.Z * k);
        public static Vec2 operator *(double k, Vec2 a) => new Vec2(a.X * k, a.Z * k);

        public static double Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Z * b.Z;
        public static double Cross(Vec2 a, Vec2 b) => a.X * b.Z - a.Z * b.X;
        public static double Distance(Vec2 a, Vec2 b) => (a - b).Length;
        public static Vec2 Lerp(Vec2 a, Vec2 b, double t) => new Vec2(a.X + (b.X - a.X) * t, a.Z + (b.Z - a.Z) * t);

        public bool Equals(Vec2 other) => X.Equals(other.X) && Z.Equals(other.Z);
        public override bool Equals(object obj) => obj is Vec2 other && Equals(other);
        public override int GetHashCode() => (X.GetHashCode() * HashPrime) ^ Z.GetHashCode();
        public override string ToString() => $"({X:0.###}, {Z:0.###})";
    }
}
