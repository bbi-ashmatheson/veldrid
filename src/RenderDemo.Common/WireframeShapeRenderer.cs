﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Veldrid.Assets;
using Veldrid.Graphics;

namespace Veldrid.RenderDemo
{
    public abstract class WireframeShapeRenderer : SwappableRenderItem, IDisposable
    {
        private readonly RawTextureDataArray<RgbaFloat> _textureData;
        private readonly MaterialAsset _materialAsset;

        private VertexBuffer _vb;
        private IndexBuffer _ib;
        private Material _material;
        private DeviceTexture _texture;
        private ShaderTextureBinding _textureBinding;

        private static string[] s_stages = new string[] { "Standard" };
        private RasterizerState _wireframeState;

        protected List<VertexPositionNormalTexture> _vertices = new List<VertexPositionNormalTexture>();
        protected List<ushort> _indices = new List<ushort>();
        protected List<ushort> _wireframePolyfillIndices = new List<ushort>();
        private DynamicDataProvider<Matrix4x4> _worldProvider;
        private DependantDataProvider<Matrix4x4> _inverseTransposeWorldProvider;
        private ConstantBufferDataProvider[] _perObjectProviders;

        public WireframeShapeRenderer(AssetDatabase ad, RenderContext rc, RgbaFloat color)
        {
            _materialAsset = ad.LoadAsset<MaterialAsset>("MaterialAsset/ShadowCaster_MtlTemplate.json");
            _textureData = new RawTextureDataArray<RgbaFloat>(new RgbaFloat[] { color }, 1, 1, RgbaFloat.SizeInBytes, PixelFormat.R32_G32_B32_A32_Float);

            _worldProvider = new DynamicDataProvider<Matrix4x4>(Matrix4x4.Identity);
            _inverseTransposeWorldProvider = new DependantDataProvider<Matrix4x4>(_worldProvider, Utilities.CalculateInverseTranspose);
            var materialProvider = new DynamicDataProvider<ForwardRendering.MtlMaterialProperties>(new ForwardRendering.MtlMaterialProperties(new Vector3(4, 5, 6), 7));
            _perObjectProviders = new ConstantBufferDataProvider[] { _worldProvider, _inverseTransposeWorldProvider, materialProvider };

            InitializeContextObjects(ad, rc);
        }

        public void ChangeRenderContext(AssetDatabase ad, RenderContext rc)
        {
            Dispose();
            InitializeContextObjects(ad, rc);
        }

        private void InitializeContextObjects(AssetDatabase ad, RenderContext rc)
        {
            ResourceFactory factory = rc.ResourceFactory;
            _vb = factory.CreateVertexBuffer(1024, true);
            _ib = factory.CreateIndexBuffer(1024, true);
            _material = _materialAsset.Create(ad, rc);
            _texture = _textureData.CreateDeviceTexture(factory);
            _textureBinding = factory.CreateShaderTextureBinding(_texture);
            _wireframeState = factory.CreateRasterizerState(FaceCullingMode.None, TriangleFillMode.Solid, true, true);
        }

        public abstract bool Cull(ref BoundingFrustum visibleFrustum);

        public void Dispose()
        {
            _vb?.Dispose();
            _ib?.Dispose();
            _material?.Dispose();
            _texture?.Dispose();
            _textureBinding?.Dispose();
            _wireframeState?.Dispose();
        }

        public RenderOrderKey GetRenderOrderKey(Vector3 viewPosition)
        {
            return RenderOrderKey.Create(_material.GetHashCode());
        }

        public IList<string> GetStagesParticipated()
        {
            return s_stages;
        }

        public void Render(RenderContext rc, string pipelineStage)
        {
            UpdateBuffers(rc);

            var rasterState = rc.RasterizerState;
            rc.VertexBuffer = _vb;
            rc.IndexBuffer = _ib;
            rc.Material = _material;
            _material.ApplyPerObjectInputs(_perObjectProviders);
            _material.UseTexture(0, _textureBinding);
            rc.RasterizerState = _wireframeState;
            rc.DrawIndexedPrimitives(_indices.Count, 0, PrimitiveTopology.LineList);
            rc.RasterizerState = rasterState;
        }

        private void UpdateBuffers(RenderContext rc)
        {
            var factory = rc.ResourceFactory;
            _vertices.Clear();
            _indices.Clear();
            AddVerticesAndIndices();

            _vb.Dispose();
            _ib.Dispose();

            _vb = factory.CreateVertexBuffer(_vertices.Count * VertexPositionNormalTexture.SizeInBytes, false);
            _vb.SetVertexData(
                _vertices.ToArray(),
                new VertexDescriptor(
                    VertexPositionNormalTexture.SizeInBytes,
                    VertexPositionNormalTexture.ElementCount,
                    0,
                    IntPtr.Zero));
            _ib = factory.CreateIndexBuffer(sizeof(ushort) * _indices.Count, false);
            _ib.SetIndices(_indices.ToArray(), IndexFormat.UInt16);
        }

        protected abstract void AddVerticesAndIndices();
    }

    public class OctreeRenderer<T> : WireframeShapeRenderer
    {
        private OctreeNode<T> _octree;
        public OctreeNode<T> Octree
        {
            get { return _octree; }
            set { _octree = value; }
        }

