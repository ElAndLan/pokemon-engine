using Silk.NET.OpenGL;

namespace Cgm.Runtime.Engine;

/// <summary>The OpenGL 3.3 core implementation of <see cref="IRenderer"/>: one dynamic quad batch,
/// one textured-quad shader pair, premultiplied-alpha blending, nearest-neighbour clamped textures.
/// No lighting, post-processing, rotation, dynamic atlasing, or second backend.</summary>
public sealed class GlRenderer : IRenderer
{
    private const int VerticesPerQuad = 4;
    private const int IndicesPerQuad = 6;
    private const int FloatsPerVertex = 8; // x, y, u, v, r, g, b, a

    private const string VertexShader = """
        #version 330 core
        layout (location = 0) in vec2 aPos;
        layout (location = 1) in vec2 aUv;
        layout (location = 2) in vec4 aColor;
        uniform vec2 uVirtualSize;
        out vec2 vUv;
        out vec4 vColor;
        void main()
        {
            // Virtual pixels (top-left origin, +Y down) to clip space.
            vec2 ndc = vec2(aPos.x / uVirtualSize.x * 2.0 - 1.0, 1.0 - aPos.y / uVirtualSize.y * 2.0);
            gl_Position = vec4(ndc, 0.0, 1.0);
            vUv = aUv;
            vColor = aColor;
        }
        """;

    private const string FragmentShader = """
        #version 330 core
        in vec2 vUv;
        in vec4 vColor;
        uniform sampler2D uTexture;
        out vec4 fragColor;
        void main()
        {
            fragColor = texture(uTexture, vUv) * vColor;
        }
        """;

    private readonly GL _gl;
    private readonly Dictionary<int, (uint Handle, TextureInfo Info)> _textures = [];
    private uint _program;
    private uint _vao;
    private uint _vbo;
    private uint _ebo;
    private int _quadCapacity;
    private int _nextTextureId = 1;
    private float[] _vertices = [];
    private bool _disposed;

