using Godot;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;

namespace HordeShooter;

/// <summary>
/// Little-endian byte packing for network messages (doc/multiplayer-sync-design.md 4장). Positions are centimetres in a
/// signed 24-bit value (about ±83 km), directions a byte of yaw, unit values a byte.
/// </summary>
public sealed class NetWriter
{
    public const int PositionBytes = 9;
    private readonly List<byte> _bytes = new(256);
    public int Length => _bytes.Count;
    public byte[] ToArray() => _bytes.ToArray();
    /// <summary>Leaves room for a 16-bit value filled in later (an event's length).</summary>
    public int ReserveU16() { int at = _bytes.Count; _bytes.Add(0); _bytes.Add(0); return at; }
    public void PatchU16(int at, int v) { _bytes[at] = (byte)(v & 0xFF); _bytes[at + 1] = (byte)((v >> 8) & 0xFF); }

    public void U8(int v) => _bytes.Add((byte)v);
    public void S8(int v) => _bytes.Add((byte)(sbyte)Math.Clamp(v, sbyte.MinValue, sbyte.MaxValue));
    public void U16(int v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteUInt16LittleEndian(b, (ushort)v); _bytes.Add(b[0]); _bytes.Add(b[1]); }
    public void S16(int v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteInt16LittleEndian(b, (short)Math.Clamp(v, short.MinValue, short.MaxValue)); _bytes.Add(b[0]); _bytes.Add(b[1]); }
    public void I32(int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(b, v); foreach (var x in b) _bytes.Add(x); }
    public void F32(float v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteSingleLittleEndian(b, v); foreach (var x in b) _bytes.Add(x); }
    /// <summary>Metres → centimetres.</summary>
    public void Cm(float metres)
    {
        double cm = Math.Round((double)metres * 100);
        if (!float.IsFinite(metres) || cm < -8388608 || cm > 8388607) throw new ArgumentOutOfRangeException(nameof(metres), "Coordinate exceeds signed24 centimetres");
        int value = (int)cm;
        U8(value); U8(value >> 8); U8(value >> 16);
    }
    public void Pos(Vector3 p) { Cm(p.X); Cm(p.Y); Cm(p.Z); }
    public void FlatPos(Vector3 p) { Cm(p.X); Cm(p.Z); }
    /// <summary>Horizontal direction as one byte of yaw (1.4° steps).</summary>
    public void Yaw(Vector3 dir) => U8(Mathf.PosMod(Mathf.RoundToInt(Mathf.Atan2(dir.X, dir.Z) / Mathf.Tau * 256), 256));
    /// <summary>−1..1 as one signed byte.</summary>
    public void Unit(float v) => S8(Mathf.RoundToInt(Mathf.Clamp(v, -1, 1) * 127));
    public void Str(string s)
    {
        var b = System.Text.Encoding.UTF8.GetBytes(s);
        U8(Math.Min(255, b.Length));
        for (int i = 0; i < Math.Min(255, b.Length); i++) _bytes.Add(b[i]);
    }
}

public sealed class NetReader
{
    private readonly byte[] _data;
    private int _at;
    public NetReader(byte[] data) => _data = data;
    /// <summary>The next <paramref name="n"/> bytes as their own reader (one event), skipping them here.</summary>
    public NetReader Slice(int n)
    {
        var part = new byte[n];
        System.Array.Copy(_data, _at, part, 0, n);
        _at += n;
        return new NetReader(part);
    }
    public bool More => _at < _data.Length;
    public int Remaining => _data.Length - _at;

    public int U8() => _data[_at++];
    public int S8() => (sbyte)_data[_at++];
    public int U16() { int v = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_at, 2)); _at += 2; return v; }
    public int S16() { int v = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(_at, 2)); _at += 2; return v; }
    public int I32() { int v = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(_at, 4)); _at += 4; return v; }
    public float F32() { float v = BinaryPrimitives.ReadSingleLittleEndian(_data.AsSpan(_at, 4)); _at += 4; return v; }
    public float Cm()
    {
        int value = U8() | U8() << 8 | U8() << 16;
        if ((value & 0x800000) != 0) value |= unchecked((int)0xFF000000);
        return value / 100f;
    }
    public Vector3 Pos() => new(Cm(), Cm(), Cm());
    public Vector3 FlatPos() { float x = Cm(), z = Cm(); return new Vector3(x, 0, z); }
    public Vector3 Yaw() { float a = U8() / 256f * Mathf.Tau; return new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); }
    public float Unit() => S8() / 127f;
    public string Str()
    {
        int n = U8();
        var s = System.Text.Encoding.UTF8.GetString(_data, _at, n);
        _at += n;
        return s;
    }
}
