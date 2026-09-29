using System.Numerics;
using System.Text.Json;
using System.Xml.Linq;
using Raylib_cs;

/// Um ponto de spawn vindo da camada de objetos do Tiled.
record SpawnPoint(string Kind, float X, float Y);

/// Um tileset (a imagem com os tiles) já carregado na GPU.
class Tileset
{
    public int FirstGid, TileW, TileH, Columns, Margin, Spacing;
    public Texture2D Tex;
}

/// Uma camada de tiles (arte e/ou colisão).
class TileLayer
{
    public string Name = "";
    public int W, H;
    public uint[] Gids = Array.Empty<uint>();   // valores crus (com flags de flip nos bits altos)
    public float OffX, OffY, Opacity = 1f;
    public bool Visible = true;
    public bool Foreground;                     // desenhada POR CIMA do player
}

class LevelData
{
    public int TileW, TileH, Width, Height;     // Width/Height em TILES
    public List<Rectangle> Solids = new();
    public List<SpawnPoint> Spawns = new();
    public List<Tileset> Tilesets = new();
    public List<TileLayer> TileLayers = new();

    public bool HasArt => Tilesets.Count > 0;
    public float PixelHeight => Height * TileH;

    /// Desenha as camadas de tiles visíveis dentro de 'view' (coordenadas do mundo).
    public void Draw(Rectangle view, bool foreground)
    {
        const int pad = 3; // tiles maiores que o grid "vazam" pra cima/direita
        foreach (var layer in TileLayers)
        {
            if (!layer.Visible || layer.Foreground != foreground) continue;

            var tint = new Color((byte)255, (byte)255, (byte)255, (byte)(255 * layer.Opacity));

            int c0 = Math.Max(0, (int)MathF.Floor((view.X - layer.OffX) / TileW) - pad);
            int c1 = Math.Min(layer.W - 1, (int)MathF.Floor((view.X + view.Width - layer.OffX) / TileW) + pad);
            int r0 = Math.Max(0, (int)MathF.Floor((view.Y - layer.OffY) / TileH) - pad);
            int r1 = Math.Min(layer.H - 1, (int)MathF.Floor((view.Y + view.Height - layer.OffY) / TileH) + pad);

            for (int r = r0; r <= r1; r++)
                for (int c = c0; c <= c1; c++)
                    DrawTile(layer.Gids[r * layer.W + c], c * TileW + layer.OffX, r * TileH + layer.OffY, tint);
        }
    }

    void DrawTile(uint raw, float x, float y, Color tint)
    {
        uint gid = raw & 0x1FFFFFFF;
        if (gid == 0) return;

        bool flipH = (raw & 0x80000000) != 0;
        bool flipV = (raw & 0x40000000) != 0;
        // (flip diagonal, bit 29, não é suportado — raro em plataforma)

        // acha o tileset dono desse gid (o de maior FirstGid que seja <= gid)
        Tileset? ts = null;
        for (int i = Tilesets.Count - 1; i >= 0; i--)
            if (Tilesets[i].FirstGid <= gid) { ts = Tilesets[i]; break; }
        if (ts == null) return;

        int id = (int)(gid - ts.FirstGid);
        int col = id % ts.Columns;
        int row = id / ts.Columns;

        var src = new Rectangle(
            ts.Margin + col * (ts.TileW + ts.Spacing),
            ts.Margin + row * (ts.TileH + ts.Spacing),
            flipH ? -ts.TileW : ts.TileW,
            flipV ? -ts.TileH : ts.TileH);

        // no Tiled, tiles maiores que o grid ficam alinhados pela base da célula
        var dst = new Rectangle(x, y + TileH - ts.TileH, ts.TileW, ts.TileH);

        Raylib.DrawTexturePro(ts.Tex, src, dst, Vector2.Zero, 0f, tint);
    }
}

