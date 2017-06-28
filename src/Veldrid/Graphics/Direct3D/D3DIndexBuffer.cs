﻿using System;
using SharpDX.Direct3D11;
using System.Runtime.CompilerServices;

namespace Veldrid.Graphics.Direct3D
{
    public class D3DIndexBuffer : D3DBuffer, IndexBuffer
    {
        private readonly Device _device;
        private SharpDX.DXGI.Format _format;
        private int _offset = 0;

        public D3DIndexBuffer(Device device, int sizeInBytes, bool isDynamic, SharpDX.DXGI.Format format = SharpDX.DXGI.Format.Unknown)
            : base(
                device,
                sizeInBytes,
                BindFlags.IndexBuffer,
                isDynamic ? ResourceUsage.Dynamic : ResourceUsage.Default,
                isDynamic ? CpuAccessFlags.Write : CpuAccessFlags.None)
        {
            _device = device;
            _format = format;
        }

        public void Apply()
        {
            if (_format == SharpDX.DXGI.Format.Unknown)
            {
                throw new InvalidOperationException("IndexBuffer format is Unknown.");
            }

            _device.ImmediateContext.InputAssembler.SetIndexBuffer(Buffer, _format, _offset);
        }

        public void SetIndices<T>(T[] indices, IndexFormat format) where T : struct
            => SetIndices(indices, format, 0, 0);

        public void SetIndices<T>(T[] indices, IndexFormat format, int stride, int elementOffset) where T : struct
        {
            _format = D3DFormats.VeldridToD3DIndexFormat(format);
            int elementSizeInBytes = Unsafe.SizeOf<T>();
            SetData(indices, elementSizeInBytes * indices.Length, elementOffset * elementSizeInBytes);
        }

        public void SetIndices(int[] indices) => SetIndices(indices, 0, 0);
        public void SetIndices(int[] indices, int stride, int elementOffset)
        {
            _format = SharpDX.DXGI.Format.R32_UInt;
            SetData(indices, indices.Length * sizeof(int), elementOffset * sizeof(int));
        }

        public void SetIndices(IntPtr indices, IndexFormat format, int elementSizeInBytes, int count)
            => SetIndices(indices, format, elementSizeInBytes, count, 0);
        public void SetIndices(IntPtr indices, IndexFormat format, int elementSizeInBytes, int count, int elementOffset)
        {
            SetData(indices, elementSizeInBytes * count, elementSizeInBytes * elementOffset);
            _format = D3DFormats.VeldridToD3DIndexFormat(format);
        }
    }
}