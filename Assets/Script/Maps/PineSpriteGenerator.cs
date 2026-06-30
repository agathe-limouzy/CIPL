// PinSpriteGenerator.cs — À attacher sur le GameObject Pin
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class PinSpriteGenerator : MonoBehaviour
{
    public Color pinColor = new Color(0.9f, 0.2f, 0.2f);

    void Awake()
    {
        // Crée une texture 32×48 en forme de pin
        int w = 32, h = 48;
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color clear = Color.clear;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float cx = x - w / 2f, cy = y - h / 2f;

                // Corps circulaire (partie haute)
                float circle = cx * cx + (cy - 6) * (cy - 6);
                bool inCircle = circle < (w / 2f - 1) * (w / 2f - 1);

                // Pointe triangulaire (partie basse)
                float tipY = h / 2f - 8;
                bool inTip = (y < tipY) && (Mathf.Abs(cx) < (y / tipY) * (w / 2f - 1));

                if (inCircle || inTip)
                {
                    // Contour sombre
                    bool border = circle > (w / 2f - 3) * (w / 2f - 3) ||
                                  (inTip && Mathf.Abs(cx) > (y / tipY) * (w / 2f - 3));
                    tex.SetPixel(x, y, border ? pinColor * 0.6f : pinColor);
                }
                else tex.SetPixel(x, y, clear);
            }
        }

        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f));
        GetComponent<Image>().sprite = s;
    }
}