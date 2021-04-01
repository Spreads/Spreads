// using System;
// using System.Runtime.CompilerServices;
// using System.Runtime.InteropServices;
// using Spreads.Collections.Generic;
//
// namespace Spreads.Text
// {
//     [StructLayout(LayoutKind.Explicit, Size = 16)]
//     public readonly struct StringSegment : IEquatable<StringSegment>, IComparable<StringSegment>
//     {
//         public static readonly StringSegment Empty = new(string.Empty);
//         public static readonly StringSegment Null;
//
//         [FieldOffset(0)]
//         public readonly string? Text;
//         [FieldOffset(8)]
//         public readonly Segment Segment;
//
//         public StringSegment(string? text)
//         {
//             if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
//             Text = text;
//             Segment = new Segment(0, text.Length);
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public StringSegment(string text, Segment segment)
//         {
//             if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
//             Text = text;
//             Segment = segment;
//         }
//
//         public StringSegment(string text, int start, int length)
//         {
//             if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
//             Text = text;
//             Segment = new Segment(start, length);
//         }
//
//         public bool IsNull => Text == null!;
//         public bool IsNullOrEmpty => Segment.Length == 0;
//         public bool IsNullOrWhiteSpace => Segment.Length == 0 || string.IsNullOrWhiteSpace(Text);
//         public bool IsEmpty => Segment.Length == 0;
//
//         // public bool StartsWith(string value) => Text.StartsWith(value);
//
//         public StringSegment TrimNewLineAtEnd()
//         {
//             if (Text == null) return Null;
//
//             var span = Span;
//             int pos;
//             for (pos = span.Length - 1; pos >= 0; pos--)
//             {
//                 var c = span[pos];
//                 if (!(c == '\n' || c == '\r'))
//                     break;
//             }
//
//             return new StringSegment(Text, Segment.Start, pos + 1);
//         }
//
//         public ReadOnlySpan<char> Span
//         {
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             get => Text.AsSpan(Segment.Start, Segment.Length);
//         }
//
//         public override string? ToString()
//         {
//             return Text?.Substring(Segment.Start, Segment.Length);
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public bool Equals(StringSegment other)
//         {
//             int segmentLength = Segment.Length;
//
//             if (segmentLength != other.Segment.Length)
//                 return false;
//
//             if (segmentLength == 0)
//                 return true;
//
//             ref char first = ref Unsafe.AddByteOffset(ref Unsafe.As<Box<char>>(Text)!.Value, Box<char>.StringOffset + Segment.Start * sizeof(char));
//             ref char otherFirst = ref Unsafe.AddByteOffset(ref Unsafe.As<Box<char>>(other.Text)!.Value, Box<char>.StringOffset + other.Segment.Start * sizeof(char));
//
//             for (var i = 0; i < segmentLength; i++)
//             {
//                 if (Unsafe.Add(ref first, i) != Unsafe.Add(ref otherFirst, i))
//                     return false;
//             }
//
//             return true;
//         }
//
//         public bool ReferenceEquals(StringSegment other)
//         {
//             return ReferenceEquals(Text, other.Text) && Segment.Equals(other.Segment);
//         }
//
//         public override bool Equals(object? obj)
//         {
//             return obj is StringSegment other && Equals(other);
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public override int GetHashCode()
//         {
//             return GetHashCode(Text, Segment.Start, Segment.Length);
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public static int GetHashCode(string str)
//         {
//             return GetHashCode(str, 0, str.Length);
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public static int GetHashCode(string? str, int start, int length)
//         {
//             if (str == null) return 0;
//
//             unchecked
//             {
//                 // Lazy FNV, just read 2 chars as int
//                 // Would be nice to put xxh3 here, it's optimized for short length.
//
//                 ref char first = ref Unsafe.AddByteOffset(ref Unsafe.As<Box<char>>(str)!.Value, Box<char>.StringOffset + start * sizeof(char));
//
//                 var hashCode = (int) 2166136261;
//
//                 int i = 0;
//
//                 for (; i < length - 2; i += 2)
//                 {
//                     hashCode = (hashCode ^ Unsafe.As<char, int>(ref Unsafe.Add(ref first, i))) * 16777619;
//                 }
//
//                 for (; i < length; i++)
//                 {
//                     hashCode = (hashCode ^ Unsafe.Add(ref first, i)) * 16777619;
//                 }
//
//                 return hashCode;
//             }
//         }
//
//         public int CompareTo(StringSegment other)
//         {
//             return MemoryExtensions.SequenceCompareTo(Span, other.Span);
//         }
//
//         public static implicit operator StringSegment(string text)
//         {
//             return new(text);
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public static bool operator ==(StringSegment left, StringSegment right)
//         {
//             return left.Equals(right);
//         }
//
//         [MethodImpl(MethodImplOptions.AggressiveInlining)]
//         public static bool operator !=(StringSegment left, StringSegment right)
//         {
//             return !left.Equals(right);
//         }
//     }
// }
