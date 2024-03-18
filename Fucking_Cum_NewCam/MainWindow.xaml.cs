using AVT.VmbAPINET;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using Window = System.Windows.Window;

namespace Fucking_Cum_NewCam
{
    public partial class MainWindow : Window
    {
        private Vimba _vimba;
        private Camera _camera;
        private Frame[] _frameArray;
        private bool _acquiring; // To keep track if we are still acquiring images

        public MainWindow()
        {
            InitializeComponent();
            InitializeVimba();
        }

        private void InitializeVimba()
        {
            _vimba = new Vimba();
            _vimba.Startup();
            Debug.WriteLine("Vimba initialized");

            CameraCollection cameras = _vimba.Cameras;
            Debug.WriteLine($"Number of cameras found: {cameras.Count}");

            if (cameras.Count > 0)
            {
                _camera = cameras[0];
                _camera.Open(VmbAccessModeType.VmbAccessModeFull);
                Debug.WriteLine($"Camera {_camera.Id} opened");

                StartImageAcquisition();
            }
            else
            {
                MessageBox.Show("No cameras found.");
            }
        }

        private void StartImageAcquisition()
        {
            if (_camera != null)
            {
                AdjustPacketSize();
                SetupCameraForCapture();
            }
            else
            {
                MessageBox.Show("Camera is not initialized.");
            }
        }

        private void AdjustPacketSize()
        {
            try
            {
                var adjustPacketSizeFeature = _camera.Features["GVSPAdjustPacketSize"];
                if (adjustPacketSizeFeature != null)
                {
                    adjustPacketSizeFeature.RunCommand();
                    while (!adjustPacketSizeFeature.IsCommandDone())
                    {
                        Debug.WriteLine("Adjusting packet size...");
                    }
                    Debug.WriteLine("Packet size adjusted.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception while adjusting packet size: {ex.Message}");
            }
        }

        private void SetupCameraForCapture()
        {
            long payloadSize = _camera.Features["PayloadSize"].IntValue;
            _frameArray = new Frame[5];
            for (int i = 0; i < _frameArray.Length; i++)
            {
                _frameArray[i] = new Frame(payloadSize);
                _camera.AnnounceFrame(_frameArray[i]);
            }

            _camera.StartCapture();
            foreach (var frame in _frameArray)
            {
                _camera.QueueFrame(frame);
            }

            _camera.OnFrameReceived += OnFrameReceived;
            _camera.Features["AcquisitionMode"].EnumValue = "Continuous";
            _camera.Features["AcquisitionStart"].RunCommand();
            _acquiring = true;
        }

        private void OnFrameReceived(Frame frame)
        {
            if (!_acquiring) return; // Exit if we are no longer acquiring

            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    Debug.WriteLine($"Frame received: {frame.FrameID}, Status: {frame.ReceiveStatus}");
                    ProcessFrame(frame);
                    if (_acquiring)
                    {
                        _camera.QueueFrame(frame);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error processing frame: {ex.Message}");
                }
            }));
        }

        private void ProcessFrame(Frame frame)
        {
            if (frame.ReceiveStatus == VmbFrameStatusType.VmbFrameStatusComplete)
            {
                using (var mat = new Mat((int)frame.Height, (int)frame.Width, MatType.CV_8UC1, frame.Buffer))
                {
                    var bitmapSource = BitmapSourceConverter.ToBitmapSource(mat);
                    imgFrame.Source = bitmapSource;
                }
            }
        }

        private void StopCamera()
        {
            if (_camera != null)
            {
                _acquiring = false; // Indicate that we are no longer acquiring before stopping the camera

                try
                {
                    _camera.Features["AcquisitionStop"].RunCommand();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error stopping acquisition: {ex.Message}");
                }

                try
                {
                    _camera.EndCapture();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error ending capture: {ex.Message}");
                }

                try
                {
                    _camera.FlushQueue();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error flushing queue: {ex.Message}");
                }

                try
                {
                    _camera.RevokeAllFrames();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error revoking frames: {ex.Message}");
                }

                try
                {
                    _camera.Close();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing camera: {ex.Message}");
                }
                finally
                {
                    _camera = null;
                    Debug.WriteLine("Camera set to null.");
                }
            }

            Debug.WriteLine("Shutting down Vimba...");
            _vimba.Shutdown();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopCamera();
            base.OnClosed(e);
        }
    }
}
