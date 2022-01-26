namespace Demo
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;

    using SharpDX;
    using SharpDX.D3DCompiler;

    using D3D11 = SharpDX.Direct3D11;
    using DXGI = SharpDX.DXGI;

    using static System.Math;
    using static CoreMath;

    public class Renderer : IDisposable
    {
        const int CubeCount = 32;

        private int width;
        private int height;

        private D3D11.Device device;
        private D3D11.DeviceContext deviceContext;

        private D3D11.Texture2D backbufferTexture;
        private D3D11.RenderTargetView backbufferRTV;

        private D3D11.Texture2D sceneTexture;
        private D3D11.ShaderResourceView sceneSRV;
        private D3D11.RenderTargetView sceneRTV;

        private D3D11.ShaderResourceView depthSRV;
        private D3D11.DepthStencilView depthDSV;

        private D3D11.DepthStencilState depthStencilState;
        private D3D11.RasterizerState rasterizerState;

        private D3D11.Buffer trianglePositionVertexBuffer;
        private D3D11.Buffer triangleIndexBuffer;

        [StructLayout( LayoutKind.Sequential )]
        private struct ConstantBuffer
        {
            public Matrix worldMatrix;
            public Matrix viewProjectionMatrix;
            public Matrix worldViewProjectionMatrix;
            public float time;
            public Vector3 padding;
        };
        private D3D11.Buffer constantBuffer;

        private D3D11.VertexShader mainVertexShader;
        private D3D11.PixelShader mainPixelShader;

        private D3D11.InputElement[] inputElements = new D3D11.InputElement[]
        {
            new D3D11.InputElement( "POSITION", 0, DXGI.Format.R32G32B32_Float, 0 ),
        };

        Vector3[] vertexPositions = new[]
        {
            new Vector3( -1.0f,  1.0f, -1.0f ), // TLB 0
            new Vector3(  1.0f,  1.0f, -1.0f ), // TRB 1
            new Vector3(  1.0f,  1.0f,  1.0f ), // TRF 2
            new Vector3( -1.0f,  1.0f,  1.0f ), // TLF 3
            new Vector3( -1.0f, -1.0f, -1.0f ), // BLB 4
            new Vector3(  1.0f, -1.0f, -1.0f ), // BRB 5
            new Vector3(  1.0f, -1.0f,  1.0f ), // BRF 6
            new Vector3( -1.0f, -1.0f,  1.0f )  // BLF 7
        };

        int[] vertexIndices = new int[]
        {
             3, 6, 2,   3, 7, 6, // Front
             1, 4, 0,   1, 5, 4, // Back
             0, 7, 3,   0, 4, 7, // Left
             2, 5, 1,   2, 6, 5, // Right
             0, 2, 1,   0, 3, 2, // Top
             7, 5, 6,   7, 4, 5, // Bottom
        };

        private D3D11.InputLayout inputLayout;

        private PostEffect postEffect;

        private Stopwatch stopWatch;

        public Renderer( D3D11.Device device, DXGI.SwapChain swapChain )
        {
            this.device = device;

            deviceContext = device.ImmediateContext;

            InitializeDeviceResources( swapChain );
            InitializeShaders();
            InitializeTriangle();

            postEffect = new PostEffect( device );

            stopWatch = Stopwatch.StartNew();
        }

        private void InitializeDeviceResources( DXGI.SwapChain swapChain )
        {
            backbufferTexture = swapChain.GetBackBuffer<D3D11.Texture2D>( 0 );
            backbufferRTV = new D3D11.RenderTargetView( device, backbufferTexture );

            width  = backbufferTexture.Description.Width;
            height = backbufferTexture.Description.Height;

            D3D11.Texture2DDescription sceneTextureDesc = new D3D11.Texture2DDescription
            {
                CpuAccessFlags = D3D11.CpuAccessFlags.None,
                BindFlags = D3D11.BindFlags.RenderTarget | D3D11.BindFlags.ShaderResource,
                Format = DXGI.Format.R8G8B8A8_UNorm,
                Width = width,
                Height = height,
                OptionFlags = D3D11.ResourceOptionFlags.None,
                MipLevels = 1,
                ArraySize = 1,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = D3D11.ResourceUsage.Default
            };

            sceneTexture = new D3D11.Texture2D( device, sceneTextureDesc );
            sceneRTV = new D3D11.RenderTargetView( device, sceneTexture );
            sceneSRV = new D3D11.ShaderResourceView( device, sceneTexture );

            var depthBufferDesc = new D3D11.Texture2DDescription()
            {
                Width = width,
                Height = height,
                MipLevels = 1,
                ArraySize = 1,
                Format = DXGI.Format.R32_Typeless,
                SampleDescription = { Count = 1, Quality = 0 },
                Usage = D3D11.ResourceUsage.Default,
                BindFlags = D3D11.BindFlags.DepthStencil | D3D11.BindFlags.ShaderResource,
                CpuAccessFlags = D3D11.CpuAccessFlags.None,
                OptionFlags = D3D11.ResourceOptionFlags.None
            };

            using ( var depthStencilBufferTexture = new D3D11.Texture2D( device, depthBufferDesc ) )
            {
                var depthStencilViewDesc = new D3D11.DepthStencilViewDescription()
                {
                    Format = DXGI.Format.D32_Float,
                    Dimension = D3D11.DepthStencilViewDimension.Texture2D,
                    Texture2D = { MipSlice = 0 }
                };

                depthDSV = new D3D11.DepthStencilView( device, depthStencilBufferTexture, depthStencilViewDesc );

                var shaderResourceViewDesc = new D3D11.ShaderResourceViewDescription()
                {
                    Format = DXGI.Format.R32_Float,
                    Dimension = SharpDX.Direct3D.ShaderResourceViewDimension.Texture2D,
                    Texture2D = { MipLevels = 1, MostDetailedMip = 0 }
                };

                depthSRV = new D3D11.ShaderResourceView( device, depthStencilBufferTexture, shaderResourceViewDesc );
            }

            var depthStencilDesc = new D3D11.DepthStencilStateDescription()
            {
                IsDepthEnabled = true,
                DepthWriteMask = D3D11.DepthWriteMask.All,
                DepthComparison = D3D11.Comparison.Less,
                IsStencilEnabled = false,
                StencilReadMask = 0xFF,
                StencilWriteMask = 0xFF,

                FrontFace = new D3D11.DepthStencilOperationDescription()
                {
                    FailOperation = D3D11.StencilOperation.Keep,
                    DepthFailOperation = D3D11.StencilOperation.Keep,
                    PassOperation = D3D11.StencilOperation.Keep,
                    Comparison = D3D11.Comparison.Always
                },

                BackFace = new D3D11.DepthStencilOperationDescription()
                {
                    FailOperation = D3D11.StencilOperation.Keep,
                    DepthFailOperation = D3D11.StencilOperation.Keep,
                    PassOperation = D3D11.StencilOperation.Keep,
                    Comparison = D3D11.Comparison.Always
                }
            };

            depthStencilState = new D3D11.DepthStencilState( device, depthStencilDesc );

            var rasterDesc = new D3D11.RasterizerStateDescription()
            {
                IsAntialiasedLineEnabled = false,
                CullMode = D3D11.CullMode.Back,
                DepthBias = 0,
                DepthBiasClamp = 0.0f,
                IsDepthClipEnabled = true,
                FillMode = D3D11.FillMode.Solid,
                IsFrontCounterClockwise = false,
                IsMultisampleEnabled = false,
                IsScissorEnabled = false,
                SlopeScaledDepthBias = 0.0f
            };

            rasterizerState = new D3D11.RasterizerState( device, rasterDesc );
        }

        private void InitializeShaders()
        {
            ConstantBuffer data = new ConstantBuffer();
            data.worldViewProjectionMatrix = Matrix.Identity;
            data.worldMatrix = Matrix.Identity;
            data.viewProjectionMatrix = Matrix.Identity;
            data.time = 0.0f;
            constantBuffer = D3D11.Buffer.Create( device, D3D11.BindFlags.ConstantBuffer, ref data );

            string textVS = System.IO.File.ReadAllText( "Shaders\\MainVS.hlsl" );
            using ( var vertexShaderByteCode = ShaderBytecode.Compile( textVS, "main", "vs_4_0", ShaderFlags.Debug | ShaderFlags.WarningsAreErrors, EffectFlags.None, null, new StreamInclude(), sourceFileName : "Shaders\\MainVS.hlsl" ) )
            {
                if ( vertexShaderByteCode.Bytecode == null )
                {
                    MessageBox.Show( vertexShaderByteCode.Message, "Shader Compilation Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                    System.Environment.Exit( -1 );
                }

                mainVertexShader = new D3D11.VertexShader( device, vertexShaderByteCode );

                using( var inputSignature = ShaderSignature.GetInputSignature( vertexShaderByteCode ) )
                    inputLayout = new D3D11.InputLayout( device, inputSignature, inputElements );
            }

            string textPS = System.IO.File.ReadAllText( "Shaders\\MainPS.hlsl" );
            using ( var pixelShaderByteCode = ShaderBytecode.Compile( textPS, "main", "ps_4_0", ShaderFlags.Debug | ShaderFlags.WarningsAreErrors, EffectFlags.None, null, new StreamInclude(), sourceFileName : "Shaders\\MainPS.hlsl" ) )
            {
                if ( pixelShaderByteCode.Bytecode == null )
                {
                    MessageBox.Show( pixelShaderByteCode.Message, "Shader Compilation Error", MessageBoxButtons.OK, MessageBoxIcon.Error );
                    System.Environment.Exit( -1 );
                }

                mainPixelShader = new D3D11.PixelShader( device, pixelShaderByteCode );
            }
        }

        private void InitializeTriangle()
        {
            trianglePositionVertexBuffer = D3D11.Buffer.Create( device, D3D11.BindFlags.VertexBuffer, vertexPositions );
            triangleIndexBuffer = D3D11.Buffer.Create( device, D3D11.BindFlags.IndexBuffer, vertexIndices );
        }

        public void Dispose()
        {
            backbufferTexture.Dispose();
            backbufferRTV.Dispose();

            sceneTexture.Dispose();
            sceneRTV.Dispose();
            sceneSRV.Dispose();

            depthDSV.Dispose();
            depthSRV.Dispose();

            depthStencilState.Dispose();
            rasterizerState.Dispose();

            constantBuffer.Dispose();
            mainVertexShader.Dispose();
            mainPixelShader.Dispose();

            trianglePositionVertexBuffer.Dispose();
            triangleIndexBuffer.Dispose();

            inputLayout.Dispose();

            postEffect.Dispose();
        }

        public void Render()
        {
            float time = stopWatch.ElapsedMilliseconds / 1000.0f;

            Viewport viewport = new Viewport( 0, 0, width, height );

            int vertexSize = Utilities.SizeOf<Vector3>();
            int vertexCount = trianglePositionVertexBuffer.Description.SizeInBytes / vertexSize;

            deviceContext.InputAssembler.SetVertexBuffers( 0, new D3D11.VertexBufferBinding( trianglePositionVertexBuffer, vertexSize, 0 ) );
            deviceContext.InputAssembler.InputLayout = inputLayout;
            deviceContext.InputAssembler.PrimitiveTopology = SharpDX.Direct3D.PrimitiveTopology.TriangleList;

            deviceContext.VertexShader.Set( mainVertexShader );
            deviceContext.PixelShader.Set( mainPixelShader );

            deviceContext.Rasterizer.SetViewport( viewport );
            deviceContext.Rasterizer.State = rasterizerState;

            deviceContext.OutputMerger.SetDepthStencilState( depthStencilState, 1 );
            deviceContext.OutputMerger.SetRenderTargets( depthDSV, sceneRTV );

            deviceContext.ClearDepthStencilView( depthDSV, D3D11.DepthStencilClearFlags.Depth, 1.0f, 0 );
            deviceContext.ClearRenderTargetView( sceneRTV, new Color( 255, 135, 60 ) );

            for ( int i = 0; i < CubeCount; i++ )
            {
                for ( int j = 0; j < CubeCount; j++ )
                {
                    UpdateConstantBuffer( GetConstantBufferForCube( i, j, time ) );

                    int indexCount = triangleIndexBuffer.Description.SizeInBytes / Utilities.SizeOf<int>();
                    deviceContext.InputAssembler.SetIndexBuffer( triangleIndexBuffer, DXGI.Format.R32_UInt, 0 );
                    deviceContext.DrawIndexed( indexCount, 0, 0 );
                }
            }

            deviceContext.OutputMerger.SetRenderTargets( null, (D3D11.RenderTargetView) null ); // Tip: always set to null after rendering. If bound as rendertarget it cannot be set as shader resource view (as texture)

            postEffect.Run( sceneSRV, depthSRV, backbufferRTV, width, height, time );
        }

        private ConstantBuffer GetConstantBufferForCube( int i, int j, float time )
        {
            ConstantBuffer data;

            float aspectRatio = (float) width / height;

            float step = 2.25f;
            float origin = -( step * CubeCount ) / 2.0f;
            float x = origin + step * i;
            float y = origin + step * j;
            float z = (float)Math.Sin((float)Math.Sqrt(x * x + y * y) + time);
            Matrix worldMatrix = Matrix.Translation( x, y, z );

            var cameraPosition = new Vector3( 90.0f, -90.0f, -90.0f);
            var cameraTarget = Vector3.Zero;
            var cameraUp = -Vector3.UnitZ;
            var viewMatrix = Matrix.LookAtLH( 0.75f * cameraPosition, cameraTarget, cameraUp );

            Matrix projectionMatrix = Matrix.PerspectiveFovLH( 2.0f * (float) PI * Remap( 0.0f, 360.0f, 45.0f ), aspectRatio, 1f, 1000.0f );//near y far

            data.worldMatrix = worldMatrix;
            data.viewProjectionMatrix = viewMatrix * projectionMatrix;
            data.worldViewProjectionMatrix = worldMatrix * viewMatrix * projectionMatrix;

            data.time = time;
            data.padding = Vector3.Zero;

            return data;
        }

        private void UpdateConstantBuffer( ConstantBuffer data )
        {
            deviceContext.UpdateSubresource( ref data, constantBuffer );

            deviceContext.VertexShader.SetConstantBuffer( 0, constantBuffer );
            deviceContext.PixelShader.SetConstantBuffer( 0, constantBuffer );
        }
    }
}
