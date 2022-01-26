namespace Demo
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    using SharpDX;
    using SharpDX.D3DCompiler;
    using SharpDX.Mathematics.Interop;

    using D3D11 = SharpDX.Direct3D11;
    using DXGI = SharpDX.DXGI;

    public class PostEffect : IDisposable
    {
        private D3D11.Device device;
        private D3D11.DeviceContext deviceContext;

        private D3D11.Buffer trianglePositionVertexBuffer;
        private D3D11.Buffer triangleTexcoordVertexBuffer;

        [StructLayout( LayoutKind.Sequential )]
        private struct ConstantBuffer
        {
            public Vector4 parameters; // Tip: if not a Vector4, add padding as in Renderer.cs
        };
        private D3D11.Buffer constantBuffer;

        private D3D11.VertexShader vertexShader;
        private D3D11.PixelShader pixelShader;

        private D3D11.SamplerState samplerState;

        public float activateFixed = 0.0f;
        public float activateVariable = 0.0f;
        public float depth = 100.0f;

        Vector3[] vertexPositions = new Vector3[]
        {
            new Vector3( -1.0f, 1.0f, 0.0f ), new Vector3(  1.0f,  1.0f, 0.0f ), new Vector3( -1.0f, -1.0f, 0.0f ),
            new Vector3(  1.0f, 1.0f, 0.0f ), new Vector3(  1.0f, -1.0f, 0.0f ), new Vector3( -1.0f, -1.0f, 0.0f )
        };

        Vector2[] vertexTexcoords = new Vector2[]
        {
            new Vector2( 0.0f, 0.0f ), new Vector2( 1.0f, 0.0f ), new Vector2( 0.0f, 1.0f ),
            new Vector2( 1.0f, 0.0f ), new Vector2( 1.0f, 1.0f ), new Vector2( 0.0f, 1.0f )
        };

        private D3D11.InputElement[] inputElements = new D3D11.InputElement[]
        {
            new D3D11.InputElement( "POSITION", 0, DXGI.Format.R32G32B32_Float, 0 ),
            new D3D11.InputElement( "TEXCOORD", 0, DXGI.Format.R32G32_Float,    1 )
        };

        private D3D11.InputLayout inputLayout;

        public PostEffect( D3D11.Device device )
        {
            this.device = device;

            deviceContext = device.ImmediateContext;

            InitializeDeviceResources();
            InitializeShaders();
            InitializeTriangle();
        }

        private void InitializeDeviceResources()
        {
            D3D11.SamplerStateDescription description;
            description.Filter = D3D11.Filter.MinMagLinearMipPoint;
            description.AddressU = D3D11.TextureAddressMode.Clamp;
            description.AddressV = D3D11.TextureAddressMode.Clamp;
            description.AddressW = D3D11.TextureAddressMode.Clamp;
            description.MinimumLod = -float.MaxValue;
            description.MaximumLod =  float.MaxValue;
            description.MipLodBias = 0.0f;
            description.MaximumAnisotropy = 1;
            description.ComparisonFunction = D3D11.Comparison.Never;
            description.BorderColor = new RawColor4();

            samplerState = new D3D11.SamplerState( device, description );
        }

        private void InitializeShaders()
        {
            ConstantBuffer data = new ConstantBuffer();
            data.parameters = Vector4.Zero;
            constantBuffer = D3D11.Buffer.Create( device, D3D11.BindFlags.ConstantBuffer, ref data );

            string textVS = System.IO.File.ReadAllText( "Shaders\\PostEffectVS.hlsl" );
            using ( var vertexShaderByteCode = ShaderBytecode.Compile( textVS, "main", "vs_4_0", ShaderFlags.Debug | ShaderFlags.WarningsAreErrors, EffectFlags.None, null, new StreamInclude(), sourceFileName : "Shaders\\PostEffectVS.hlsl" ) )
            {
                if ( vertexShaderByteCode.Bytecode == null )
                {
                    MessageBox.Show( vertexShaderByteCode.Message, "Shader Compilation Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                    System.Environment.Exit( -1 );
                }

                vertexShader = new D3D11.VertexShader( device, vertexShaderByteCode );

                using( var inputSignature = ShaderSignature.GetInputSignature( vertexShaderByteCode ) )
                    inputLayout = new D3D11.InputLayout( device, inputSignature, inputElements );
            }

            string textPS = System.IO.File.ReadAllText( "Shaders\\PostEffectPS.hlsl" );
            using ( var pixelShaderByteCode = ShaderBytecode.Compile( textPS, "main", "ps_5_0", ShaderFlags.Debug | ShaderFlags.WarningsAreErrors, EffectFlags.None, null, new StreamInclude(), sourceFileName : "Shaders\\PostEffectPS.hlsl" ) )
            {
                if ( pixelShaderByteCode.Bytecode == null )
                {
                    MessageBox.Show( pixelShaderByteCode.Message, "Shader Compilation Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                    System.Environment.Exit( -1 );
                }

                pixelShader = new D3D11.PixelShader( device, pixelShaderByteCode );
            }
        }

        private void InitializeTriangle()
        {
            trianglePositionVertexBuffer = D3D11.Buffer.Create( device, D3D11.BindFlags.VertexBuffer, vertexPositions );
            triangleTexcoordVertexBuffer = D3D11.Buffer.Create( device, D3D11.BindFlags.VertexBuffer, vertexTexcoords );
        }

        public void Dispose()
        {
            samplerState.Dispose();

            constantBuffer.Dispose();
            vertexShader.Dispose();
            pixelShader.Dispose();

            trianglePositionVertexBuffer.Dispose();
            triangleTexcoordVertexBuffer.Dispose();

            inputLayout.Dispose();
        }

        public void Run( D3D11.ShaderResourceView srcSRV, D3D11.ShaderResourceView depthSRV, D3D11.RenderTargetView dstRTV, int width, int height, float time )
        {
            Viewport viewport = new Viewport( 0, 0, width, height );

            int positionSize = Utilities.SizeOf<Vector3>();
            int texcoordSize = Utilities.SizeOf<Vector2>();
            int vertexCount = trianglePositionVertexBuffer.Description.SizeInBytes / positionSize;

            deviceContext.InputAssembler.SetVertexBuffers( 0, new D3D11.VertexBufferBinding( trianglePositionVertexBuffer, positionSize, 0 ) );
            deviceContext.InputAssembler.SetVertexBuffers( 1, new D3D11.VertexBufferBinding( triangleTexcoordVertexBuffer, texcoordSize, 0 ) );
            deviceContext.InputAssembler.InputLayout = inputLayout;
            deviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;

            deviceContext.VertexShader.Set( vertexShader );
            deviceContext.PixelShader.Set( pixelShader );

            deviceContext.PixelShader.SetSampler( 0, samplerState );
            deviceContext.PixelShader.SetShaderResource( 0, srcSRV );
            deviceContext.PixelShader.SetShaderResource( 1, depthSRV );

            deviceContext.Rasterizer.SetViewport( viewport );

            deviceContext.OutputMerger.SetRenderTargets( dstRTV );

            UpdateConstantBuffer( time );

            deviceContext.Draw( vertexCount, 0 );

            deviceContext.OutputMerger.SetRenderTargets( null, (D3D11.RenderTargetView) null );
        }

        private void UpdateConstantBuffer( float time )
        {
            ConstantBuffer data = new ConstantBuffer();
            data.parameters = Vector4.Zero; // Tip: set yzw to something (focus plane for dof, perhaps radius and strength for ssao, etc.)
            data.parameters.X = time;

            data.parameters.Y = activateFixed;//ACTIVACION FIXED
            data.parameters.Z = activateVariable;//ACTIVATION VARIABLE
            data.parameters.W = depth;//DEPTH 1-1000

            deviceContext.UpdateSubresource( ref data, constantBuffer );

            deviceContext.VertexShader.SetConstantBuffer( 0, constantBuffer );
            deviceContext.PixelShader.SetConstantBuffer( 0, constantBuffer );
        }
    }
}
