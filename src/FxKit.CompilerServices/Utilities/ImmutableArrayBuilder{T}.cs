// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Ported from ComputeSharp: https://github.com/Sergio0694/ComputeSharp/tree/main/src/ComputeSharp.SourceGeneration/Helpers
// Modifications mainly around style and naming.

using System.Collections;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace FxKit.CompilerServices.Utilities;

/// <summary>
/// A helper type to build sequences of values with pooled buffers.
/// </summary>
/// <typeparam name="T">The type of items to create sequences for.</typeparam>
internal struct ImmutableArrayBuilder<T> : IDisposable
{
    /// <summary>
    /// The shared <see cref="ObjectPool{T}"/> instance to share <see cref="Writer"/> objects.
    /// </summary>
    private static readonly ObjectPool<Writer> sharedObjectPool = new(static () => []);

    /// <summary>
    /// The rented <see cref="Writer"/> instance to use.
    /// </summary>
    private Writer? _writer;

    /// <summary>
    /// Creates a new <see cref="ImmutableArrayBuilder{T}"/> object.
    /// </summary>
    public ImmutableArrayBuilder()
    {
        this._writer = sharedObjectPool.Allocate();
    }

    /// <summary>
    /// Gets the data written to the underlying buffer so far, as a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    public readonly ReadOnlySpan<T> WrittenSpan
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._writer!.WrittenSpan;
    }

    /// <summary>
    /// Gets the number of elements currently written in the current instance.
    /// </summary>
    public readonly int Count
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._writer!.Count;
    }

    /// <summary>
    /// Advances the current writer and gets a <see cref="Span{T}"/> to the requested memory area.
    /// </summary>
    /// <param name="requestedSize">The requested size to advance by.</param>
    /// <returns>A <see cref="Span{T}"/> to the requested memory area.</returns>
    /// <remarks>
    /// No other data should be written to the builder while the returned <see cref="Span{T}"/>
    /// is in use, as it could invalidate the memory area wrapped by it, if resizing occurs.
    /// </remarks>
    public readonly Span<T> Advance(int requestedSize)
    {
        return this._writer!.Advance(requestedSize);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.Add(T)"/>
    public readonly void Add(T item)
    {
        this._writer!.Add(item);
    }

    /// <summary>
    /// Adds the specified items to the end of the array.
    /// </summary>
    /// <param name="items">The items to add at the end of the array.</param>
    public readonly void AddRange(ReadOnlySpan<T> items)
    {
        this._writer!.AddRange(items);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.Clear"/>
    public readonly void Clear()
    {
        this._writer!.Clear();
    }

    /// <summary>
    /// Inserts an item to the builder at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index at which <paramref name="item"/> should be inserted.</param>
    /// <param name="item">The object to insert into the current instance.</param>
    public readonly void Insert(int index, T item)
    {
        this._writer!.Insert(index, item);
    }

    /// <summary>
    /// Gets an <see cref="IEnumerable{T}"/> instance for the current builder.
    /// </summary>
    /// <returns>An <see cref="IEnumerable{T}"/> instance for the current builder.</returns>
    /// <remarks>
    /// The builder should not be mutated while an enumerator is in use.
    /// </remarks>
    public readonly IEnumerable<T> AsEnumerable()
    {
        return this._writer!;
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.ToImmutable"/>
    public readonly ImmutableArray<T> ToImmutable()
    {
        var array = this._writer!.WrittenSpan.ToArray();

        return Unsafe.As<T[], ImmutableArray<T>>(ref array);
    }

    /// <inheritdoc cref="ImmutableArray{T}.Builder.ToArray"/>
    public readonly T[] ToArray()
    {
        return this._writer!.WrittenSpan.ToArray();
    }

    /// <inheritdoc/>
    public override readonly string ToString()
    {
        return this._writer!.WrittenSpan.ToString();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        var writer = this._writer;

        this._writer = null;

        if (writer is not null)
        {
            writer.Clear();

            sharedObjectPool.Free(writer);
        }
    }

    /// <summary>
    /// A class handling the actual buffer writing.
    /// </summary>
    private sealed class Writer : IList<T>, IReadOnlyList<T>
    {
        // ReSharper disable once InvalidXmlDocComment
        /// <summary>
        /// The underlying <typeparamref name="T" />.
        /// </summary>
        private T[] _array;

        /// <summary>
        /// The starting offset within <see cref="_array"/>.
        /// </summary>
        private int _index;

        /// <summary>
        /// Creates a new <see cref="Writer"/> instance with the specified parameters.
        /// </summary>
        public Writer()
        {
            this._array = typeof(T) == typeof(char) ? new T[1024] : new T[8];

            this._index = 0;
        }

        /// <inheritdoc cref="ICollection{T}.Count" />
        public int Count => this._index;

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.WrittenSpan"/>
        public ReadOnlySpan<T> WrittenSpan
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(this._array, start: 0, this._index);
        }

        /// <inheritdoc/>
        bool ICollection<T>.IsReadOnly => true;

        /// <inheritdoc/>
        T IReadOnlyList<T>.this[int index] => WrittenSpan[index];

        /// <inheritdoc/>
        T IList<T>.this[int index]
        {
            get => WrittenSpan[index];
            set => throw new NotSupportedException();
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Advance"/>
        public Span<T> Advance(int requestedSize)
        {
            EnsureCapacity(requestedSize);

            var span = this._array.AsSpan(this._index, requestedSize);

            this._index += requestedSize;

            return span;
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Add"/>
        public void Add(T value)
        {
            EnsureCapacity(1);

            this._array[this._index++] = value;
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.AddRange"/>
        public void AddRange(ReadOnlySpan<T> items)
        {
            EnsureCapacity(items.Length);

            items.CopyTo(this._array.AsSpan(this._index));

            this._index += items.Length;
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Clear"/>
        // ReSharper disable once UnusedMember.Local
        // ReSharper disable once UnusedParameter.Local
        public void Clear(ReadOnlySpan<T> items)
        {
            this._index = 0;
        }

        /// <inheritdoc cref="ImmutableArrayBuilder{T}.Insert"/>
        public void Insert(int index, T item)
        {
            if (index < 0 || index > this._index)
            {
                ImmutableArrayBuilder.ThrowArgumentOutOfRangeExceptionForIndex();
            }

            EnsureCapacity(1);

            if (index < this._index)
            {
                Array.Copy(this._array, index, this._array, index + 1, this._index - index);
            }

            this._array[index] = item;
            this._index++;
        }

        /// <summary>
        /// Clears the items in the current writer.
        /// </summary>
        public void Clear()
        {
            if (typeof(T) != typeof(byte) &&
                typeof(T) != typeof(char) &&
                typeof(T) != typeof(int))
            {
                this._array.AsSpan(start: 0, this._index).Clear();
            }

            this._index = 0;
        }

        /// <summary>
        /// Ensures that <see cref="_array"/> has enough free space to contain a given number of new items.
        /// </summary>
        /// <param name="requestedSize">The minimum number of items to ensure space for in <see cref="_array"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requestedSize)
        {
            if (requestedSize > this._array.Length - this._index)
            {
                ResizeBuffer(requestedSize);
            }
        }

        /// <summary>
        /// Resizes <see cref="_array"/> to ensure it can fit the specified number of new items.
        /// </summary>
        /// <param name="sizeHint">The minimum number of items to ensure space for in <see cref="_array"/>.</param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ResizeBuffer(int sizeHint)
        {
            var minimumSize = this._index + sizeHint;
            var requestedSize = Math.Max(this._array.Length * 2, minimumSize);

            var newArray = new T[requestedSize];

            Array.Copy(this._array, newArray, this._index);

            this._array = newArray;
        }

        /// <inheritdoc/>
        int IList<T>.IndexOf(T item)
        {
            return Array.IndexOf(this._array, item, startIndex: 0, this._index);
        }

        /// <inheritdoc/>
        void IList<T>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        bool ICollection<T>.Contains(T item)
        {
            return Array.IndexOf(this._array, item, startIndex: 0, this._index) >= 0;
        }

        /// <inheritdoc/>
        void ICollection<T>.CopyTo(T[] array, int arrayIndex)
        {
            Array.Copy(this._array, sourceIndex: 0, array, arrayIndex, this._index);
        }

        /// <inheritdoc/>
        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        /// <inheritdoc/>
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            T?[] array = this._array;
            var length = this._index;

            for (var i = 0; i < length; i++)
            {
                yield return array[i]!;
            }
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<T>)this).GetEnumerator();
        }
    }
}

/// <summary>
/// Private helpers for the <see cref="ImmutableArrayBuilder{T}"/> type.
/// </summary>
static file class ImmutableArrayBuilder
{
    /// <summary>
    /// Throws an <see cref="ArgumentOutOfRangeException"/> for <c>"index"</c>.
    /// </summary>
    public static void ThrowArgumentOutOfRangeExceptionForIndex()
    {
        // ReSharper disable once NotResolvedInText
        throw new ArgumentOutOfRangeException("index");
    }
}
