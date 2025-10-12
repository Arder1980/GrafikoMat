using Microsoft.Graphics.Canvas;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;           // RenderTargetBitmap (WinUI 3)
using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.DirectX;                   // DirectXPixelFormat (Windows.* dla Win2D CreateFromBytes)
using Windows.Graphics.Imaging;
using Windows.Foundation;                        // Rect

namespace GrafikoMat.Services
{
    /// <summary>
    /// Jednorazowy snapshot wybranego widoku (FrameworkElement) + offline blur
    /// (downscale -> Gaussian -> upscale). Zwraca SoftwareBitmap gotowy do wstawienia
    /// jako Image.Source (1:1, bez skalowania).
    /// </summary>
    public sealed class SnapshotBlurService
    {
        public async Task<SoftwareBitmap?> CaptureAndBlurAsync(
            FrameworkElement root,
            int downscaleFactor = 2,
            float blurAmountAtHalfRes = 12f)
        {
            if (root == null || root.XamlRoot == null) return null;

            // 1) RenderTargetBitmap snapshot TYLKO TEGO widoku (WinUI 3: RenderAsync(UIElement))
            var rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(root);

            // Uwaga: PixelWidth/PixelHeight uwzględniają bieżący RasterizationScale – mamy 1:1 w pikselach
            int wPx = Math.Max(1, rtb.PixelWidth);
            int hPx = Math.Max(1, rtb.PixelHeight);

            var pixels = await rtb.GetPixelsAsync();
            if (pixels == null || pixels.Length == 0) return null;

            // 2) Win2D: z bufora -> CanvasBitmap
            var device = CanvasDevice.GetSharedDevice();
            using var srcBitmap = CanvasBitmap.CreateFromBytes(
                device,
                pixels.ToArray(), // IBuffer -> byte[]
                wPx,
                hPx,
                DirectXPixelFormat.B8G8R8A8UIntNormalized,
                96); // DPI logiczne – i tak renderujemy 1:1 pikselami

            // 3) Downscale + Gauss + Upscale (szybko i „mleczko” wygląda świetnie)
            int dw = Math.Max(1, wPx / Math.Max(1, downscaleFactor));
            int dh = Math.Max(1, hPx / Math.Max(1, downscaleFactor));

            using var downTarget = new CanvasRenderTarget(device, dw, dh, 96);
            using (var ds = downTarget.CreateDrawingSession())
            {
                ds.DrawImage(srcBitmap, new Rect(0, 0, dw, dh));
            }

            using var blurredHalf = new CanvasRenderTarget(device, dw, dh, 96);
            using (var ds = blurredHalf.CreateDrawingSession())
            {
                var blur = new Microsoft.Graphics.Canvas.Effects.GaussianBlurEffect
                {
                    Source = downTarget,
                    BlurAmount = blurAmountAtHalfRes,
                    BorderMode = Microsoft.Graphics.Canvas.Effects.EffectBorderMode.Hard,
                    Optimization = Microsoft.Graphics.Canvas.Effects.EffectOptimization.Balanced
                };
                ds.DrawImage(blur);
            }

            using var fullTarget = new CanvasRenderTarget(device, wPx, hPx, 96);
            using (var ds = fullTarget.CreateDrawingSession())
            {
                // Upscale bilinear do dokładnych wymiarów pikselowych zrzutu
                ds.DrawImage(blurredHalf, new Rect(0, 0, wPx, hPx));
            }

            // 4) Canvas -> SoftwareBitmap (BGRA8 premul) – gotowy dla Image.Source (Stretch=None => brak skalowania)
            byte[] finalBytes = fullTarget.GetPixelBytes();
            var sbmp = new SoftwareBitmap(BitmapPixelFormat.Bgra8, wPx, hPx, BitmapAlphaMode.Premultiplied);
            sbmp.CopyFromBuffer(finalBytes.AsBuffer());
            return sbmp;
        }
    }
}
