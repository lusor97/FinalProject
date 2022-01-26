namespace Demo
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Runtime.InteropServices;
    using System.Windows.Forms;
    
    using SharpDX.Direct3D;
    using SharpDX.Windows;

    using D3D11 = SharpDX.Direct3D11;
    using DXGI = SharpDX.DXGI;

    public class Program : IDisposable
    {
        private const string Title = "DoF OFF";

        private const int Width = 1280 *2;
        private const int Height = 720 *2;

        private RenderForm renderForm;
        private DXGI.SwapChain swapChain;
        private D3D11.Device device;
        private Renderer renderer;

        [DllImport("kernel32.dll", EntryPoint = "LoadLibrary")]
        static extern int LoadLibrary( [MarshalAs( UnmanagedType.LPStr )] string lpLibFileName );

        [DllImport("kernel32.dll", EntryPoint = "FreeLibrary")]
        static extern bool FreeLibrary( int hModule );

        public static void Main( string[] args )
        {
            int hmod = Environment.Is64BitProcess ? LoadLibrary( "x64\\d3dcompiler_47.dll" ) : LoadLibrary( "x86\\d3dcompiler_47.dll" );
            Debug.Assert( hmod != 0 );

            using ( Program program = new Program() )
            {
                program.renderForm.KeyDown += ( s, e ) => program.KeyDownCallback( e.KeyCode );

                RenderLoop.Run( program.renderForm, program.RenderCallback );
            }

            FreeLibrary( hmod );
        }

        private Program()
        {
            SharpDX.Configuration.EnableObjectTracking = true;

            renderForm = new RenderForm( Title );
            renderForm.ClientSize = new Size( Width, Height );
            renderForm.AllowUserResizing = false;

            InitializeSwapChain();

            renderer = new Renderer( device, swapChain );

            SetTitle();
        }

        private void InitializeSwapChain()
        {
            DXGI.ModeDescription backBufferDesc = new DXGI.ModeDescription( Width, Height, new DXGI.Rational( 60, 1 ), DXGI.Format.R8G8B8A8_UNorm );

            DXGI.SwapChainDescription swapChainDesc = new DXGI.SwapChainDescription()
            {
                ModeDescription = backBufferDesc,
                SampleDescription = new DXGI.SampleDescription( 1, 0 ),
                Usage = DXGI.Usage.RenderTargetOutput,
                BufferCount = 1,
                OutputHandle = renderForm.Handle,
                IsWindowed = true
            };

            D3D11.Device.CreateWithSwapChain( DriverType.Hardware, D3D11.DeviceCreationFlags.None, swapChainDesc, out device, out swapChain );
        }

        private void SetTitle()
        {
            renderForm.Text = Title;
            if (renderer.PostEffect.activateFixed == 1.0f)
            {
                renderForm.Text = "DoF FIXED: ";
            }
            if (renderer.PostEffect.activateVariable == 1.0f)
            {
                renderForm.Text = "DoF Variable: Depth -> " + renderer.PostEffect.depth.ToString();
            }
        }

        public void Dispose()
        {
            renderForm.Dispose();
            swapChain.Dispose();
            device.Dispose();
            renderer.Dispose();

            Console.Write( SharpDX.Diagnostics.ObjectTracker.ReportActiveObjects() );
        }

        private void KeyDownCallback( Keys key )
        {
            if ( key == Keys.Escape )
                renderForm.Dispose();

            if (key == Keys.F) //DoF ACTIVATION Fixed
            {
                renderer.PostEffect.activateFixed += 1.0f % 2;
                renderer.PostEffect.activateVariable = 0.0f;
            }

            if (key == Keys.V) //DoF ACTIVATION Variable
            {
                renderer.PostEffect.activateFixed = 0.0f;
                renderer.PostEffect.activateVariable += 1.0f % 2;
            }

            if (key == Keys.Space) //DoF DEACTIVATE
            {
                renderer.PostEffect.activateFixed = 0.0f;
                renderer.PostEffect.activateVariable = 0.0f;
                renderer.PostEffect.depth = 100.0f;
            }

            if (key == Keys.Up) //DoF +DEPTH
            {
                renderer.PostEffect.depth += 1.0f;
            }

            if (key == Keys.Down) //DoF -DEPTH
            {
                renderer.PostEffect.depth -= 1.0f;
            }

            SetTitle();
        }

        private void RenderCallback()
        {
            renderer.Render();
            swapChain.Present( 1, DXGI.PresentFlags.None );
        }
    }
}
