using System;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NAudio.Dsp;
using NAudio.Wave;

namespace Vimu
{
    public partial class MainWindow : Window
    {
        WasapiLoopbackCapture capture;
        float[] audioBuffer = new float[2048];
        int bufferOffset = 0;
        double[] smoothedHeights; 
        int barCount = 32; // this is where you could configure the amount of collums

        public MainWindow()
        {
            InitializeComponent();
            smoothedHeights = new double[barCount];
            StartAudio();
        }

        void StartAudio()
        {
            capture = new WasapiLoopbackCapture();
            capture.DataAvailable += OnAudio;
            capture.StartRecording();
        }

        void OnAudio(object sender, WaveInEventArgs e)
        {
            for (int i = 0; i < e.BytesRecorded; i += 4)
            {
                float sample = BitConverter.ToSingle(e.Buffer, i);
                audioBuffer[bufferOffset++] = sample;

                if (bufferOffset >= audioBuffer.Length)
                {
                    bufferOffset = 0;
                    ProcessFFT(audioBuffer);
                }
            }
        }

        void ProcessFFT(float[] samples)
        {
            int fftSize = samples.Length;
            NAudio.Dsp.Complex[] fftBuffer = new NAudio.Dsp.Complex[fftSize];

            for (int i = 0; i < fftSize; i++)
            {
                double window = 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / fftSize);
                fftBuffer[i].X = (float)(samples[i] * window);
                fftBuffer[i].Y = 0;
            }

            FastFourierTransform.FFT(true, (int)Math.Log(fftSize, 2), fftBuffer);

            Dispatcher.Invoke(() =>
            {
                VisualizerCanvas.Children.Clear();
                DrawBars(fftBuffer);
            });
        }

        void DrawBars(NAudio.Dsp.Complex[] fft)
        {
            int bars = barCount;
            double width = VisualizerCanvas.ActualWidth / bars;
            double canvasHeight = VisualizerCanvas.ActualHeight;

            for (int i = 0; i < bars && i < fft.Length / 2; i++)
            {
              
                double magnitude = Math.Sqrt(fft[i].X * fft[i].X + fft[i].Y * fft[i].Y);

                // If you wanna change the aplitude of the collums configure this
                magnitude = magnitude * 8000;

                if (magnitude > 0)
                {
                    magnitude = Math.Log10(1 + magnitude) * 120;
                }

                double targetHeight = Math.Min(magnitude, canvasHeight);

                // This is where you could make it smoother and a bit like jelly 
                double smoothingFactor = 0.3; // The smaller the number is, the animation gets smaller
                smoothedHeights[i] = smoothedHeights[i] * (1 - smoothingFactor) + targetHeight * smoothingFactor;

                // When the music stops, this is the "drop off"
                if (targetHeight < smoothedHeights[i])
                {
                    smoothedHeights[i] *= 0.85;
                }

                double height = Math.Max(smoothedHeights[i], 2);

                Color topColor = height > canvasHeight * 0.7 ? Colors.Red :
                                height > canvasHeight * 0.4 ? Colors.Yellow : Colors.Lime;

                Rectangle rect = new Rectangle
                {
                    Width = width - 4,
                    Height = height,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 1),
                        EndPoint = new Point(0, 0),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Colors.Blue, 0.0),
                            new GradientStop(Colors.Cyan, 0.3),
                            new GradientStop(topColor, 1.0)
                        }
                    },
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(rect, i * width + 2);
                Canvas.SetBottom(rect, 0);
                VisualizerCanvas.Children.Add(rect);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            capture?.StopRecording();
            capture?.Dispose();
            base.OnClosed(e);
        }
    }
}