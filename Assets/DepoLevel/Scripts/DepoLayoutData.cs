// Kasa Avcisi - Depo level verisi (DepoLayout.json) icin serilestirme siniflari.
// JSON koordinatlari: metre, Unity eksenleri (X dogu, Y yukari, Z kuzey).
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DepoLevel
{
    [Serializable]
    public class DepoLayoutFile
    {
        public string version;
        public string units;
        public DepoBounds bounds;
        public DepoMaterialDef[] materials;
        public DepoZoneDef[] zones;
        public DepoNodeDef[] nodes;
    }

    [Serializable]
    public class DepoBounds
    {
        public float[] ana_depo;        // x0, z0, x1, z1, tavan yuksekligi
        public float[] koridor_binasi;  // x0, z0, x1, z1, tavan yuksekligi
        public float koridor_genislik;
        public float[] konteyner;      // uzunluk, genislik, yukseklik
        public float konteyner_adim;
    }

    [Serializable]
    public class DepoMaterialDef
    {
        public string key;
        public string color;
        public float smoothness;
        public float metallic;
        public string emission;
        public float emissionIntensity;
        public int edge = 1;     // 1 = yuzey kenarlarina murekkep cizgisi
        public int outline = 1;  // 1 = dis kontur
        public float[] tiling;   // doku tekrari (x, y), bos = 1,1
    }

    [Serializable]
    public class DepoZoneDef
    {
        public string id;
        public string name;
        public string color;
        public string rects;   // "x0,z0,x1,z1|x0,z0,x1,z1"
        public float tx, tz, tyaw;
    }

    [Serializable]
    public class DepoNodeDef
    {
        public string id;
        public string parent;
        public string kind;    // group | box | cyl | label | slot | zone | loot | spawn | trigger | light
        public string name;
        public float px, py, pz;
        public float rx, ry;
        public float sx, sy, sz;
        public string mat;
        public int nc;         // 1 = collider yok
        public string prefab;  // DepoPrefabMap anahtari
        public string text;    // label yazisi
        public string tag;     // floor | roof | placeholder | container | zone
        public string data;    // "k=v;k=v"
        public float lr, li;   // isik menzil / siddet
        public string lc;      // isik rengi
    }

    public static class DepoUtil
    {
        public static Dictionary<string, string> ParseData(string data)
        {
            var d = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(data)) return d;
            foreach (var part in data.Split(';'))
            {
                int i = part.IndexOf('=');
                if (i <= 0) continue;
                d[part.Substring(0, i).Trim()] = part.Substring(i + 1).Trim();
            }
            return d;
        }

        public static string Get(Dictionary<string, string> d, string key, string fallback = "")
        {
            return d != null && d.TryGetValue(key, out var v) ? v : fallback;
        }

        public static Color Hex(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c)) return c;
            return fallback;
        }

        public static List<Rect> ParseRects(string rects)
        {
            var list = new List<Rect>();
            if (string.IsNullOrEmpty(rects)) return list;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var r in rects.Split('|'))
            {
                var p = r.Split(',');
                if (p.Length != 4) continue;
                float x0 = float.Parse(p[0], inv), z0 = float.Parse(p[1], inv);
                float x1 = float.Parse(p[2], inv), z1 = float.Parse(p[3], inv);
                list.Add(Rect.MinMaxRect(Mathf.Min(x0, x1), Mathf.Min(z0, z1), Mathf.Max(x0, x1), Mathf.Max(z0, z1)));
            }
            return list;
        }
    }
}