    public GlRenderer(GL gl, int quadCapacity = QuadBatch.InitialCapacity)
    {
        ArgumentNullException.ThrowIfNull(gl);
        _gl = gl;
        _program = CreateProgram();
        (_vao, _vbo, _ebo) = CreateBuffers(quadCapacity);
        _quadCapacity = quadCapacity;
        _vertices = new float[quadCapacity * VerticesPerQuad * FloatsPerVertex];

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha); // premultiplied alpha
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);
    }

    public TextureHandle CreateTexture(int width, int height, ReadOnlySpan<byte> rgba)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TextureInfo info = TextureInfo.Validate(width, height, rgba.Length);

        uint handle = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        unsafe
        {
            fixed (byte* pixels = rgba)
                _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)width, (uint)height, 0,
                    PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        }
        // Nearest-neighbour, clamp to edge, no mipmaps: pixel art must not blur or bleed.
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        _gl.BindTexture(TextureTarget.Texture2D, 0);

        var lease = new TextureHandle(_nextTextureId++);
        _textures[lease.Id] = (handle, info);
        return lease;
    }

    public void DisposeTexture(TextureHandle texture)
    {
        if (!_textures.Remove(texture.Id, out (uint Handle, TextureInfo Info) entry))
            return; // idempotent: releasing an already-released lease is harmless
        _gl.DeleteTexture(entry.Handle);
    }

    public TextureInfo Describe(TextureHandle texture) =>
        _textures.TryGetValue(texture.Id, out (uint Handle, TextureInfo Info) entry)
            ? entry.Info
            : throw new ArgumentException($"Texture {texture.Id} is not a live lease.", nameof(texture));

    public void BeginFrame(Viewport viewport, int virtualWidth, int virtualHeight, Rgba clear)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (virtualWidth <= 0 || virtualHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(virtualWidth), "Virtual size must be positive.");

        // Letterbox bars are opaque black and are painted outside the scaled viewport.
        _gl.Disable(EnableCap.ScissorTest);
        _gl.ClearColor(0f, 0f, 0f, 1f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);

        _gl.Viewport(viewport.OffsetX, viewport.OffsetY, (uint)viewport.Width, (uint)viewport.Height);
        _gl.Enable(EnableCap.ScissorTest);
        _gl.Scissor(viewport.OffsetX, viewport.OffsetY, (uint)viewport.Width, (uint)viewport.Height);
        _gl.ClearColor(clear.R / 255f, clear.G / 255f, clear.B / 255f, clear.A / 255f);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _gl.Disable(EnableCap.ScissorTest);

        _gl.UseProgram(_program);
        _gl.Uniform2(_gl.GetUniformLocation(_program, "uVirtualSize"), (float)virtualWidth, (float)virtualHeight);
        _gl.Uniform1(_gl.GetUniformLocation(_program, "uTexture"), 0);
        ViewportRect = viewport;
        VirtualSize = (virtualWidth, virtualHeight);
    }

    public void Draw(IReadOnlyList<Quad> quads, IReadOnlyList<DrawCall> calls)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(quads);
        ArgumentNullException.ThrowIfNull(calls);
        if (quads.Count == 0)
            return;

        EnsureCapacity(quads.Count);
        for (int i = 0; i < quads.Count; i++)
        {
            Quad quad = quads[i];
            if (!_textures.TryGetValue(quad.Texture.Id, out (uint Handle, TextureInfo Info) entry))
                throw new ArgumentException($"Quad references dead texture {quad.Texture.Id}.", nameof(quads));
            // Programmer-error guard. Authored content is rejected at asset load, where the
            // rectangle and its atlas are both known; nothing here can repair a bad rectangle.
            if (!entry.Info.Contains(quad.Source))
                throw new ArgumentException(
                    $"Source {quad.Source} is outside texture {quad.Texture.Id} " +
                    $"({entry.Info.Width}x{entry.Info.Height}).", nameof(quads));
            WriteQuad(_vertices, i * VerticesPerQuad * FloatsPerVertex, quad, entry.Info);
        }

        _gl.BindVertexArray(_vao);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vbo);
        unsafe
        {
            fixed (float* data = _vertices)
                _gl.BufferSubData(BufferTargetARB.ArrayBuffer, 0,
                    (nuint)(quads.Count * VerticesPerQuad * FloatsPerVertex * sizeof(float)), data);
        }

        foreach (DrawCall call in calls)
        {
            if (!_textures.TryGetValue(call.Texture.Id, out (uint Handle, TextureInfo Info) entry))
                throw new ArgumentException($"Draw call references dead texture {call.Texture.Id}.", nameof(calls));

            ApplyScissor(call.Scissor);
            _gl.ActiveTexture(TextureUnit.Texture0);
            _gl.BindTexture(TextureTarget.Texture2D, entry.Handle);
            unsafe
            {
                _gl.DrawElements(PrimitiveType.Triangles, (uint)(call.Count * IndicesPerQuad),
                    DrawElementsType.UnsignedInt, (void*)(call.Start * IndicesPerQuad * sizeof(uint)));
            }
        }

        _gl.Disable(EnableCap.ScissorTest);
        _gl.BindVertexArray(0);
    }

    public void EndFrame()
    {
        _gl.Disable(EnableCap.ScissorTest);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.UseProgram(0);
    }

    /// <summary>Live texture leases, for the frame diagnostics and leak checks.</summary>
    public int TextureCount => _textures.Count;

    public int QuadCapacity => _quadCapacity;

    public Viewport ViewportRect { get; private set; }

    public (int Width, int Height) VirtualSize { get; private set; }

    /// <summary>Disposes leases, then buffers, then the program: children before shared state,
    /// reverse of creation. Repeating disposal is harmless.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        foreach ((uint handle, TextureInfo _) in _textures.Values)
            _gl.DeleteTexture(handle);
        _textures.Clear();

        if (_ebo != 0) _gl.DeleteBuffer(_ebo);
        if (_vbo != 0) _gl.DeleteBuffer(_vbo);
        if (_vao != 0) _gl.DeleteVertexArray(_vao);
        if (_program != 0) _gl.DeleteProgram(_program);
        _ebo = _vbo = _vao = _program = 0;
    }

    private void ApplyScissor(RectI? scissor)
    {
        if (scissor is not { } rect)
        {
            _gl.Disable(EnableCap.ScissorTest);
            return;
        }
        // Scissor is in window pixels with a bottom-left origin; virtual coordinates are top-left.
        int scale = Math.Max(1, ViewportRect.Scale);
        int x = ViewportRect.OffsetX + rect.X * scale;
        int y = ViewportRect.OffsetY + (VirtualSize.Height - rect.Bottom) * scale;
        _gl.Enable(EnableCap.ScissorTest);
        _gl.Scissor(x, y, (uint)Math.Max(0, rect.Width * scale), (uint)Math.Max(0, rect.Height * scale));
    }

    private void EnsureCapacity(int quads)
    {
        if (quads <= _quadCapacity)
            return;
        int grown = _quadCapacity;
        while (grown < quads)
            grown <<= 1;

        _gl.DeleteBuffer(_ebo);
        _gl.DeleteBuffer(_vbo);
        _gl.DeleteVertexArray(_vao);
        (_vao, _vbo, _ebo) = CreateBuffers(grown);
        _quadCapacity = grown;
        _vertices = new float[grown * VerticesPerQuad * FloatsPerVertex];
    }

    private static void WriteQuad(float[] buffer, int offset, Quad quad, TextureInfo texture)
    {
        // Source rectangles are integer atlas pixels; the sampler needs normalised UVs.
        float u0 = (float)quad.Source.X / texture.Width;
        float v0 = (float)quad.Source.Y / texture.Height;
        float u1 = (float)quad.Source.Right / texture.Width;
        float v1 = (float)quad.Source.Bottom / texture.Height;
        if ((quad.Flip & Flip.Horizontal) != 0)
            (u0, u1) = (u1, u0);
        if ((quad.Flip & Flip.Vertical) != 0)
            (v0, v1) = (v1, v0);

        float r = quad.Tint.R / 255f, g = quad.Tint.G / 255f, b = quad.Tint.B / 255f, a = quad.Tint.A / 255f;
        Span<(float X, float Y, float U, float V)> corners =
        [
            (quad.Dest.X, quad.Dest.Y, u0, v0),
            (quad.Dest.Right, quad.Dest.Y, u1, v0),
            (quad.Dest.Right, quad.Dest.Bottom, u1, v1),
            (quad.Dest.X, quad.Dest.Bottom, u0, v1),
        ];
        for (int i = 0; i < corners.Length; i++)
        {
            int at = offset + i * FloatsPerVertex;
            buffer[at] = corners[i].X;
            buffer[at + 1] = corners[i].Y;
            buffer[at + 2] = corners[i].U;
            buffer[at + 3] = corners[i].V;
            buffer[at + 4] = r;
            buffer[at + 5] = g;
            buffer[at + 6] = b;
            buffer[at + 7] = a;
        }
    }

    private (uint Vao, uint Vbo, uint Ebo) CreateBuffers(int quadCapacity)
    {
        uint vao = _gl.GenVertexArray();
        _gl.BindVertexArray(vao);

        uint vbo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
        unsafe
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer,
                (nuint)(quadCapacity * VerticesPerQuad * FloatsPerVertex * sizeof(float)),
                null, BufferUsageARB.DynamicDraw);
        }

        // Indices are fixed for the whole buffer: two triangles per quad, uploaded once.
        var indices = new uint[quadCapacity * IndicesPerQuad];
        for (int q = 0; q < quadCapacity; q++)
        {
            uint v = (uint)(q * VerticesPerQuad);
            int i = q * IndicesPerQuad;
            indices[i] = v; indices[i + 1] = v + 1; indices[i + 2] = v + 2;
            indices[i + 3] = v; indices[i + 4] = v + 2; indices[i + 5] = v + 3;
        }
        uint ebo = _gl.GenBuffer();
        _gl.BindBuffer(BufferTargetARB.ElementArrayBuffer, ebo);
        unsafe
        {
            fixed (uint* data = indices)
                _gl.BufferData(BufferTargetARB.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)),
                    data, BufferUsageARB.StaticDraw);
        }

        unsafe
        {
            uint stride = FloatsPerVertex * sizeof(float);
            _gl.EnableVertexAttribArray(0);
            _gl.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, (void*)0);
            _gl.EnableVertexAttribArray(1);
            _gl.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, (void*)(2 * sizeof(float)));
            _gl.EnableVertexAttribArray(2);
            _gl.VertexAttribPointer(2, 4, VertexAttribPointerType.Float, false, stride, (void*)(4 * sizeof(float)));
        }
        _gl.BindVertexArray(0);
        return (vao, vbo, ebo);
    }

    private uint CreateProgram()
    {
        uint vertex = Compile(ShaderType.VertexShader, VertexShader);
        uint fragment = Compile(ShaderType.FragmentShader, FragmentShader);
        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertex);
        _gl.AttachShader(program, fragment);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
            throw new InvalidOperationException($"Shader link failed: {_gl.GetProgramInfoLog(program)}");
        _gl.DetachShader(program, vertex);
        _gl.DetachShader(program, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
        return program;
    }

    private uint Compile(ShaderType type, string source)
    {
        uint shader = _gl.CreateShader(type);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compiled);
        if (compiled == 0)
            throw new InvalidOperationException($"{type} compile failed: {_gl.GetShaderInfoLog(shader)}");
        return shader;
    }
}
