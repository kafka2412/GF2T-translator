using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GF2T.Util
{
    public static class Otsu
    {
        // function is used to compute the q values in the equation
        private static float Px(int init, int end, int[] hist)
        {
            int sum = 0;
            int i;
            for (i = init; i <= end; i++)
                sum += hist[i];

            return (float)sum;
        }

        // function is used to compute the mean values in the equation (mu)
        private static float Mx(int init, int end, int[] hist)
        {
            int sum = 0;
            int i;
            for (i = init; i <= end; i++)
                sum += i * hist[i];

            return (float)sum;
        }

        // finds the maximum element in a vector
        private static int findMax(float[] vec, int n)
        {
            float maxVec = 0;
            int idx = 0;
            int i;

            for (i = 1; i < n - 1; i++)
            {
                if (vec[i] > maxVec)
                {
                    maxVec = vec[i];
                    idx = i;
                }
            }
            return idx;
        }

        // simply computes the image histogram
        unsafe private static void getHistogram(byte* p, int w, int h, int ws, int[] hist)
        {
            hist.Initialize();
            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < w * 3; j += 3)
                {
                    int index = i * ws + j;
                    hist[p[index]]++;
                }
            }
        }

        // find otsu threshold
        public static int getOtsuThreshold(Bitmap bmp)
        {
            byte t = 0;
            float[] vet = new float[256];
            int[] hist = new int[256];
            vet.Initialize();

            float p1, p2, p12;
            int k;

            BitmapData bmData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* p = (byte*)(void*)bmData.Scan0.ToPointer();

                getHistogram(p, bmp.Width, bmp.Height, bmData.Stride, hist);

                // loop through all possible t values and maximize between class variance
                for (k = 1; k != 255; k++)
                {
                    p1 = Px(0, k, hist);
                    p2 = Px(k + 1, 255, hist);
                    p12 = p1 * p2;
                    if (p12 == 0)
                        p12 = 1;
                    float diff = (Mx(0, k, hist) * p2) - (Mx(k + 1, 255, hist) * p1);
                    vet[k] = (float)diff * diff / p12;
                    //vet[k] = (float)Math.Pow((Mx(0, k, hist) * p2) - (Mx(k + 1, 255, hist) * p1), 2) / p12;
                }
            }
            bmp.UnlockBits(bmData);

            t = (byte)findMax(vet, 256);

            return t;
        }

        public static void Convert2GrayScaleFast(Bitmap bmp)
        {
            // Lock the bitmap's bits
            Rectangle rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
            BitmapData bmData = bmp.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);

            unsafe
            {
                // Get the address of the first line
                byte* pBase = (byte*)bmData.Scan0.ToPointer();

                // Declare an array to hold the bytes of the bitmap
                byte[] rgbValues = new byte[bmData.Stride * bmData.Height];

                // Copy the RGB values into the array
                Marshal.Copy(bmData.Scan0, rgbValues, 0, rgbValues.Length);

                // Get the number of bytes in the bitmap
                int numBytes = bmData.Stride * bmData.Height;

                // Iterate over each pixel in the bitmap
                for (int counter = 0; counter < numBytes; counter += 4)
                {
                    // Calculate the average of the blue, green, and red values
                    byte grayValue = (byte)(.299 * rgbValues[counter + 2] + .587 * rgbValues[counter + 1] + .114 * rgbValues[counter]);

                    // Set the new color value
                    rgbValues[counter] = grayValue;
                    rgbValues[counter + 1] = grayValue;
                    rgbValues[counter + 2] = grayValue;
                }

                // Copy the RGB values back to the bitmap
                Marshal.Copy(rgbValues, 0, bmData.Scan0, numBytes);
            }

            // Unlock the bits
            bmp.UnlockBits(bmData);
        }


        // simple routine for thresholdin
        public static void threshold(Bitmap bmp, int thresh)
        {
            BitmapData bmData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* p = (byte*)(void*)bmData.Scan0.ToPointer();
                int h = bmp.Height;
                int w = bmp.Width;
                int ws = bmData.Stride;

                for (int i = 0; i < h; i++)
                {
                    byte* row = &p[i * ws];
                    for (int j = 0; j < w * 3; j += 3)
                    {
                        row[j] = (byte)((row[j] > (byte)thresh) ? 255 : 0);
                        row[j + 1] = (byte)((row[j + 1] > (byte)thresh) ? 255 : 0);
                        row[j + 2] = (byte)((row[j + 2] > (byte)thresh) ? 255 : 0);
                    }
                }
            }
            bmp.UnlockBits(bmData);
        }
    }
}
