// Tier 0 — tipos matematicos. Reimplementacao limpa baseada na checklist
// docs/apicheck_stardust.json (UnityEngine.CoreModule). Ordem de rotacao
// Euler: TODO validar paridade contra Unity (usa ordem propria).
using System;

namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public Vector3 normalized { get { float m = magnitude; return m > 1e-6f ? this / m : zero; } }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => a * d;
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        }
        public static Vector3 Project(Vector3 v, Vector3 normal)
        {
            float denom = Dot(normal, normal);
            return denom < 1e-6f ? zero : normal * (Dot(v, normal) / denom);
        }
        public static Vector3 SmoothDamp(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime)
        {
            // Aproximacao: TODO paridade exata com Unity (usa spring-damper com maxSpeed).
            float t = 1f - (float)Math.Exp(-smoothTime <= 0 ? 1 : 1f / smoothTime * Time.deltaTime);
            Vector3 result = Lerp(current, target, t);
            velocity = (result - current) / Math.Max(Time.deltaTime, 1e-6f);
            return result;
        }
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public Vector3 eulerAngles => ToEuler(this);
        public static Quaternion Euler(float x, float y, float z) => FromEuler(x, y, z);
        public static Quaternion Euler(Vector3 e) => FromEuler(e.x, e.y, e.z);
        public static Quaternion AngleAxis(float angleDeg, Vector3 axis)
        {
            Vector3 n = axis.normalized;
            float half = angleDeg * (float)Math.PI / 360f;
            float s = (float)Math.Sin(half);
            return new Quaternion(n.x * s, n.y * s, n.z * s, (float)Math.Cos(half));
        }
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            Quaternion p = new Quaternion(v.x, v.y, v.z, 0);
            Quaternion conj = new Quaternion(-q.x, -q.y, -q.z, q.w);
            Quaternion r = q * p * conj;
            return new Vector3(r.x, r.y, r.z);
        }
        public static Quaternion Lerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            Quaternion r = new Quaternion(
                a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t,
                a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
            return Normalize(r);
        }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => Lerp(a, b, t); // TODO arco real
        public static Quaternion LookRotation(Vector3 forward, Vector3 up)
        {
            Vector3 f = forward.normalized;
            Vector3 r = Vector3.Cross(up, f).normalized;
            Vector3 u = Vector3.Cross(f, r);
            float m00 = r.x, m01 = u.x, m02 = f.x;
            float m10 = r.y, m11 = u.y, m12 = f.y;
            float m20 = r.z, m21 = u.z, m22 = f.z;
            float trace = m00 + m11 + m22;
            if (trace > 0)
            {
                float s = (float)Math.Sqrt(trace + 1f) * 2f;
                return Normalize(new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, s / 4f));
            }
            if (m00 > m11 && m00 > m22)
            {
                float s = (float)Math.Sqrt(1f + m00 - m11 - m22) * 2f;
                return Normalize(new Quaternion(s / 4f, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s));
            }
            if (m11 > m22)
            {
                float s = (float)Math.Sqrt(1f + m11 - m00 - m22) * 2f;
                return Normalize(new Quaternion((m01 + m10) / s, s / 4f, (m12 + m21) / s, (m02 - m20) / s));
            }
            float s2 = (float)Math.Sqrt(1f + m22 - m00 - m11) * 2f;
            return Normalize(new Quaternion((m02 + m20) / s2, (m12 + m21) / s2, s2 / 4f, (m10 - m01) / s2));
        }
        public static Quaternion LookRotation(Vector3 forward) => LookRotation(forward, Vector3.up);
        public static Quaternion RotateTowards(Quaternion from, Quaternion to, float maxDegreesDelta)
        {
            float angle = Angle(from, to);
            if (angle <= maxDegreesDelta || angle < 1e-4f) return to;
            return Slerp(from, to, maxDegreesDelta / angle);
        }
        public static float Angle(Quaternion a, Quaternion b)
        {
            float dot = Math.Min(1f, Math.Abs(a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w));
            return (float)Math.Acos(dot) * 2f * 180f / (float)Math.PI;
        }
        static Quaternion Normalize(Quaternion q)
        {
            float m = (float)Math.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return m > 1e-6f ? new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m) : identity;
        }
        static Quaternion FromEuler(float xDeg, float yDeg, float zDeg)
        {
            float cx = (float)Math.Cos(xDeg * Math.PI / 360.0), sx = (float)Math.Sin(xDeg * Math.PI / 360.0);
            float cy = (float)Math.Cos(yDeg * Math.PI / 360.0), sy = (float)Math.Sin(yDeg * Math.PI / 360.0);
            float cz = (float)Math.Cos(zDeg * Math.PI / 360.0), sz = (float)Math.Sin(zDeg * Math.PI / 360.0);
            return new Quaternion(
                sx * cy * cz + cx * sy * sz,
                cx * sy * cz - sx * cy * sz,
                cx * cy * sz - sx * sy * cz,
                cx * cy * cz + sx * sy * sz);
        }
        static Vector3 ToEuler(Quaternion q)
        {
            float ysqr = q.y * q.y;
            float t0 = 2f * (q.w * q.x + q.y * q.z);
            float t1 = 1f - 2f * (q.x * q.x + ysqr);
            float x = (float)Math.Atan2(t0, t1);
            float t2 = 2f * (q.w * q.y - q.z * q.x);
            t2 = Math.Max(-1f, Math.Min(1f, t2));
            float y = (float)Math.Asin(t2);
            float t3 = 2f * (q.w * q.z + q.x * q.y);
            float t4 = 1f - 2f * (ysqr + q.z * q.z);
            float z = (float)Math.Atan2(t3, t4);
            float rad2deg = 180f / (float)Math.PI;
            return new Vector3(x * rad2deg, y * rad2deg, z * rad2deg);
        }
    }

    public struct Matrix4x4
    {
        float[] m; // row-major, 16
        public Matrix4x4(float[] values) { m = (float[])values.Clone(); }
        public static Matrix4x4 identity => new Matrix4x4(new float[16] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 });
        public static Matrix4x4 TRS(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            var r = new Matrix4x4(new float[16]);
            float x2 = rot.x + rot.x, y2 = rot.y + rot.y, z2 = rot.z + rot.z;
            float xx = rot.x * x2, xy = rot.x * y2, xz = rot.x * z2;
            float yy = rot.y * y2, yz = rot.y * z2, zz = rot.z * z2;
            float wx = rot.w * x2, wy = rot.w * y2, wz = rot.w * z2;
            r.m[0] = (1 - (yy + zz)) * scale.x; r.m[1] = (xy - wz) * scale.x; r.m[2] = (xz + wy) * scale.x; r.m[3] = pos.x;
            r.m[4] = (xy + wz) * scale.y; r.m[5] = (1 - (xx + zz)) * scale.y; r.m[6] = (yz - wx) * scale.y; r.m[7] = pos.y;
            r.m[8] = (xz - wy) * scale.z; r.m[9] = (yz + wx) * scale.z; r.m[10] = (1 - (xx + yy)) * scale.z; r.m[11] = pos.z;
            r.m[12] = 0; r.m[13] = 0; r.m[14] = 0; r.m[15] = 1;
            return r;
        }
        public void SetTRS(Vector3 pos, Quaternion rot, Vector3 scale) { this = TRS(pos, rot, scale); }
        /// <summary>Multiplicação row-major (a * b).</summary>
        public static Matrix4x4 operator *(Matrix4x4 a, Matrix4x4 b)
        {
            var r = new float[16];
            for (int row = 0; row < 4; row++)
                for (int col = 0; col < 4; col++)
                    r[row * 4 + col] = a.m[row * 4] * b.m[col] + a.m[row * 4 + 1] * b.m[4 + col]
                        + a.m[row * 4 + 2] * b.m[8 + col] + a.m[row * 4 + 3] * b.m[12 + col];
            return new Matrix4x4(r);
        }
        /// <summary>Escala pura row-major.</summary>
        /// <summary>Escala pura row-major.</summary>
        public static Matrix4x4 Scale(Vector3 s) => new Matrix4x4(new float[16] {
            s.x, 0, 0, 0, 0, s.y, 0, 0, 0, 0, s.z, 0, 0, 0, 0, 1 });
        /// <summary>Perspectiva OpenGL row-major (fovY em graus).</summary>
        public static Matrix4x4 Perspective(float fovY, float aspect, float near, float far)
        {
            float f = 1f / (float)Math.Tan(fovY * 0.5f * (float)Math.PI / 180f);
            float fn = far - near;
            return new Matrix4x4(new float[16] {
                f / aspect, 0, 0, 0,
                0, f, 0, 0,
                0, 0, -(far + near) / fn, -2f * far * near / fn,
                0, 0, -1, 0 });
        }
        /// <summary>Inversa geral 4x4 (para view matrix).</summary>
        public Matrix4x4 Inverse()
        {
            float[] a = m, inv = new float[16];
            inv[0] = a[5]*a[10]*a[15] - a[5]*a[11]*a[14] - a[9]*a[6]*a[15] + a[9]*a[7]*a[14] + a[13]*a[6]*a[11] - a[13]*a[7]*a[10];
            inv[4] = -a[4]*a[10]*a[15] + a[4]*a[11]*a[14] + a[8]*a[6]*a[15] - a[8]*a[7]*a[14] - a[12]*a[6]*a[11] + a[12]*a[7]*a[10];
            inv[8] = a[4]*a[9]*a[15] - a[4]*a[11]*a[13] - a[8]*a[5]*a[15] + a[8]*a[7]*a[13] + a[12]*a[5]*a[11] - a[12]*a[7]*a[9];
            inv[12] = -a[4]*a[9]*a[14] + a[4]*a[10]*a[13] + a[8]*a[5]*a[14] - a[8]*a[6]*a[13] - a[12]*a[5]*a[10] + a[12]*a[6]*a[9];
            inv[1] = -a[1]*a[10]*a[15] + a[1]*a[11]*a[14] + a[9]*a[2]*a[15] - a[9]*a[3]*a[14] - a[13]*a[2]*a[11] + a[13]*a[3]*a[10];
            inv[5] = a[0]*a[10]*a[15] - a[0]*a[11]*a[14] - a[8]*a[2]*a[15] + a[8]*a[3]*a[14] + a[12]*a[2]*a[11] - a[12]*a[3]*a[10];
            inv[9] = -a[0]*a[9]*a[15] + a[0]*a[11]*a[13] + a[8]*a[1]*a[15] - a[8]*a[3]*a[13] - a[12]*a[1]*a[11] + a[12]*a[3]*a[9];
            inv[13] = a[0]*a[9]*a[14] - a[0]*a[10]*a[13] - a[8]*a[1]*a[14] + a[8]*a[2]*a[13] + a[12]*a[1]*a[10] - a[12]*a[2]*a[9];
            inv[2] = a[1]*a[6]*a[15] - a[1]*a[7]*a[14] - a[5]*a[2]*a[15] + a[5]*a[3]*a[14] + a[13]*a[2]*a[7] - a[13]*a[3]*a[6];
            inv[6] = -a[0]*a[6]*a[15] + a[0]*a[7]*a[14] + a[4]*a[2]*a[15] - a[4]*a[3]*a[14] - a[12]*a[2]*a[7] + a[12]*a[3]*a[6];
            inv[10] = a[0]*a[5]*a[15] - a[0]*a[7]*a[13] - a[4]*a[1]*a[15] + a[4]*a[3]*a[13] + a[12]*a[1]*a[7] - a[12]*a[3]*a[5];
            inv[14] = -a[0]*a[5]*a[14] + a[0]*a[6]*a[13] + a[4]*a[1]*a[14] - a[4]*a[2]*a[13] - a[12]*a[1]*a[6] + a[12]*a[2]*a[5];
            inv[3] = -a[1]*a[6]*a[11] + a[1]*a[7]*a[10] + a[5]*a[2]*a[11] - a[5]*a[3]*a[10] - a[9]*a[2]*a[7] + a[9]*a[3]*a[6];
            inv[7] = a[0]*a[6]*a[11] - a[0]*a[7]*a[10] - a[4]*a[2]*a[11] + a[4]*a[3]*a[10] + a[8]*a[2]*a[7] - a[8]*a[3]*a[6];
            inv[11] = -a[0]*a[5]*a[11] + a[0]*a[7]*a[9] + a[4]*a[1]*a[11] - a[4]*a[3]*a[9] - a[8]*a[1]*a[7] + a[8]*a[3]*a[5];
            inv[15] = a[0]*a[5]*a[10] - a[0]*a[6]*a[9] - a[4]*a[1]*a[10] + a[4]*a[2]*a[9] + a[8]*a[1]*a[6] - a[8]*a[2]*a[5];
            float det = a[0]*inv[0] + a[1]*inv[4] + a[2]*inv[8] + a[3]*inv[12];
            if (Math.Abs(det) < 1e-12) return identity;
            det = 1f / det;
            for (int i = 0; i < 16; i++) inv[i] *= det;
            return new Matrix4x4(inv);
        }
        /// <summary>Projeção ortográfica row-major (consistente com TRS/identity).
        /// Upload para GL deve usar transpose=true (ver GLES2Renderer.PassUniform).</summary>
        public static Matrix4x4 Ortho(float left, float right, float bottom, float top, float near, float far)
        {
            float rl = right - left, tb = top - bottom, fn = far - near;
            return new Matrix4x4(new float[16] {
                2f / rl, 0, 0, -(right + left) / rl,
                0, 2f / tb, 0, -(top + bottom) / tb,
                0, 0, -2f / fn, -(far + near) / fn,
                0, 0, 0, 1 });
        }
        public Vector3 MultiplyPoint3x4(Vector3 p) => new Vector3(
            m[0] * p.x + m[1] * p.y + m[2] * p.z + m[3],
            m[4] * p.x + m[5] * p.y + m[6] * p.z + m[7],
            m[8] * p.x + m[9] * p.y + m[10] * p.z + m[11]);
        /// <summary>Acesso aos dados para uniformes GL.</summary>
        public float this[int i] => m[i];
        public float[] Data => m;
    }

    public static class Mathf
    {
        public static float Abs(float v) => Math.Abs(v);
        public static double Acos(double d) => Math.Acos(d);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => Clamp(v, 0, 1);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Max(float a, float b) => a > b ? a : b;
        public static float PingPong(float t, float length)
        {
            if (length <= 0) return 0;
            t = t % (2 * length);
            return length - Math.Abs(t - length);
        }
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01((t - from) / (to - from));
            return t * t * (3 - 2 * t);
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color black => new Color(0, 0, 0);
        public static Color white => new Color(1, 1, 1);
        public static Color Lerp(Color a, Color b, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
    }

    public struct Bounds
    {
        Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public Vector3 min => center - size / 2;
        public Vector3 max => center + size / 2;
    }

    public struct Ray
    {
        public Vector3 origin, direction;
        public Ray(Vector3 origin, Vector3 direction) { this.origin = origin; this.direction = direction; }
    }

    public struct Keyframe
    {
        public float value;
        public Keyframe(float time, float value) { this.value = value; }
    }

    public static class Random
    {
        static System.Random rng = new System.Random();
        public static int Range(int min, int max) => rng.Next(min, max);
        public static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        public static Vector3 insideUnitSphere
        {
            get
            {
                Vector3 v;
                do { v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)); }
                while (v.magnitude > 1);
                return v;
            }
        }
    }
}