        public OctreeRenderer(OctreeNode<T> octree, AssetDatabase ad, RenderContext rc) : base(ad, rc, RgbaFloat.Red)
        {
            _octree = octree;
        }

        public override bool Cull(ref BoundingFrustum visibleFrustum)
        {
            return visibleFrustum.Contains(_octree.Bounds) == ContainmentType.Disjoint;
        }

        protected override void AddVerticesAndIndices()
        {
            AddVerticesAndIndices(_octree, _vertices, _indices);
        }

        private void AddVerticesAndIndices(OctreeNode<T> octree, List<VertexPositionNormalTexture> vertices, List<ushort> indices)
        {
            // TODO: This is literally the exact same thing as the bounding box renderer, except recursive.
            ushort baseIndex = checked((ushort)vertices.Count);
            var bounds = octree.Bounds;

            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Min.Z), Vector3.Zero, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Min.Z), Vector3.Zero, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Min.Z), Vector3.Zero, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Min.Z), Vector3.Zero, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Min.X, bounds.Min.Y, bounds.Max.Z), Vector3.Zero, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Min.X, bounds.Max.Y, bounds.Max.Z), Vector3.Zero, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Max.X, bounds.Max.Y, bounds.Max.Z), Vector3.Zero, Vector2.Zero));
            vertices.Add(new VertexPositionNormalTexture(new Vector3(bounds.Max.X, bounds.Min.Y, bounds.Max.Z), Vector3.Zero, Vector2.Zero));

            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 3));

            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 7));

            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 1));

            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 3));

            foreach (var child in octree.Children)
            {
                AddVerticesAndIndices(child, vertices, indices);
            }
        }
    }

    public class FrustumWireframeRenderer : WireframeShapeRenderer
    {
        private BoundingFrustum _frustum;

        public FrustumWireframeRenderer(BoundingFrustum frustum, AssetDatabase ad, RenderContext rc)
            : base(ad, rc, RgbaFloat.Cyan)
        {
            _frustum = frustum;
        }

        public BoundingFrustum Frustum
        {
            get { return _frustum; }
            set { _frustum = value; }
        }

        public override bool Cull(ref BoundingFrustum visibleFrustum)
        {
            return visibleFrustum.Contains(ref _frustum) == ContainmentType.Disjoint;
        }

        protected override void AddVerticesAndIndices()
        {
            ushort baseIndex = checked((ushort)_vertices.Count);
            FrustumCorners corners = _frustum.GetCorners();

            _vertices.Add(new VertexPositionNormalTexture(corners.NearTopLeft, Vector3.One * 0.5f, Vector2.One * 0.5f));
            _vertices.Add(new VertexPositionNormalTexture(corners.NearTopRight, Vector3.One * 0.5f, Vector2.One * 0.5f));
            _vertices.Add(new VertexPositionNormalTexture(corners.NearBottomRight, Vector3.One * 0.5f, Vector2.One * 0.5f));
            _vertices.Add(new VertexPositionNormalTexture(corners.NearBottomLeft, Vector3.One * 0.5f, Vector2.One * 0.5f));

            _vertices.Add(new VertexPositionNormalTexture(corners.FarTopLeft, Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(corners.FarTopRight, Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(corners.FarBottomRight, Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(corners.FarBottomLeft, Vector3.Zero, Vector2.Zero));

            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 3));

            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 7));

            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 6));

            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 2));
        }
    }

    public class BoundingBoxWireframeRenderer : WireframeShapeRenderer
    {
        private BoundingBox _box;

        public BoundingBoxWireframeRenderer(BoundingBox box, AssetDatabase ad, RenderContext rc)
            : base(ad, rc, RgbaFloat.Cyan)
        {
            _box = box;
        }

        public BoundingBox Box
        {
            get { return _box; }
            set { _box = value; }
        }

        public override bool Cull(ref BoundingFrustum visibleFrustum)
        {
            return visibleFrustum.Contains(_box) == ContainmentType.Disjoint;
        }

        protected override void AddVerticesAndIndices()
        {
            ushort baseIndex = checked((ushort)_vertices.Count);

            var min = _box.Min;
            var max = _box.Max;

            _vertices.Add(new VertexPositionNormalTexture(new Vector3(min.X, min.Y, min.Z), Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(new Vector3(min.X, max.Y, min.Z), Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(new Vector3(max.X, max.Y, min.Z), Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(new Vector3(max.X, min.Y, min.Z), Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(new Vector3(min.X, min.Y, max.Z), Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(new Vector3(min.X, max.Y, max.Z), Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(new Vector3(max.X, max.Y, max.Z), Vector3.Zero, Vector2.Zero));
            _vertices.Add(new VertexPositionNormalTexture(new Vector3(max.X, min.Y, max.Z), Vector3.Zero, Vector2.Zero));

            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 3));

            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 7));

            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 4));
            _indices.Add((ushort)(baseIndex + 0));
            _indices.Add((ushort)(baseIndex + 1));
            _indices.Add((ushort)(baseIndex + 5));
            _indices.Add((ushort)(baseIndex + 1));

            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 6));
            _indices.Add((ushort)(baseIndex + 2));
            _indices.Add((ushort)(baseIndex + 3));
            _indices.Add((ushort)(baseIndex + 7));
            _indices.Add((ushort)(baseIndex + 3));
        }
    }
}
