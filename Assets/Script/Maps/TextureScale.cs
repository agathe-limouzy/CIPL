// Source : wiki.unity3d.com/index.php/TextureScale
using UnityEngine;
using System.Threading;

public class TextureScale
{
    public class ThreadData
    {
        public int start, end;
        public ThreadData(int s, int e) { start = s; end = e; }
    }

    private static Color[] texColors;
    private static Color[] newColors;
    private static int w, h2;
    private static float ratioX, ratioY;
    private static int w2, finishCount;
    private static Mutex mutex;

    public static void Bilinear(Texture2D tex, int newWidth, int newHeight)
    {
        ThreadedScale(tex, newWidth, newHeight, true);
    }

    private static void ThreadedScale(Texture2D tex, int newWidth, int newHeight, bool useBilinear)
    {
        texColors = tex.GetPixels();
        newColors = new Color[newWidth * newHeight];
        if (useBilinear) { ratioX = 1.0f / ((float)newWidth / (tex.width - 1)); ratioY = 1.0f / ((float)newHeight / (tex.height - 1)); }
        else { ratioX = ((float)tex.width) / newWidth; ratioY = ((float)tex.height) / newHeight; }
        w = tex.width; w2 = newWidth; h2 = newHeight;
        int cores = Mathf.Min(SystemInfo.processorCount, newHeight);
        int step = newHeight / cores;
        finishCount = 0;
        mutex = new Mutex(false);

        if (cores > 1)
        {
            for (int i = 0; i < cores - 1; i++)
            {
                var td = new ThreadData(step * i, step * (i + 1));
                var t = new Thread(useBilinear ? (ParameterizedThreadStart)BilinearScale : PointScale);
                t.Start(td);
            }
            var last = new ThreadData(step * (cores - 1), newHeight);
            if (useBilinear) BilinearScale(last); else PointScale(last);
        }
        else
        {
            var td = new ThreadData(0, newHeight);
            if (useBilinear) BilinearScale(td); else PointScale(td);
        }

        tex.Reinitialize(newWidth, newHeight);
        tex.SetPixels(newColors);
        tex.Apply();
    }

    private static void BilinearScale(System.Object obj)
    {
        var range = (ThreadData)obj;
        for (int y = range.start; y < range.end; y++)
        {
            int yFloor = (int)Mathf.Floor(y * ratioY);
            var y1 = yFloor * w; var y2 = (yFloor + 1) * w;
            var yw = y * w2;
            for (int x = 0; x < w2; x++)
            {
                int xFloor = (int)Mathf.Floor(x * ratioX);
                float xLerp = x * ratioX - xFloor;
                newColors[yw + x] = ColorLerpUnclamped(
                    ColorLerpUnclamped(texColors[y1 + xFloor], texColors[y1 + xFloor + 1], xLerp),
                    ColorLerpUnclamped(texColors[y2 + xFloor], texColors[y2 + xFloor + 1], xLerp),
                    y * ratioY - yFloor);
            }
        }
        mutex.WaitOne(); finishCount++; mutex.ReleaseMutex();
    }

    private static void PointScale(System.Object obj)
    {
        var range = (ThreadData)obj;
        for (var y = range.start; y < range.end; y++)
        {
            var yw = y * w2;
            for (var x = 0; x < w2; x++)
                newColors[yw + x] = texColors[(int)(y * ratioY) * w + (int)(x * ratioX)];
        }
        mutex.WaitOne(); finishCount++; mutex.ReleaseMutex();
    }

    private static Color ColorLerpUnclamped(Color c1, Color c2, float value)
    {
        return new Color(c1.r + (c2.r - c1.r) * value, c1.g + (c2.g - c1.g) * value,
                         c1.b + (c2.b - c1.b) * value, c1.a + (c2.a - c1.a) * value);
    }
}
