using System;
using Avalonia;
using Avalonia.Media.Imaging;
using OpenCvSharp;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace Satellite.Tools;

public class ImageHelper
{
    // scale to one of thw width or height, makes the bitmap larger than the size given
    public static Bitmap ScaleBitmap(Bitmap sourceBitmap, int distWidth, int distHeight)
    {
        var oriW = sourceBitmap.Size.Width;
        var oriH = sourceBitmap.Size.Height;

        var windowWhRate = distWidth / (double)distHeight;
        var imgWhRate = oriW / oriH;

        if (imgWhRate > windowWhRate) // extra width
            distWidth = (int)Math.Ceiling(distHeight * imgWhRate);
        else if (imgWhRate < windowWhRate) // extra height
            distHeight = (int)Math.Ceiling(distWidth / imgWhRate);

        return sourceBitmap.CreateScaledBitmap(new PixelSize(distWidth, distHeight));
    }

    public static Mat ScaleCv2(Mat srcImage, int distWidth, int distHeight)
    {
        var windowWhRate = distWidth / (double)distHeight;
        var imgWhRate = srcImage.Width / (double)srcImage.Height;

        if (imgWhRate > windowWhRate) // extra width
            distWidth = (int)Math.Ceiling(distHeight * imgWhRate);
        else if (imgWhRate < windowWhRate) // extra height
            distHeight = (int)Math.Ceiling(distWidth / imgWhRate);

        var resizedImage = new Mat();
        Cv2.Resize(srcImage, resizedImage, new Size(distWidth, distHeight),
            interpolation: InterpolationFlags.Linear);
        return resizedImage;
    }

    public static Bitmap CropBitmap(Bitmap sourceBitmap, int x, int y, int width, int height)
    {
        var piexlRect = new PixelRect(x, y, width, height);
        var distBitmap =
            new WriteableBitmap(piexlRect.Size, sourceBitmap.Dpi, sourceBitmap.Format, sourceBitmap.AlphaFormat);
        using var lb = distBitmap.Lock();
        sourceBitmap.CopyPixels(piexlRect, lb.Address, lb.RowBytes * piexlRect.Height, lb.RowBytes);
        return distBitmap;
    }


    public static Bitmap CenterCropBitmap(Bitmap sourceBitmap, int distWidth, int distHeight)
    {
        var imgW = (int)sourceBitmap.Size.Width;
        var imgH = (int)sourceBitmap.Size.Height;
        return CropBitmap(sourceBitmap, imgW / 2 - distWidth / 2, imgH / 2 - distHeight / 2, distWidth, distHeight);
    }

    public static Mat CropCv2(Mat image, int x, int y, int distWidth, int distHeight)
    {
        var rect = new Rect(x, y, distWidth, distHeight);
        return new Mat(image, rect);
    }

    public static Mat CenterCropCv2(Mat image, int distWidth, int distHeight)
    {
        return CropCv2(image, image.Width / 2 - distWidth / 2, image.Height / 2 - distHeight / 2, distWidth,
            distHeight);
    }

    public static Mat GaussianBlur(Mat srcImage, int ksizeWidth, int ksizeHeight, double sigmaX, double sigmaY = 0)
    {
        var blurImg = new Mat();
        sigmaY = sigmaY == 0 ? sigmaX : sigmaY;
        Cv2.GaussianBlur(srcImage, blurImg, new Size(ksizeWidth, ksizeHeight), sigmaX, sigmaY);
        return blurImg;
    }
}