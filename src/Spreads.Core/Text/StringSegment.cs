using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Spreads.Buffers;
using Spreads.Collections.Generic;

namespace Spreads.Text
{
    // TODO OwnedStringSegment that is backed by a pooled char[], should work with native memory: copy to a borrowed array and return it to pool on dispose

    /// <summary>
    /// A non-ref (could be stored as a field of a class) <see cref="ReadOnlySpan{T}"/>-like structure that wraps a segment of a <see cref="string"/> or an array of <see cref="char"/>s.
    /// Does not distinguish between <see langword="null"/> and <see cref="string.Empty"/> so that empty <see cref="StringSegment"/>s are always equal.
    /// </summary>
    [DebuggerDisplay("{ToString()}")]
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public readonly struct StringSegment : ISegment<StringSegment, char>
    {
        public static readonly StringSegment Empty;

        // Could also do native pointer, but chars/UTF16 are less common in native, and that would require branching

        [FieldOffset(0)]
        private readonly object _object;

        /// <summary>
        /// Offset from the first char of a string. If _object is char[], _start is adjusted by <see cref="TypeHelper.ArrayOffset"/> -<see cref="TypeHelper.StringOffset"/>.
        /// </summary>
        [FieldOffset(8)]
        private readonly int _byteStart;

        [FieldOffset(12)]
        private readonly int _charLength;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private StringSegment(object obj, int byteStart, int charLength)
        {
            _object = obj;
            _byteStart = byteStart;
            _charLength = charLength;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment(string text)
        {
            if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
            _object = text;
            _byteStart = TypeHelper.StringOffsetInt;
            _charLength = text.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment(char[] text)
        {
            if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
            _object = text;
            _byteStart = TypeHelper.ArrayOffsetInt;
            _charLength = text.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment(string text, Segment segment)
        {
            if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
            ThrowHelper.EnsureOffsetLength(segment.Start, segment.Length, text.Length);
            _object = text;
            _byteStart = TypeHelper.StringOffsetInt + segment.Start * Unsafe.SizeOf<char>();
            _charLength = segment.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment(char[] text, Segment segment)
        {
            if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
            ThrowHelper.EnsureOffsetLength(segment.Start, segment.Length, text.Length);
            _object = text;
            _byteStart = TypeHelper.ArrayOffsetInt + segment.Start * Unsafe.SizeOf<char>();
            _charLength = segment.Length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment(string text, int start, int length)
        {
            if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
            ThrowHelper.EnsureOffsetLength(start, length, text.Length);
            _object = text;
            _byteStart = TypeHelper.StringOffsetInt + start * Unsafe.SizeOf<char>();
            _charLength = length;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment(char[] text, int start, int length)
        {
            if (text == null!) ThrowHelper.ThrowArgumentNullException(ExceptionArgument.text);
            ThrowHelper.EnsureOffsetLength(start, length, text.Length);
            _object = text;
            _byteStart = TypeHelper.ArrayOffsetInt + start * Unsafe.SizeOf<char>();
            _charLength = length;
        }

        public bool IsEmpty => Length == 0;

        public bool IsEmptyOrWhiteSpace
        {
            get
            {
                if (_charLength == 0)
                    return true;
                ref readonly char first = ref First;
                for (var i = 0; i < _charLength; ++i)
                {
                    if (!char.IsWhiteSpace(Unsafe.Add(ref Unsafe.AsRef(in first), i)))
                        return false;
                }

                return true;
            }
        }

        // public bool StartsWith(string value) => Text.StartsWith(value); // TODO uncomment, + overload that accepts StringSegment

        public StringSegment TrimNewLineAtEnd()
        {
            if (_charLength == 0) return Empty;

            ref readonly char first = ref First;

            int pos;
            for (pos = Length - 1; pos >= 0; pos--)
            {
                var c = Unsafe.Add(ref Unsafe.AsRef(in first), pos);
                if (!(c == '\n' || c == '\r'))
                    break;
            }

            return new StringSegment(_object, _byteStart, pos + 1);
        }

        public StringSegment TrimWhiteSpaceAtEnd()
        {
            if (_charLength == 0) return Empty;

            ref readonly char first = ref First;

            int pos;
            for (pos = Length - 1; pos >= 0; pos--)
            {
                if (!char.IsWhiteSpace(Unsafe.Add(ref Unsafe.AsRef(in first), pos)))
                    break;
            }

            return new StringSegment(_object, _byteStart, pos + 1);
        }

        public int Start
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (_byteStart - (_object is string ? TypeHelper.StringOffsetInt : TypeHelper.ArrayOffsetInt)) / Unsafe.SizeOf<char>(); }
        }

        public int Length => _charLength;
        public int End => Start + Length;

        internal string? String => _object as string;
        internal char[]? Array => _object as char[];

        public ref readonly char First
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (_charLength == 0)
                    return ref Unsafe.NullRef<char>();
                return ref Unsafe.AddByteOffset(ref Unsafe.As<Box<char>>(_object)!.Value, (nint)_byteStart);
            }
        }

        public ref readonly char this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.AddByteOffset(ref Unsafe.As<Box<char>>(_object)!.Value, (nint)(_byteStart + index * sizeof(char)));
        }

        public ReadOnlySpan<char> Span
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
#if BUILTIN_SPAN
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in First), _charLength);
#else
                if (_object is string s)
                {
                    int start = (_byteStart - TypeHelper.StringOffsetInt) / Unsafe.SizeOf<char>(); // TODO (low) just in case, ensure this is optimized to `>> 1`
                    return s.AsSpan(start, Length);
                }
                else
                {
                    int start = (_byteStart - TypeHelper.ArrayOffsetInt) / Unsafe.SizeOf<char>();
                    return Unsafe.As<object, char[]>(ref Unsafe.AsRef(in _object)).AsSpan(start, Length);
                }
#endif
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment Slice(int start)
        {
            var length = _charLength - start;
            ThrowHelper.EnsureOffsetLength(start, length, _charLength);
            start = _byteStart + start * Unsafe.SizeOf<char>();
            return new StringSegment(_object, start, length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public StringSegment Slice(int start, int length)
        {
            ThrowHelper.EnsureOffsetLength(start, length, _charLength);
            start = _byteStart + start * Unsafe.SizeOf<char>();
            return new StringSegment(_object, start, length);
        }

        /// <summary>
        /// True if the segment is backed by a string of the exact length.
        /// </summary>
        public bool IsTrimmed
        {
            get
            {
                if (_object is string s)
                    return s.Length == Length;
                return false;
            }
        }

        /// <summary>
        /// Create a new <see cref="StringSegment"/> backed by a string of the exact length (a <see cref="string"/> return value from <see cref="ToString"/>).
        /// This new segment could be used as a key in a long-lived dictionary
        /// without consuming excess memory and holding a GC reference to the
        /// underlying bigger string or mutable char[].
        /// When using with <see cref="HashSetSlim{T}"/>, a <see cref="StringSegment"/> could be interned
        /// and its <see cref="int"/> index in the <see cref="HashSetSlim{T}"/> could be used as a small reference to the segment.
        /// </summary>
        public StringSegment Trim()
        {
            return new(ToString());
        }

        public override string ToString()
        {
            if (Length == 0)
                return string.Empty;
            if (_object is string s && Length == s.Length)
                return s;
            return Span.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(StringSegment other)
        {
            int segmentLength = Length;

            if (segmentLength != other.Length)
                return false;

            if (segmentLength == 0)
                return true;

            ref char first = ref Unsafe.AsRef(in First);
            ref char otherFirst = ref Unsafe.AsRef(in other.First);

            for (var i = 0; i < segmentLength; i++)
            {
                if (Unsafe.Add(ref first, i) != Unsafe.Add(ref otherFirst, i))
                    return false;
            }

            return true;
        }

        public bool ReferenceEquals(StringSegment other)
        {
            return ReferenceEquals(_object, other._object)
                   && _byteStart == other._byteStart
                   && Length == other.Length;
        }

        public override bool Equals(object? obj)
        {
            return obj is StringSegment other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return UnsafeGetHashCode(in First, _charLength);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UnsafeGetHashCode(in char firstChar, int length)
        {
            // Includes both null and empty
            if (length == 0) return 0;

            unchecked
            {
                // Lazy FNV, just read 2 chars as int
                // Would be nice to put xxh3 here, it's optimized for short length.

                ref char first = ref Unsafe.AsRef(in firstChar);

                var hashCode = (int)2166136261;

                var i = 0;

                for (; i < length - 2; i += 2)
                {
                    hashCode = (hashCode ^ Unsafe.As<char, int>(ref Unsafe.Add(ref first, i))) * 16777619;
                }

                for (; i < length; i++)
                {
                    hashCode = (hashCode ^ Unsafe.Add(ref first, i)) * 16777619;
                }

                return hashCode;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(string? text)
        {
            if (text == null) return 0;
            return UnsafeGetHashCode(in text.AsSpan().GetPinnableReference(), text.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(string? text, int start, int length)
        {
            if (text == null) return 0;
            return UnsafeGetHashCode(in text.AsSpan(start, length).GetPinnableReference(), text.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(char[]? text)
        {
            if (text == null) return 0;
            return UnsafeGetHashCode(in text.AsSpan().GetPinnableReference(), text.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetHashCode(char[]? text, int start, int length)
        {
            if (text == null) return 0;
            return UnsafeGetHashCode(in text.AsSpan(start, length).GetPinnableReference(), text.Length);
        }

        public int CompareTo(StringSegment other)
        {
            return MemoryExtensions.SequenceCompareTo(Span, other.Span);
        }

        public static implicit operator StringSegment(string text)
        {
            return new(text);
        }

        public static implicit operator StringSegment(char[] text)
        {
            return new(text);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(StringSegment left, StringSegment right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(StringSegment left, StringSegment right)
        {
            return !left.Equals(right);
        }
    }
}
