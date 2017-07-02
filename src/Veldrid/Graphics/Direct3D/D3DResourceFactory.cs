﻿using System;
using SharpDX.Direct3D11;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace Veldrid.Graphics.Direct3D
{
    public class D3DResourceFactory : ResourceFactory
    {
        private static readonly string s_shaderFileExtension = "hlsl";

        private readonly Device _device;
        private List<ShaderLoader> _shaderLoaders = new List<ShaderLoader>();

        public D3DResourceFactory(Device device)
        {
            _device = device;
            AddShaderLoader(new FolderShaderLoader(Path.Combine(AppContext.BaseDirectory, "HLSL")));
        }

        public override ConstantBuffer CreateConstantBuffer(int sizeInBytes)
        {
            return new D3DConstantBuffer(_device, sizeInBytes);
        }

        public override Framebuffer CreateFramebuffer()
        {
            return new D3DFramebuffer(_device);
        }

        public override Framebuffer CreateFramebuffer(int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);

            D3DTexture2D colorTexture = new D3DTexture2D(_device, new Texture2DDescription()
            {
                Format = SharpDX.DXGI.Format.R32G32B32A32_Float,
                ArraySize = 1,
                MipLevels = 1,
                Width = width,
                Height = height,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            D3DTexture2D depthTexture = new D3DTexture2D(_device, new Texture2DDescription()
            {
                Format = SharpDX.DXGI.Format.R16_Typeless,
                ArraySize = 1,
                MipLevels = 1,
                Width = width,
                Height = height,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });

            return new D3DFramebuffer(_device, colorTexture, depthTexture);
        }

        public override IndexBuffer CreateIndexBuffer(int sizeInBytes, bool isDynamic, IndexFormat format)
        {
            return new D3DIndexBuffer(_device, sizeInBytes, isDynamic, D3DFormats.VeldridToD3DIndexFormat(format));
        }

        public override Shader CreateShader(ShaderType type, string name)
        {
            using (Stream stream = GetShaderStream(name))
            using (StreamReader reader = new StreamReader(stream))
            {
                return CreateShader(type, reader.ReadToEnd(), name);
            }
        }

        public override Shader CreateShader(ShaderType type, string shaderCode, string name)
        {
            switch (type)
            {
                case ShaderType.Vertex:
                    return new D3DVertexShader(_device, shaderCode, name);
                case ShaderType.Geometry:
                    return new D3DGeometryShader(_device, shaderCode, name);
                case ShaderType.Fragment:
                    return new D3DFragmentShader(_device, shaderCode, name);
                default:
                    throw Illegal.Value<ShaderType>();
            }
        }

        public override VertexInputLayout CreateInputLayout(VertexInputDescription[] vertexInputs)
        {
            return new D3DVertexInputLayout(_device, vertexInputs);
        }

        public override ShaderSet CreateShaderSet(VertexInputLayout inputLayout, Shader vertexShader, Shader fragmentShader)
        {
            return new D3DShaderSet(inputLayout, vertexShader, null, fragmentShader);
        }

        public override ShaderSet CreateShaderSet(VertexInputLayout inputLayout, Shader vertexShader, Shader geometryShader, Shader fragmentShader)
        {
            return new D3DShaderSet(inputLayout, vertexShader, geometryShader, fragmentShader);
        }

        public override ShaderConstantBindingSlots CreateShaderConstantBindingSlots(
            ShaderSet shaderSet,
            ShaderConstantDescription[] constants)
        {
            return new D3DShaderConstantBindingSlots(_device, shaderSet, constants);
        }

        public override ShaderTextureBindingSlots CreateShaderTextureBindingSlots(ShaderSet shaderSet, ShaderTextureInput[] textureInputs)
        {
            return new D3DShaderTextureBindingSlots((D3DShaderSet)shaderSet, textureInputs);
        }

        public override VertexBuffer CreateVertexBuffer(int sizeInBytes, bool isDynamic)
        {
            return new D3DVertexBuffer(_device, sizeInBytes, isDynamic);
        }

        public override DeviceTexture2D CreateTexture(int mipLevels, int width, int height, int pixelSizeInBytes, PixelFormat format)
        {
            D3DTexture2D texture = new D3DTexture2D(
                _device,
                BindFlags.ShaderResource,
                ResourceUsage.Default,
                CpuAccessFlags.None,
                D3DFormats.VeldridToD3DPixelFormat(format),
                mipLevels,
                width,
                height,
                width * pixelSizeInBytes);
            return texture;
        }

        protected override SamplerState CreateSamplerStateCore(
            SamplerAddressMode addressU, 
            SamplerAddressMode addressV, 
            SamplerAddressMode addressW, 
            SamplerFilter filter, 
            int maxAnisotropy, 
            RgbaFloat borderColor, 
            DepthComparison comparison, 
            int minimumLod, 
            int maximumLod, 
            int lodBias)
        {
            return new D3DSamplerState(_device, addressU, addressV, addressW, filter, maxAnisotropy, borderColor, comparison, minimumLod, maximumLod, lodBias);
        }

        public override DeviceTexture2D CreateDepthTexture(int width, int height, int pixelSizeInBytes, PixelFormat format)
        {
            if (format != PixelFormat.R16_UInt)
            {
                throw new NotImplementedException("R16_UInt is the only supported depth texture format.");
            }

            return new D3DTexture2D(_device, new Texture2DDescription()
            {
                Format = SharpDX.DXGI.Format.R16_Typeless,
                ArraySize = 1,
                MipLevels = 1,
                Width = width,
                Height = height,
                SampleDescription = new SharpDX.DXGI.SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil | BindFlags.ShaderResource,
                CpuAccessFlags = CpuAccessFlags.None,
                OptionFlags = ResourceOptionFlags.None
            });
        }

        public override CubemapTexture CreateCubemapTexture(
            IntPtr pixelsFront,
            IntPtr pixelsBack,
            IntPtr pixelsLeft,
            IntPtr pixelsRight,
            IntPtr pixelsTop,
            IntPtr pixelsBottom,
            int width,
            int height,
            int pixelSizeinBytes,
            PixelFormat format)
        {
            return new D3DCubemapTexture(_device, pixelsFront, pixelsBack, pixelsLeft, pixelsRight, pixelsTop, pixelsBottom, width, height, pixelSizeinBytes, format);
        }

        public override ShaderTextureBinding CreateShaderTextureBinding(DeviceTexture texture)
        {
            D3DTexture d3dTexture = (D3DTexture)texture;
            ShaderResourceViewDescription srvd = d3dTexture.GetShaderResourceViewDescription();
            ShaderResourceView srv = new ShaderResourceView(_device, d3dTexture.DeviceTexture, srvd);
            return new D3DTextureBinding(srv, d3dTexture);
        }

        protected override BlendState CreateCustomBlendStateCore(
            bool isBlendEnabled,
            Blend srcAlpha, Blend destAlpha, BlendFunction alphaBlendFunc,
            Blend srcColor, Blend destColor, BlendFunction colorBlendFunc,
            RgbaFloat blendFactor)
        {
            return new D3DBlendState(_device, isBlendEnabled, srcAlpha, destAlpha, alphaBlendFunc, srcColor, destColor, colorBlendFunc, blendFactor);
        }

        protected override DepthStencilState CreateDepthStencilStateCore(bool isDepthEnabled, DepthComparison comparison, bool isDepthWriteEnabled)
        {
            return new D3DDepthStencilState(_device, isDepthEnabled, comparison, isDepthWriteEnabled);
        }

        protected override RasterizerState CreateRasterizerStateCore(
            FaceCullingMode cullMode,
            TriangleFillMode fillMode,
            bool isDepthClipEnabled,
            bool isScissorTestEnabled)
        {
            return new D3DRasterizerState(_device, cullMode, fillMode, isDepthClipEnabled, isScissorTestEnabled);
        }

        public override void AddShaderLoader(ShaderLoader loader)
        {
            _shaderLoaders.Add(loader);
        }

        private Stream GetShaderStream(string name)
        {
            foreach (var loader in _shaderLoaders)
            {
                Stream s;
                if (loader.TryOpenShader(name, s_shaderFileExtension, out s))
                {
                    return s;
                }
            }

            throw new InvalidOperationException("No registered loader was able to find shader: " + name);
        }
    }
}