/// Lê um mapa do Tiled exportado em JSON.
///
/// Convenções:
///   - Camada de tiles chamada "Solids"  -> COLISÃO (qualquer tile pintado vira bloco sólido)
///   - Todas as camadas de tiles visíveis são DESENHADAS (esconda a "Solids" no Tiled se ela for só colisão)
///   - Camada de tiles cujo nome começa com "fg" ou "front" -> desenhada por cima do player
///   - Objetos ponto com Class: PlayerSpawn | Enemy | Boss  (x,y = onde ficam os PÉS)
static class TiledLoader
{
    static readonly Dictionary<string, Texture2D> textureCache = new();

    public static LevelData Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Mapa nao encontrado: {Path.GetFullPath(path)}\n" +
                "Rode 'dotnet run' dentro da pasta do projeto e confira se Assets/level1.json existe.");

        string mapDir = Path.GetDirectoryName(Path.GetFullPath(path))!;

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var root = doc.RootElement;

        var lvl = new LevelData
        {
            TileW = root.GetProperty("tilewidth").GetInt32(),
            TileH = root.GetProperty("tileheight").GetInt32(),
            Width = root.GetProperty("width").GetInt32(),
            Height = root.GetProperty("height").GetInt32(),
        };

        // ---- tilesets ----
        if (root.TryGetProperty("tilesets", out var tilesets))
            foreach (var ts in tilesets.EnumerateArray())
            {
                var t = ReadTileset(ts, mapDir);
                if (t != null) lvl.Tilesets.Add(t);
            }
        lvl.Tilesets.Sort((a, b) => a.FirstGid.CompareTo(b.FirstGid));

        // ---- camadas ----
        foreach (var layer in root.GetProperty("layers").EnumerateArray())
        {
            string type = Str(layer, "type");
            string name = Str(layer, "name");

            if (type == "tilelayer")
            {
                var tl = ReadTileLayer(layer, name);
                lvl.TileLayers.Add(tl);
                if (name.Equals("Solids", StringComparison.OrdinalIgnoreCase))
                    BuildSolids(tl, lvl);
            }
            else if (type == "objectgroup")
                ReadObjects(layer, lvl);
        }

        if (lvl.Solids.Count == 0)
            Console.WriteLine("[Tiled] AVISO: nenhuma colisao. Precisa existir uma camada de tiles chamada 'Solids' com tiles pintados.");
        if (!lvl.HasArt)
            Console.WriteLine("[Tiled] AVISO: nenhum tileset carregado, desenhando so retangulos.");

        return lvl;
    }

    // ------------------------------------------------------------ tilesets

    static Tileset? ReadTileset(JsonElement ts, string mapDir)
    {
        int firstGid = Int(ts, "firstgid", 1);

        // tileset externo (.tsx ou .tsj)
        if (ts.TryGetProperty("source", out var srcEl) && srcEl.ValueKind == JsonValueKind.String)
        {
            string p = Path.GetFullPath(Path.Combine(mapDir, srcEl.GetString()!));
            if (!File.Exists(p)) { Console.WriteLine($"[Tiled] Tileset nao encontrado: {p}"); return null; }
            string dir = Path.GetDirectoryName(p)!;

            if (p.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
                return ReadTsx(p, dir, firstGid);

            using var d = JsonDocument.Parse(File.ReadAllText(p));
            return FromJson(d.RootElement, dir, firstGid);
        }

        // tileset embutido no mapa
        return FromJson(ts, mapDir, firstGid);
    }

    static Tileset? FromJson(JsonElement e, string baseDir, int firstGid)
    {
        string image = Str(e, "image");
        if (image == "")
        {
            Console.WriteLine("[Tiled] Tileset de 'colecao de imagens' nao e suportado. Use um tileset baseado em uma imagem unica.");
            return null;
        }
        return Make(firstGid, Int(e, "tilewidth"), Int(e, "tileheight"),
            Int(e, "spacing"), Int(e, "margin"), Int(e, "columns"),
            Path.GetFullPath(Path.Combine(baseDir, image)));
    }

    static Tileset? ReadTsx(string file, string dir, int firstGid)
    {
        var x = XDocument.Load(file).Root!;
        var img = x.Element("image");
        if (img == null)
        {
            Console.WriteLine("[Tiled] Tileset de 'colecao de imagens' nao e suportado. Use um tileset baseado em uma imagem unica.");
            return null;
        }
        string source = (string?)img.Attribute("source") ?? "";
        return Make(firstGid,
            (int?)x.Attribute("tilewidth") ?? 0, (int?)x.Attribute("tileheight") ?? 0,
            (int?)x.Attribute("spacing") ?? 0, (int?)x.Attribute("margin") ?? 0,
            (int?)x.Attribute("columns") ?? 0,
            Path.GetFullPath(Path.Combine(dir, source)));
    }

    static Tileset? Make(int firstGid, int tw, int th, int spacing, int margin, int columns, string imagePath)
    {
        if (!textureCache.TryGetValue(imagePath, out var tex))
        {
            if (!File.Exists(imagePath)) { Console.WriteLine($"[Tiled] Imagem do tileset nao encontrada: {imagePath}"); return null; }
            tex = Raylib.LoadTexture(imagePath);
            if (tex.Id == 0) { Console.WriteLine($"[Tiled] Falha ao carregar a imagem: {imagePath}"); return null; }
            Raylib.SetTextureFilter(tex, TextureFilter.Point); // pixel art nítida
            textureCache[imagePath] = tex;
        }

        if (columns <= 0) columns = Math.Max(1, (tex.Width - 2 * margin + spacing) / (tw + spacing));

        return new Tileset
        {
            FirstGid = firstGid, TileW = tw, TileH = th,
            Columns = columns, Margin = margin, Spacing = spacing, Tex = tex
        };
    }

    // ------------------------------------------------------------ camadas

    static TileLayer ReadTileLayer(JsonElement layer, string name)
    {
        if (!layer.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException(
                $"A camada '{name}' nao esta em CSV. No Tiled: Map > Map Properties > " +
                "'Tile Layer Format' = CSV, e desmarque 'Infinite'. Depois exporte de novo.");

        var tl = new TileLayer
        {
            Name = name,
            W = layer.GetProperty("width").GetInt32(),
            H = layer.GetProperty("height").GetInt32(),
            OffX = Flt(layer, "offsetx", 0),
            OffY = Flt(layer, "offsety", 0),
            Opacity = Flt(layer, "opacity", 1),
            Visible = !layer.TryGetProperty("visible", out var v) || v.ValueKind != JsonValueKind.False,
            Foreground = name.StartsWith("fg", StringComparison.OrdinalIgnoreCase)
                      || name.StartsWith("front", StringComparison.OrdinalIgnoreCase),
        };

        tl.Gids = new uint[tl.W * tl.H];
        int i = 0;
        foreach (var el in data.EnumerateArray())
            tl.Gids[i++] = el.GetUInt32();
        return tl;
    }

    static void BuildSolids(TileLayer tl, LevelData lvl)
    {
        // junta tiles vizinhos na horizontal num único retângulo
        for (int row = 0; row < tl.H; row++)
        {
            int col = 0;
            while (col < tl.W)
            {
                if ((tl.Gids[row * tl.W + col] & 0x1FFFFFFF) == 0) { col++; continue; }

                int start = col;
                while (col < tl.W && (tl.Gids[row * tl.W + col] & 0x1FFFFFFF) != 0) col++;

                lvl.Solids.Add(new Rectangle(
                    start * lvl.TileW, row * lvl.TileH,
                    (col - start) * lvl.TileW, lvl.TileH));
            }
        }
    }

    static void ReadObjects(JsonElement layer, LevelData lvl)
    {
        foreach (var obj in layer.GetProperty("objects").EnumerateArray())
        {
            // Tiled 1.9+ usa "class"; antigos usam "type". Aceita os dois (e o nome como último recurso).
            string kind = Str(obj, "class");
            if (kind == "") kind = Str(obj, "type");
            if (kind == "") kind = Str(obj, "name");
            if (kind == "") continue;

            lvl.Spawns.Add(new SpawnPoint(kind, obj.GetProperty("x").GetSingle(), obj.GetProperty("y").GetSingle()));
        }
    }

    // ------------------------------------------------------------ helpers

    static string Str(JsonElement e, string prop)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    static int Int(JsonElement e, string prop, int def = 0)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : def;

    static float Flt(JsonElement e, string prop, float def)
        => e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetSingle() : def;
}
